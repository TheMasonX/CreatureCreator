using System.Collections.Generic;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Serialization;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    public sealed class CreatureRuntimePreview : MonoBehaviour
    {
        [SerializeField] private TextAsset definitionJson;
        [SerializeField] private bool generateOnStart = true;

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
            CreatureDefinition definition = LoadDefinition();
            var diagnostics = new GenerationDiagnostics(collectTimings: false);
            MeshTopologyReport topology;
            GeneratedCreature generated = CreatureMeshGenerator.Generate(definition, out topology, diagnostics);

            DestroyGeneratedGeometry();

            for (int i = 0; i < generated.Geometry.Count; i++)
            {
                CreateGeometryObject(i, generated.Geometry[i].Mesh);
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

        private void CreateGeometryObject(int index, Mesh mesh)
        {
            var go = new GameObject($"{GeometryChildPrefix}{index}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            if (_previewMaterial == null) _previewMaterial = CreatePreviewMaterial();
            if (_previewMaterial != null) renderer.sharedMaterial = _previewMaterial;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            _geometryObjects.Add(go);
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
