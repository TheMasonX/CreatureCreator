using System.Collections.Generic;
using ProceduralCreature.Appearance;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;
using ProceduralCreature.Serialization;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    public sealed class CreatureRuntimePreview : MonoBehaviour
    {
        [SerializeField] private TextAsset definitionJson;
        [SerializeField] private bool generateOnStart = true;

        [SerializeField] private CreatureGenerationConfig generationConfig;

        /// <summary>
        /// The shared material palette (CC-028). When a geometry item's part carries
        /// a submaterial key, it resolves through this palette — the same asset the
        /// editor preview uses, so both resolve identically. Null means mesh-asset
        /// items keep the default preview material (and a set-but-unresolvable key
        /// logs a warning rather than breaking Play Mode).
        /// </summary>
        [SerializeField] private CreatureMaterialPalette materialPalette;

        private const string GeometryChildPrefix = "GeneratedGeometry_";

        private readonly List<GameObject> _geometryObjects = new List<GameObject>();
        private Material _previewMaterial;

        private void Start()
        {
            if (generateOnStart) Generate();
        }

        [ContextMenu("Generate Creature")]
        public void Generate()
        {
            CreatureDefinition definition = LoadDefinition().Clone();
            if (generationConfig != null)
            {
                definition.Generation.VoxelsPerUnit = generationConfig.DefaultVoxelsPerUnit;
            }
            var diagnostics = new GenerationDiagnostics(collectTimings: false);
            MeshTopologyReport topology;
            GeneratedCreature generated = CreatureMeshGenerator.Generate(
                definition, out topology, diagnostics,
                usePortableSampling: generationConfig == null || generationConfig.UsePortableSampling,
                meshResolver: ResolveMeshAsset,
                cullingMode: generationConfig != null ? generationConfig.CullingMode : SdfCullingMode.Exact);

            DestroyGeneratedGeometry();

            for (int i = 0; i < generated.Geometry.Count; i++)
            {
                CreateGeometryObject(i, generated.Geometry[i]);
            }

            int implicitTriangles = generated.MainMesh != null ? generated.MainMesh.triangles.Length / 3 : 0;
            Debug.Log($"[CreatureCreator] Runtime preview generated: {generated.Count} geometry item(s), " +
                      $"{implicitTriangles} implicit triangles.", this);
            if (!topology.IsWatertight)
            {
                Debug.LogWarning("[CreatureCreator] Runtime preview implicit mesh is not watertight.", this);
            }
        }

        private CreatureDefinition LoadDefinition()
        {
            if (definitionJson != null)
            {
                return new JsonDnaSerializer().Deserialize(definitionJson.text);
            }
            return CreateDemoDefinition();
        }

        private Mesh ResolveMeshAsset(string key)
        {
            CreatureMeshPalette palette = generationConfig != null ? generationConfig.MeshPalette : null;
            if (palette != null && palette.TryResolve(key, out Mesh mesh)) return mesh;
            return null;
        }

        private CreatureMaterialPalette ResolveMaterialPalette()
        {
            return generationConfig != null && generationConfig.MaterialPalette != null
                ? generationConfig.MaterialPalette
                : materialPalette;
        }

        private void CreateGeometryObject(int index, GeometryItem item)
        {
            var go = new GameObject($"{GeometryChildPrefix}{index}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.AddComponent<MeshFilter>().sharedMesh = item.Mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            AssignItemMaterials(renderer, item);
            go.AddComponent<MeshCollider>().sharedMesh = item.Mesh;
            _geometryObjects.Add(go);
        }

        /// <summary>
        /// CC-028: a mesh-asset item whose part carries a submaterial key resolves
        /// it through the shared palette. A set-but-unresolvable key logs a warning
        /// and falls back to the default preview material (the editor preview treats
        /// it as an error; Play Mode stays resilient). Items with no region keep the
        /// default material.
        /// </summary>
        private void AssignItemMaterials(MeshRenderer renderer, GeometryItem item)
        {
            if (item.MaterialRegions.Count == 0)
            {
                AssignFallbackMaterial(renderer);
                return;
            }

            Material resolved = null;
            try
            {
                resolved = MaterialResolver.Resolve(ResolveMaterialPalette(), item.MaterialRegions[0].MaterialKey);
            }
            catch (DomainException ex)
            {
                Debug.LogWarning(
                    $"[CreatureCreator] {ex.Message} Using the default preview material for item '{item.SourcePartId}'.",
                    this);
            }

            if (resolved == null)
            {
                AssignFallbackMaterial(renderer);
                return;
            }

            int subMeshCount = Mathf.Max(1, item.Mesh != null ? item.Mesh.subMeshCount : 1);
            var materials = new Material[subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = resolved;
            renderer.sharedMaterials = materials;
        }

        private void AssignFallbackMaterial(MeshRenderer renderer)
        {
            if (_previewMaterial == null) _previewMaterial = CreatePreviewMaterial();
            if (_previewMaterial != null) renderer.sharedMaterial = _previewMaterial;
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
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = new Vector3(0f, 0f, -1f),
                Radius = 1.1f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 2,
                Position = new Vector3(0f, 0f, 0f),
                Radius = 1.3f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 3,
                Position = new Vector3(0f, 0f, 1f),
                Radius = 1.0f,
            });
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
