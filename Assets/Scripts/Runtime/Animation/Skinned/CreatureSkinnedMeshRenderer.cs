using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;
using SkeletonModel = ProceduralCreature.Skeleton.Skeleton;

namespace ProceduralCreature.Animation.Skinned
{
    public sealed class CreatureSkinnedMeshRenderer : MonoBehaviour
    {
        private const string SkinnedObjectName = "SkinnedMesh";
        private readonly List<GameObject> _generatedObjects = new List<GameObject>();
        private readonly List<Mesh> _ownedMeshes = new List<Mesh>();
        private SkinnedMeshRenderer _renderer;
        private Transform[] _bones = new Transform[0];
        private IReadOnlyList<Transform> _bonesView = Array.AsReadOnly(new Transform[0]);
        private CreatureRig _rig;

        public SkinnedMeshRenderer Renderer => _renderer;
        public CreatureRig Rig => _rig;
        public IReadOnlyList<Transform> Bones => _bonesView;

        public void Bind(
            CreatureRig rig,
            SkeletonModel restSkeleton,
            Mesh sourceMesh,
            IReadOnlyList<float> radiiByBoneIndex = null,
            IReadOnlyList<Material> materials = null,
            IReadOnlyList<InfluenceDomain> vertexDomains = null)
        {
            if (rig == null) throw new DomainException("rig must not be null.");
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");
            if (sourceMesh == null) throw new DomainException("sourceMesh must not be null.");
            if (rig.IndexedBones.Count == 0) throw new DomainException("rig must be built (Build) before binding a mesh.");

            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(restSkeleton);
            if (snapshot.Count != rig.IndexedBones.Count)
            {
                throw new DomainException("The rest skeleton does not match the rig: bind bone count " + snapshot.Count +
                    " != rig indexed bone count " + rig.IndexedBones.Count + ".");
            }

            Vector3[] restVertices = sourceMesh.vertices;
            if (restVertices == null || restVertices.Length == 0) throw new DomainException("sourceMesh has no vertices to bind.");

            List<BoneSegmentInfluence> segments =
                ImplicitSurfaceWeightAuthoring.BuildBindingInfluences(snapshot, radiiByBoneIndex);
            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(segments, restVertices, vertexDomains);
            Matrix4x4[] bindposes = SkinnedMeshBindingBuilder.ComputeBindposes(snapshot);
            BoneWeight[] boneWeights = SkinnedMeshBindingBuilder.BuildBoneWeights(weights, snapshot.Count);

            // Build the replacement completely before touching the existing valid bind.
            // Binding can be expensive on large welded meshes; a later Unity allocation
            // or mesh-copy failure must not leave the preview unrenderable. Commit the
            // new presentation only after every step below succeeds.
            Mesh skinnedMesh = null;
            GameObject skinnedObject = null;
            try
            {
                skinnedMesh = BuildSkinningMeshCopy(sourceMesh, bindposes, boneWeights);

                skinnedObject = new GameObject(SkinnedObjectName);
                skinnedObject.transform.SetParent(transform, worldPositionStays: false);
                SkinnedMeshRenderer renderer = skinnedObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = skinnedMesh;
                renderer.rootBone = rig.IndexedBones[snapshot.RootIndex];
                var bones = new Transform[rig.IndexedBones.Count];
                for (int i = 0; i < bones.Length; i++) bones[i] = rig.IndexedBones[i];
                renderer.bones = bones;
                renderer.enabled = false;
                if (materials != null)
                {
                    var resolved = new Material[Mathf.Max(1, sourceMesh.subMeshCount)];
                    for (int i = 0; i < resolved.Length; i++) resolved[i] = i < materials.Count ? materials[i] : null;
                    renderer.sharedMaterials = resolved;
                }

                // Commit: the new Unity presentation is valid. Only now destroy the
                // previous bind. This keeps rebinding failure-safe without changing the
                // per-frame animation path.
                Clear();

                _renderer = renderer;
                _bones = bones;
                _bonesView = Array.AsReadOnly(bones);
                _rig = rig;
                _ownedMeshes.Add(skinnedMesh);
                _generatedObjects.Add(skinnedObject);
                return;
            }
            catch
            {
                if (skinnedObject != null)
                {
                    if (Application.isPlaying) Destroy(skinnedObject);
                    else DestroyImmediate(skinnedObject);
                }
                else if (skinnedMesh != null)
                {
                    if (Application.isPlaying) Destroy(skinnedMesh);
                    else DestroyImmediate(skinnedMesh);
                }
                throw;
            }
        }

        public void Clear()
        {
            for (int i = _generatedObjects.Count - 1; i >= 0; i--)
            {
                if (_generatedObjects[i] == null) continue;
                if (Application.isPlaying) Destroy(_generatedObjects[i]);
                else DestroyImmediate(_generatedObjects[i]);
            }
            _generatedObjects.Clear();
            for (int i = _ownedMeshes.Count - 1; i >= 0; i--)
            {
                Mesh mesh = _ownedMeshes[i];
                if (mesh == null) continue;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            _ownedMeshes.Clear();
            _renderer = null;
            _bones = new Transform[0];
            _bonesView = Array.AsReadOnly(_bones);
            _rig = null;
        }

        private void OnDestroy() => Clear();

        private static Mesh BuildSkinningMeshCopy(Mesh source, Matrix4x4[] bindposes, BoneWeight[] boneWeights)
        {
            var mesh = new Mesh { name = source.name + "_Skinned", indexFormat = source.indexFormat };
            Vector3[] vertices = source.vertices;
            mesh.vertices = vertices;
            if (source.normals != null && source.normals.Length == vertices.Length) mesh.normals = source.normals;
            if (source.tangents != null && source.tangents.Length == vertices.Length) mesh.tangents = source.tangents;
            if (source.uv != null && source.uv.Length == vertices.Length) mesh.uv = source.uv;
            if (source.uv2 != null && source.uv2.Length == vertices.Length) mesh.uv2 = source.uv2;
            if (source.uv3 != null && source.uv3.Length == vertices.Length) mesh.uv3 = source.uv3;
            if (source.colors != null && source.colors.Length == vertices.Length) mesh.colors = source.colors;

            int subMeshCount = Mathf.Max(1, source.subMeshCount);
            mesh.subMeshCount = subMeshCount;
            for (int sub = 0; sub < subMeshCount; sub++) mesh.SetTriangles(source.GetTriangles(sub), sub);
            mesh.bindposes = bindposes;
            mesh.boneWeights = boneWeights;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
