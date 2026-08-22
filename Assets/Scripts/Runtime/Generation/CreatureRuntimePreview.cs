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

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private Mesh _generatedMesh;

        private void Start()
        {
            if (generateOnStart) Generate();
        }

        [ContextMenu("Generate Creature")]
        public void Generate()
        {
            CreatureDefinition definition = LoadDefinition();
            var diagnostics = new GenerationDiagnostics();
            MeshTopologyReport topology;
            Mesh mesh = CreatureMeshGenerator.Generate(definition, out topology, diagnostics);

            EnsureComponents();
            DestroyGeneratedMesh();
            _generatedMesh = mesh;
            _meshFilter.sharedMesh = mesh;
            _meshCollider.sharedMesh = mesh;
            AssignPreviewMaterial();

            Debug.Log($"[CreatureCreator] Runtime preview generated: {mesh.triangles.Length / 3} triangles.", this);
            if (!topology.IsWatertight)
            {
                Debug.LogWarning("[CreatureCreator] Runtime preview mesh is not watertight.", this);
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

        private void EnsureComponents()
        {
            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();

            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            _meshCollider = GetComponent<MeshCollider>();
            if (_meshCollider == null) _meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        private void AssignPreviewMaterial()
        {
            if (_meshRenderer.sharedMaterial != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard")
                            ?? Shader.Find("Unlit/Color");
            if (shader != null) _meshRenderer.sharedMaterial = new Material(shader);
        }

        private void DestroyGeneratedMesh()
        {
            if (_generatedMesh == null) return;
            if (Application.isPlaying) Destroy(_generatedMesh);
            else DestroyImmediate(_generatedMesh);
            _generatedMesh = null;
        }

        private static CreatureDefinition CreateDemoDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 12f };
            definition.AddPart(new CreaturePart
            {
                Id = "runtime_body",
                PartType = PartType.Body,
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 1.6f, SmoothBlendRadius = 0.25f },
                Appearance = new AppearanceDefinition { BaseColor = new Color(0.22f, 0.58f, 0.78f, 1f), NoiseSeed = 7, NoiseScale = 1.5f },
            });
            definition.AddPart(new CreaturePart
            {
                Id = "runtime_head",
                ParentId = "runtime_body",
                PartType = PartType.Body,
                Transform = new TransformData { Position = new Vector3(0f, 1.45f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.85f, SmoothBlendRadius = 0.2f },
                Appearance = new AppearanceDefinition { BaseColor = new Color(0.3f, 0.72f, 0.86f, 1f), NoiseSeed = 11, NoiseScale = 1.5f },
            });
            return definition;
        }
    }
}
