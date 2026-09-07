using System.Collections.Generic;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;
using SkeletonModel = ProceduralCreature.Skeleton.Skeleton;

namespace ProceduralCreature.Animation.Skinned
{
    /// <summary>
    /// The MVP presentation adapter: installs already-authored binding data plus the
    /// <see cref="CreatureRig"/> indexed bone hierarchy into a real Unity
    /// <see cref="SkinnedMeshRenderer"/>. This is the connective tissue between the
    /// pure resolved-creature generation output and a skinned runtime render.
    ///
    /// OWNERSHIP / SEPARATION (TSK-0132):
    /// * This adapter is NOT inside <c>CreatureRig</c> and NOT in the pure generation
    ///   path. It is a separate runtime presenter that consumes (a) a source rest mesh
    ///   (the implicit welded-surface item), (b) the <see cref="Skeleton.Skeleton"/>
    ///   the rig was built from, and (c) an already-built <c>CreatureRig</c>.
    /// * It is a PURE data→Unity-Mesh converter at bind time. <c>GeneratedCreature</c>
    ///   stays pose-free; this component holds no pose and does no per-frame work.
    ///   Animation ticks change only bone <c>Transform</c> position/rotation (via
    ///   <c>CreatureRig.ApplyPose</c>) — there is no per-frame
    ///   SetVertices/RecalculateNormals/RecalculateBounds and no weight/bindpose/
    ///   rebind rebuild.
    /// * The source mesh is NEVER modified: a skinning mesh is built as a deep copy
    ///   that preserves the source submesh structure, then gets bindposes + boneWeights.
    ///   All generated Unity objects created by a bind are owned here and destroyed on
    ///   rebind or teardown, so no orphaned generated objects remain.
    ///
    /// SPACE CONTRACT (identity): mesh-local == creature-space == rig-host-space at
    /// identity. Like <c>CreatureRig</c>, the host GameObject that owns this adapter
    /// and the rig must remain at identity (position zero, rotation identity, scale
    /// one) so bone frames and mesh vertices share one coordinate space.
    ///
    /// BIND INDEX CONTRACT: weights are authored with <c>VertexInfluence.BoneIndex</c> =
    /// index into <see cref="SkeletonSnapshot.Capture"/> order (TSK-0131). Bind re-
    /// captures the supplied rest skeleton so its bone order and <see cref="CreatureRig"/>
    /// (which captures the same skeleton internally) are index-parallel, and feeds the
    /// rig's indexed <c>Transform[]</c> as <c>SkinnedMeshRenderer.bones</c>.
    ///
    /// WELDED-SURFACE SCOPE (this round): the adapter wires the implicit welded surface
    /// (the MVP-critical whole-creature surface) and any item whose mesh it can skin.
    /// Weights are authored once by <see cref="ImplicitSurfaceWeightAuthoring"/> over
    /// the segment influences built from the rest skeleton; this component supplies the
    /// morphology-derived radius bridge (<paramref name="radiiByBoneIndex"/>, per-bone
    /// Body-sample/limb thickness) that the pure authoring core deliberately does not
    /// guess. Rigid mesh-asset weighting (TSK-0130) is out of scope here.
    /// </summary>
    public sealed class CreatureSkinnedMeshRenderer : MonoBehaviour
    {
        private const string SkinnedObjectName = "SkinnedMesh";

        private readonly List<GameObject> _generatedObjects = new List<GameObject>();
        private readonly List<Mesh> _ownedMeshes = new List<Mesh>();
        private SkinnedMeshRenderer _renderer;
        private Transform[] _bones = new Transform[0];
        private CreatureRig _rig;

        /// <summary>The live SkinnedMeshRenderer this adapter owns, or null before/after bind.</summary>
        public SkinnedMeshRenderer Renderer => _renderer;

        /// <summary>The rig this adapter is bound to (bone Transform source).</summary>
        public CreatureRig Rig => _rig;

        /// <summary>The bone Transforms in bind order currently fed to the renderer.</summary>
        public IReadOnlyList<Transform> Bones => _bones;

        /// <summary>
        /// Binds the implicit welded-surface mesh to the given rig. Builds the owned
        /// skinning mesh copy (bindposes + boneWeights) and the <see cref="SkinnedMeshRenderer"/>
        /// ONCE; subsequent calls replace the previous bind and destroy its generated objects.
        /// </summary>
        /// <param name="rig">An already-<see cref="CreatureRig.Build"/>t rig whose indexed
        /// bones are in <see cref="SkeletonSnapshot.Capture"/> order.</param>
        /// <param name="restSkeleton">The rest skeleton the rig was built from (defines bind
        /// frames and the bone-index order).</param>
        /// <param name="sourceMesh">Source rest mesh (creature-space); never modified.</param>
        /// <param name="radiiByBoneIndex">Optional morphology-derived influence radius per bone
        /// (Body-sample radius / limb thickness). Falls back to
        /// <see cref="ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius"/>.</param>
        /// <param name="materials">Optional resolved materials, index-parallel to the source
        /// submeshes (submesh/material preservation, ADR-009). When null, a single default
        /// material slot is left for the caller to assign (rendering material wiring is not
        /// this adapter's concern).</param>
        /// <param name="vertexDomains">Optional resolved geometry domains, index-parallel to
        /// source vertices. When supplied, authoring admits only bones from each vertex's
        /// domain; null preserves the legacy unrestricted welded-surface contract.</param>
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
            if (rig.IndexedBones.Count == 0)
            {
                throw new DomainException("rig must be built (Build) before binding a mesh.");
            }

            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(restSkeleton);
            if (snapshot.Count != rig.IndexedBones.Count)
            {
                throw new DomainException(
                    "The rest skeleton does not match the rig: bind bone count " + snapshot.Count +
                    " != rig indexed bone count " + rig.IndexedBones.Count + ".");
            }

            Vector3[] restVertices = sourceMesh.vertices;
            if (restVertices == null || restVertices.Length == 0)
            {
                throw new DomainException("sourceMesh has no vertices to bind.");
            }

            // Author welded-surface weights once at bind time (TSK-0131 consumes the shared
            // SkeletonSnapshot.Capture bone-index contract; this adapter owns the radius bridge).
            List<BoneSegmentInfluence> segments =
                ImplicitSurfaceWeightAuthoring.BuildBindingInfluences(snapshot, radiiByBoneIndex);
            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(
                segments, restVertices, vertexDomains);

            Matrix4x4[] bindposes = SkinnedMeshBindingBuilder.ComputeBindposes(snapshot);
            BoneWeight[] boneWeights = SkinnedMeshBindingBuilder.BuildBoneWeights(weights, snapshot.Count);

            // Rebind: destroy the previous bind's generated objects before building the new one.
            Clear();

            Mesh skinnedMesh = BuildSkinningMeshCopy(sourceMesh, bindposes, boneWeights);
            _ownedMeshes.Add(skinnedMesh);

            var skinnedObject = new GameObject(SkinnedObjectName);
            skinnedObject.transform.SetParent(transform, worldPositionStays: false);
            SkinnedMeshRenderer renderer = skinnedObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = skinnedMesh;
            renderer.rootBone = rig.IndexedBones[snapshot.RootIndex];
            var bones = new Transform[rig.IndexedBones.Count];
            for (int i = 0; i < bones.Length; i++) bones[i] = rig.IndexedBones[i];
            renderer.bones = bones;
            renderer.enabled = false; // pure data converter: rendering enabled by the caller/driver
            if (materials != null)
            {
                var resolved = new Material[Mathf.Max(1, sourceMesh.subMeshCount)];
                for (int i = 0; i < resolved.Length; i++)
                {
                    resolved[i] = i < materials.Count ? materials[i] : null;
                }
                renderer.sharedMaterials = resolved;
            }

            _renderer = renderer;
            _bones = bones;
            _rig = rig;
            _generatedObjects.Add(skinnedObject);
        }

        /// <summary>
        /// Destroys the owned renderer GameObject and skinning meshes. Safe to call
        /// repeatedly. Bone Transforms belong to the rig (not owned here).
        /// </summary>
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
            _rig = null;
        }

        private void OnDestroy()
        {
            Clear();
        }

        /// <summary>
        /// Builds an owned skinning mesh that copies the source mesh's geometry and
        /// preserves its index format and submesh structure (ADR-009), then attaches
        /// bindposes and boneWeights. The source mesh is never modified.
        /// </summary>
        private static Mesh BuildSkinningMeshCopy(
            Mesh source, Matrix4x4[] bindposes, BoneWeight[] boneWeights)
        {
            var mesh = new Mesh();
            mesh.name = source.name + "_Skinned";
            mesh.indexFormat = source.indexFormat;

            Vector3[] vertices = source.vertices;
            mesh.vertices = vertices;

            if (source.normals != null && source.normals.Length == vertices.Length)
            {
                mesh.normals = source.normals;
            }
            if (source.tangents != null && source.tangents.Length == vertices.Length)
            {
                mesh.tangents = source.tangents;
            }
            if (source.uv != null && source.uv.Length == vertices.Length)
            {
                mesh.uv = source.uv;
            }
            if (source.uv2 != null && source.uv2.Length == vertices.Length)
            {
                mesh.uv2 = source.uv2;
            }
            if (source.uv3 != null && source.uv3.Length == vertices.Length)
            {
                mesh.uv3 = source.uv3;
            }
            if (source.colors != null && source.colors.Length == vertices.Length)
            {
                mesh.colors = source.colors;
            }

            int subMeshCount = Mathf.Max(1, source.subMeshCount);
            mesh.subMeshCount = subMeshCount;
            for (int sub = 0; sub < subMeshCount; sub++)
            {
                mesh.SetTriangles(source.GetTriangles(sub), sub);
            }

            mesh.bindposes = bindposes;
            mesh.boneWeights = boneWeights;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
