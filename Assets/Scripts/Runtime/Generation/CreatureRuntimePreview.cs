using System.Collections.Generic;
using ProceduralCreature.Animation;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Animation.Skinned;
using ProceduralCreature.Appearance;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Serialization;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    public sealed class CreatureRuntimePreview : MonoBehaviour
    {
        [SerializeField] private TextAsset definitionJson;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private CreatureGenerationConfig generationConfig;

        private const string GeometryChildPrefix = "GeneratedGeometry_";

        private readonly List<GameObject> _geometryObjects = new List<GameObject>();
        private Material _previewMaterial;
        private CreatureGenerationScheduler _generationScheduler;
        private CreatureRig _rig;
        private CreatureSkinnedMeshRenderer _skinnedRenderer;

        private void Awake() => _generationScheduler = new CreatureGenerationScheduler();

        private void Start()
        {
            if (generateOnStart) Generate();
        }

        [ContextMenu("Generate Creature")]
        public void Generate()
        {
            CreatureDefinition definition = LoadDefinition();
            if (generationConfig != null)
                definition.Generation.VoxelsPerUnit = generationConfig.DefaultVoxelsPerUnit;
            _generationScheduler.Enqueue(definition, new GenerationDiagnostics(collectTimings: false));
        }

        private void Update()
        {
            if (_generationScheduler == null) return;
            while (_generationScheduler.TryTakeCompleted(out CreatureGenerationResult result))
            {
                if (result.IsStale) continue;
                if (!result.Succeeded)
                {
                    Debug.LogException(result.Exception, this);
                    continue;
                }

                GeneratedCreature generated = CreatureMeshGenerator.Assemble(result.Data, ResolveMeshAsset);
                MeshTopologyReport topology = result.Data.TopologyReport;

                DestroyGeneratedGeometry();
                BindImplicitSurfaceToRig(generated, result.Data);
                CreateRigAttachedGeometry(generated, result.Data.Snapshot);

                int implicitTriangles = 0;
                if (generated.TryGetImplicitSurface(out GeometryItem implicitSurface) && implicitSurface.Mesh != null)
                {
                    implicitTriangles = implicitSurface.Mesh.triangles.Length / 3;
                }
                Debug.Log($"[CreatureCreator] Runtime preview generated: {generated.Count} geometry item(s), " +
                          $"{implicitTriangles} implicit triangles.", this);
                if (!topology.IsWatertight)
                {
                    Debug.LogWarning("[CreatureCreator] Runtime preview implicit mesh is not watertight.", this);
                }
            }
        }

        private void OnDestroy()
        {
            _generationScheduler?.Dispose();
            _generationScheduler = null;
            DestroyGeneratedGeometry();
            if (_previewMaterial != null)
            {
                if (Application.isPlaying) Destroy(_previewMaterial);
                else DestroyImmediate(_previewMaterial);
                _previewMaterial = null;
            }
        }

        private CreatureDefinition LoadDefinition()
        {
            if (definitionJson != null)
                return new JsonDnaSerializer().Deserialize(definitionJson.text);
            return CreateDemoDefinition();
        }

        private Mesh ResolveMeshAsset(string key)
        {
            CreatureMeshPalette palette = generationConfig != null ? generationConfig.MeshPalette : null;
            if (palette != null && palette.TryResolve(key, out Mesh mesh)) return mesh;
            return null;
        }

        private CreatureMaterialPalette ResolveMaterialPalette()
            => generationConfig != null ? generationConfig.MaterialPalette : null;

        private InfluenceWeightingPolicy ResolveWeightingPolicy()
            => generationConfig != null ? generationConfig.WeightingPolicy : InfluenceWeightingPolicy.Default;

        private void CreateRigAttachedGeometry(GeneratedCreature generated, ResolvedCreatureSnapshot snapshot)
        {
            if (generated == null || _rig == null || snapshot == null) return;

            // Geometry order is semantic, not positional: the implicit surface may
            // appear anywhere in the collection, so inspect every item and skip it
            // by type rather than assuming it occupies slot zero.
            for (int index = 0; index < generated.Geometry.Count; index++)
            {
                GeometryItem item = generated.Geometry[index];
                if (item.GeometryType == GeometryType.Implicit) continue;
                if (item.Mesh == null)
                    throw new DomainException($"Generated geometry item {index} has no mesh.");
                if (item.RigBinding == null)
                    throw new DomainException($"Generated geometry item {index} has no RigBinding metadata.");

                if (!TryResolveGeometryBone(item.RigBinding, snapshot, out Transform bone))
                {
                    throw new DomainException(
                        $"Generated mesh asset '{item.RigBinding.SourcePartId}' could not resolve its rig bone " +
                        $"(mirrored={item.RigBinding.IsMirrored}).");
                }

                var go = new GameObject($"{GeometryChildPrefix}{index}");
                // Generated mesh vertices are already in creature/world rest space.
                // Preserve that placement while making the semantic bone the owner so
                // one-bone accessories follow the rig with no second skinning path.
                go.transform.SetParent(bone, worldPositionStays: true);
                go.AddComponent<MeshFilter>().sharedMesh = item.Mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                AssignItemMaterials(renderer, item);
                go.AddComponent<MeshCollider>().sharedMesh = item.Mesh;
                _geometryObjects.Add(go);
            }
        }

        private bool TryResolveGeometryBone(
            RigBindingMetadata binding,
            ResolvedCreatureSnapshot snapshot,
            out Transform bone)
        {
            bone = null;
            if (_rig == null || binding == null || snapshot == null) return false;
            string boneId = SemanticBoneResolver.ResolveGeometryAttachmentBoneId(
                snapshot, binding.SourcePartId, binding.IsMirrored);
            return _rig.TryGetBone(boneId, out bone);
        }

        private void AssignItemMaterials(MeshRenderer renderer, GeometryItem item)
        {
            int subMeshCount = Mathf.Max(1, item.Mesh != null ? item.Mesh.subMeshCount : 1);
            Material fallback = MaterialResolver.ResolveDefault(ResolveMaterialPalette());
            if (fallback == null)
            {
                if (_previewMaterial == null) _previewMaterial = CreatePreviewMaterial();
                fallback = _previewMaterial;
            }

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

                try
                {
                    Material resolved = MaterialResolver.Resolve(ResolveMaterialPalette(), region.MaterialKey);
                    materials[region.SubmeshIndex] = resolved ?? fallback;
                }
                catch (DomainException ex)
                {
                    Debug.LogWarning(
                        $"[CreatureCreator] {ex.Message} Using the default preview material for item '{item.SourcePartId}' submesh {region.SubmeshIndex}.",
                        this);
                }
            }

            renderer.sharedMaterials = materials;
        }

        private void BindImplicitSurfaceToRig(
            GeneratedCreature generated,
            GeneratedCreatureData data)
        {
            if (generated == null || data == null || data.Snapshot == null) return;
            if (!generated.TryGetImplicitSurface(out GeometryItem implicitItem))
            {
                Debug.LogWarning("[CreatureCreator] Runtime preview has no implicit surface to bind to a SkinnedMeshRenderer.", this);
                return;
            }

            SkeletonSnapshot skeletonSnapshot = data.SkeletonSnapshot;
            if (skeletonSnapshot == null || skeletonSnapshot.Count == 0)
            {
                Debug.LogWarning("[CreatureCreator] Runtime preview generated data has no resolved skeleton snapshot.", this);
                return;
            }

            IReadOnlyList<InfluenceDomain> vertexDomains = data.VertexInfluenceDomains;
            if (vertexDomains == null || vertexDomains.Count != implicitItem.Mesh.vertexCount)
            {
                throw new DomainException(
                    "Generated data has no complete implicit-surface influence-domain correspondence.");
            }

            if (_rig == null)
                _rig = gameObject.GetComponent<CreatureRig>() ?? gameObject.AddComponent<CreatureRig>();
            _rig.Build(skeletonSnapshot);
            _rig.ApplyPose(PosedSkeleton.FromRestPose(skeletonSnapshot));

            if (_skinnedRenderer == null)
            {
                _skinnedRenderer = gameObject.GetComponent<CreatureSkinnedMeshRenderer>()
                    ?? gameObject.AddComponent<CreatureSkinnedMeshRenderer>();
            }

            float[] radiiByBoneIndex = MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(
                skeletonSnapshot, data.Snapshot);
            Material defaultMaterial = MaterialResolver.ResolveDefault(ResolveMaterialPalette());
            Material[] materials = defaultMaterial != null ? new[] { defaultMaterial } : null;
            _skinnedRenderer.Bind(
                _rig, skeletonSnapshot, implicitItem.Mesh, radiiByBoneIndex, materials, vertexDomains, ResolveWeightingPolicy());
            if (_skinnedRenderer.Renderer != null) _skinnedRenderer.Renderer.enabled = true;
        }

        private void DestroyGeneratedGeometry()
        {
            for (int i = _geometryObjects.Count - 1; i >= 0; i--)
            {
                if (_geometryObjects[i] == null) continue;
                if (Application.isPlaying) Destroy(_geometryObjects[i]);
                else DestroyImmediate(_geometryObjects[i]);
            }
            _geometryObjects.Clear();

            if (_skinnedRenderer != null)
            {
                _skinnedRenderer.Clear();
                if (Application.isPlaying) Destroy(_skinnedRenderer);
                else DestroyImmediate(_skinnedRenderer);
                _skinnedRenderer = null;
            }

            if (_rig != null)
            {
                _rig.Clear();
                if (Application.isPlaying) Destroy(_rig);
                else DestroyImmediate(_rig);
                _rig = null;
            }
        }

        private static Material CreatePreviewMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard")
                            ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogWarning("[CreatureCreator] No default shader found; preview meshes will use Unity's fallback material.");
                return null;
            }
            return new Material(shader);
        }

        private static CreatureDefinition CreateDemoDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 12f };
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 1.1f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 1.3f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 1.0f });
            definition.AddPart(new CreaturePart
            {
                Id = "runtime_head",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                DisplayName = "Head",
                Transform = new TransformData { Position = new Vector3(0f, 1.45f, 1.4f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.85f, SmoothBlendRadius = 0.2f },
                Appearance = new AppearanceDefinition { BaseColor = new Color(0.3f, 0.72f, 0.86f, 1f), NoiseSeed = 11, NoiseScale = 1.5f },
            });
            return definition;
        }
    }
}