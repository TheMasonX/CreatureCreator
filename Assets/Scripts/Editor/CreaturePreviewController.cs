using System;
using System.Collections.Generic;
using ProceduralCreature.Animation;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Animation.Skinned;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Skeleton;
using UnityEditor;
using UnityEngine;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Owns the editor preview root and its generated geometry children, and the
    /// runtime generation lifecycle that produces them.
    /// </summary>
    internal sealed class CreaturePreviewController : IDisposable
    {
        private const string PreviewObjectName = "CreatureCreator Preview";
        internal const string RootEntityKey = "ProceduralCreature.Editor.Preview.RootEntityId";
        internal const string GeometryEntityIdsKey = "ProceduralCreature.Editor.Preview.GeometryEntityIds";

        private readonly CreatureGenerationScheduler _scheduler = new CreatureGenerationScheduler();
        private readonly CreaturePreviewRequestState _requestState = new CreaturePreviewRequestState();
        private readonly Func<Material> _defaultMaterialResolver;
        private readonly Func<string, Material> _materialResolver;
        private bool _disposed;

        public CreaturePreviewController(
            Func<Material> defaultMaterialResolver,
            Func<string, Material> materialResolver)
        {
            _defaultMaterialResolver = defaultMaterialResolver ?? throw new ArgumentNullException(nameof(defaultMaterialResolver));
            _materialResolver = materialResolver ?? throw new ArgumentNullException(nameof(materialResolver));
        }

        public GameObject PreviewGameObject { get; private set; }

        public long Enqueue(CreatureDefinition definition, float voxelsPerUnit, bool logDiagnostics)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_disposed) throw new ObjectDisposedException(nameof(CreaturePreviewController));

            // The preview controller owns the detached request capture because it
            // must configure generation settings before handing work to the scheduler.
            // EnqueueCaptured transfers that already-detached snapshot without a
            // redundant second deep clone (TSK-0104).
            CreatureDefinition captured = definition.Clone();
            captured.Generation.VoxelsPerUnit = voxelsPerUnit;
            long requestId = _scheduler.EnqueueCaptured(captured, new GenerationDiagnostics(logDiagnostics));
            _requestState.BeginRequest(requestId);
            return requestId;
        }

        public void ProcessCompletions(Func<string> currentRevisionResolver, Action<CreatureGenerationResult> onCompleted)
        {
            if (currentRevisionResolver == null) throw new ArgumentNullException(nameof(currentRevisionResolver));
            if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));
            if (_disposed) return;

            while (_scheduler.TryTakeCompleted(out CreatureGenerationResult result))
            {
                if (!result.Succeeded)
                {
                    bool isCurrent = _requestState.IsCurrentRequest(result.Sequence);
                    if (isCurrent) _requestState.Clear();
                    if (isCurrent) onCompleted(result);
                    continue;
                }

                string currentRevisionId;
                try
                {
                    currentRevisionId = currentRevisionResolver();
                }
                catch (DomainException)
                {
                    if (_requestState.IsCurrentRequest(result.Sequence)) _requestState.Clear();
                    continue;
                }

                if (!_requestState.TryAcceptResult(
                        result.Sequence,
                        result.Data?.Snapshot?.RevisionId,
                        currentRevisionId)) continue;
                onCompleted(result);
            }
        }

        public void ApplyPreviewGeometry(
            GeneratedCreature generated,
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot)
        {
            if (generated == null) throw new ArgumentNullException(nameof(generated));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (_disposed) throw new ObjectDisposedException(nameof(CreaturePreviewController));

            if (!generated.TryGetImplicitSurface(out GeometryItem implicitSurface))
            {
                ClearGeometryObjects();
                return;
            }

            ClearGeometryObjects();
            BindImplicitSurface(implicitSurface.Mesh, definition, snapshot);
            AttachMeshAssets(generated, snapshot);
        }

        public void ApplyPreviewGeometry(
            GeneratedCreature generated,
            GeneratedCreatureData data)
        {
            if (generated == null) throw new ArgumentNullException(nameof(generated));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (_disposed) throw new ObjectDisposedException(nameof(CreaturePreviewController));

            if (!generated.TryGetImplicitSurface(out GeometryItem implicitSurface))
            {
                ClearGeometryObjects();
                return;
            }
            if (data.VertexInfluenceDomains == null ||
                data.VertexInfluenceDomains.Count != implicitSurface.Mesh.vertexCount)
            {
                throw new DomainException(
                    "Generated data has no complete implicit-surface influence-domain correspondence.");
            }
            if (data.SkeletonSnapshot == null || data.SkeletonSnapshot.Count == 0)
            {
                throw new DomainException("Generated data has no resolved skeleton snapshot for preview binding.");
            }

            ClearGeometryObjects();
            BindImplicitSurface(
                implicitSurface.Mesh,
                data.Definition,
                data.Snapshot,
                data.SkeletonSnapshot,
                data.VertexInfluenceDomains);
            AttachMeshAssets(generated, data.Snapshot);
        }

        private void AttachMeshAssets(GeneratedCreature generated, ResolvedCreatureSnapshot snapshot)
        {
            CreatureRig rig = GetSingleOwnedComponent<CreatureRig>(clearGeneratedState: false);
            if (rig == null || rig.RestSkeleton == null)
            {
                throw new DomainException("Preview rig must be built before mesh-asset geometry is attached.");
            }

            // GeneratedCreature intentionally does not guarantee implicit geometry at
            // position zero. Search by semantic geometry type so adding/reordering
            // geometry items cannot silently skip a renderable mesh asset or treat the
            // implicit surface as a rigid attachment.
            for (int i = 0; i < generated.Geometry.Count; i++)
            {
                GeometryItem item = generated.Geometry[i];
                if (item.GeometryType == GeometryType.Implicit) continue;

                if (item.Mesh == null) throw new DomainException($"Generated mesh asset item {i} has no mesh.");
                if (item.RigBinding == null)
                {
                    throw new DomainException($"Generated mesh asset item {i} has no rig binding metadata.");
                }

                if (!TryResolveGeometryBone(rig, item.RigBinding, snapshot, out Transform bone))
                {
                    throw new DomainException(
                        $"Generated mesh asset '{item.RigBinding.SourcePartId}' could not resolve its rig bone " +
                        $"(mirrored={item.RigBinding.IsMirrored}).");
                }

                var child = new GameObject("Preview Mesh " + i);
                child.transform.SetParent(bone, worldPositionStays: true);
                child.AddComponent<MeshFilter>().sharedMesh = item.Mesh;
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                AssignMaterials(renderer, item);
                RegisterOwnedGeometry(child);
            }
        }

        private static bool TryResolveGeometryBone(
            CreatureRig rig,
            RigBindingMetadata binding,
            ResolvedCreatureSnapshot snapshot,
            out Transform bone)
        {
            bone = null;
            if (binding == null || rig == null || snapshot == null) return false;
            string boneId = SemanticBoneResolver.ResolveGeometryAttachmentBoneId(
                snapshot, binding.SourcePartId, binding.IsMirrored);
            return rig.TryGetBone(boneId, out bone);
        }

        private T GetSingleOwnedComponent<T>(bool clearGeneratedState = true) where T : Component
        {
            if (PreviewGameObject == null) return null;
            T[] components = PreviewGameObject.GetComponents<T>();
            if (components == null || components.Length == 0) return null;

            T retained = components[0];
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null) continue;

                if (clearGeneratedState)
                {
                    if (component is CreatureRig rig) rig.Clear();
                    else if (component is CreatureSkinnedMeshRenderer skinned) skinned.Clear();
                }

                if (component != retained) UnityEngine.Object.DestroyImmediate(component);
            }
            return retained;
        }

        public GameObject RecoverExistingPreview()
        {
            if (PreviewGameObject != null) return PreviewGameObject;
            EntityId handle = ReadRootEntity();
            if (!handle.IsValid()) return null;

            GameObject root = EditorUtility.EntityIdToObject(handle) as GameObject;
            if (root == null)
            {
                SessionState.EraseString(RootEntityKey);
                return null;
            }

            PreviewGameObject = root;
            return PreviewGameObject;
        }

        private void EnsurePreviewRoot()
        {
            if (PreviewGameObject != null) return;
            RecoverExistingPreview();
            if (PreviewGameObject != null) return;

            PreviewGameObject = new GameObject(PreviewObjectName);
            PreviewGameObject.AddComponent<MeshCollider>();
            PersistRootEntity(PreviewGameObject.GetEntityId());
        }

        private void BindImplicitSurface(
            Mesh sourceMesh,
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot)
        {
            SkeletonModel skeleton = SkeletonInferrer.Infer(snapshot);
            if (skeleton == null || skeleton.Bones.Count == 0)
            {
                throw new DomainException("Preview snapshot did not produce a skeleton for the implicit surface.");
            }

            SkeletonSnapshot snapshotForBinding = SkeletonSnapshot.Capture(skeleton);
            // This compatibility path predates GeneratedCreatureData correspondence.
            // Production generation uses the overload below so the binding pass cannot
            // silently recompile SDF programs or reclassify every vertex.
            float[] radiiByBoneIndex = MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(
                snapshotForBinding, snapshot);
            InfluenceDomain[] vertexDomains = ImplicitSurfaceInfluenceDomainResolver.Resolve(
                definition, snapshot, sourceMesh.vertices);
            BindImplicitSurface(
                sourceMesh,
                definition,
                snapshot,
                snapshotForBinding,
                vertexDomains,
                radiiByBoneIndex);
        }

        private void BindImplicitSurface(
            Mesh sourceMesh,
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            SkeletonSnapshot skeletonSnapshot,
            IReadOnlyList<InfluenceDomain> vertexDomains)
        {
            float[] radiiByBoneIndex = MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(
                skeletonSnapshot, snapshot);
            BindImplicitSurface(
                sourceMesh,
                definition,
                snapshot,
                skeletonSnapshot,
                vertexDomains,
                radiiByBoneIndex);
        }

        private void BindImplicitSurface(
            Mesh sourceMesh,
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            SkeletonSnapshot skeletonSnapshot,
            IReadOnlyList<InfluenceDomain> vertexDomains,
            IReadOnlyList<float> radiiByBoneIndex)
        {
            EnsurePreviewRoot();

            MeshFilter rawFilter = PreviewGameObject.GetComponent<MeshFilter>();
            if (rawFilter == null) rawFilter = PreviewGameObject.AddComponent<MeshFilter>();
            rawFilter.sharedMesh = sourceMesh;

            MeshRenderer rawRenderer = PreviewGameObject.GetComponent<MeshRenderer>();
            if (rawRenderer == null) rawRenderer = PreviewGameObject.AddComponent<MeshRenderer>();
            Material rawMaterial = _defaultMaterialResolver();
            if (rawMaterial != null) rawRenderer.sharedMaterial = rawMaterial;
            rawRenderer.enabled = false;

            MeshCollider collider = PreviewGameObject.GetComponent<MeshCollider>();
            if (collider == null) collider = PreviewGameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = sourceMesh;

            if (skeletonSnapshot == null || skeletonSnapshot.Count == 0)
            {
                throw new DomainException("Preview snapshot did not produce a skeleton for the implicit surface.");
            }
            if (vertexDomains == null || vertexDomains.Count != sourceMesh.vertexCount)
            {
                throw new DomainException("Preview implicit surface has incomplete influence-domain correspondence.");
            }

            CreatureRig rig = GetSingleOwnedComponent<CreatureRig>();
            if (rig == null) rig = PreviewGameObject.AddComponent<CreatureRig>();
            rig.Build(skeletonSnapshot);
            rig.ApplyPose(PosedSkeleton.FromRestPose(skeletonSnapshot));

            CreatureSkinnedMeshRenderer skinnedRenderer = GetSingleOwnedComponent<CreatureSkinnedMeshRenderer>();
            if (skinnedRenderer == null) skinnedRenderer = PreviewGameObject.AddComponent<CreatureSkinnedMeshRenderer>();

            Material defaultMaterial = _defaultMaterialResolver();
            Material[] materials = defaultMaterial != null ? new[] { defaultMaterial } : null;
            skinnedRenderer.Bind(
                rig,
                skeletonSnapshot,
                sourceMesh,
                radiiByBoneIndex,
                materials,
                vertexDomains);
            if (skinnedRenderer.Renderer != null) skinnedRenderer.Renderer.enabled = true;
        }

        private void AssignMaterials(MeshRenderer renderer, GeometryItem item)
        {
            int subMeshCount = Mathf.Max(1, item.Mesh != null ? item.Mesh.subMeshCount : 1);
            Material fallback = _defaultMaterialResolver();
            var materials = new Material[subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = fallback;

            for (int i = 0; i < item.MaterialRegions.Count; i++)
            {
                MaterialRegion region = item.MaterialRegions[i];
                if (region.SubmeshIndex < 0 || region.SubmeshIndex >= materials.Length)
                {
                    throw new DomainException(
                        $"Generated geometry item '{item.SourcePartId}' material region {i} targets submesh {region.SubmeshIndex}, " +
                        $"but the mesh has {materials.Length} submesh slots.");
                }

                Material resolved = null;
                try
                {
                    resolved = _materialResolver(region.MaterialKey);
                }
                catch (DomainException ex)
                {
                    Debug.LogWarning(
                        $"[CreatureCreator] {ex.Message} Using the default preview material for item '{item.SourcePartId}' submesh {region.SubmeshIndex}.",
                        PreviewGameObject);
                }

                materials[region.SubmeshIndex] = resolved != null ? resolved : fallback;
            }

            if (materials.Length > 0) renderer.sharedMaterials = materials;
        }

        private void ClearGeometryObjects()
        {
            List<ulong> owned = ReadOwnedGeometryEntities();
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                GameObject child = EditorUtility.EntityIdToObject(EntityId.FromULong(owned[i])) as GameObject;
                if (child != null) UnityEngine.Object.DestroyImmediate(child);
            }
            PersistOwnedGeometryEntities(new List<ulong>());

            MeshFilter rawFilter = PreviewGameObject != null ? PreviewGameObject.GetComponent<MeshFilter>() : null;
            if (rawFilter != null) rawFilter.sharedMesh = null;
            MeshRenderer rawRenderer = PreviewGameObject != null ? PreviewGameObject.GetComponent<MeshRenderer>() : null;
            if (rawRenderer != null) rawRenderer.enabled = false;

            GetSingleOwnedComponent<CreatureSkinnedMeshRenderer>()?.Clear();
            GetSingleOwnedComponent<CreatureRig>()?.Clear();
        }

        private static void RegisterOwnedGeometry(GameObject child)
        {
            List<ulong> owned = ReadOwnedGeometryEntities();
            ulong id = EntityId.ToULong(child.GetEntityId());
            if (!owned.Contains(id)) owned.Add(id);
            PersistOwnedGeometryEntities(owned);
        }

        private static List<ulong> ReadOwnedGeometryEntities()
        {
            var ids = new List<ulong>();
            string raw = SessionState.GetString(GeometryEntityIdsKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return ids;
            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
                if (ulong.TryParse(parts[i], out ulong id) && !ids.Contains(id)) ids.Add(id);
            return ids;
        }

        private static void PersistOwnedGeometryEntities(List<ulong> ids) => SessionState.SetString(GeometryEntityIdsKey, string.Join(",", ids));

        private static void PersistRootEntity(EntityId entity) => SessionState.SetString(RootEntityKey, entity.IsValid() ? EntityId.ToULong(entity).ToString() : string.Empty);

        private static EntityId ReadRootEntity()
        {
            string raw = SessionState.GetString(RootEntityKey, string.Empty);
            return ulong.TryParse(raw, out ulong value) ? EntityId.FromULong(value) : EntityId.None;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _requestState.Clear();
            _scheduler.Dispose();
        }
    }
}