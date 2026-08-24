using NUnit.Framework;
using ProceduralCreature.Appearance;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;
using UnityEditor;
using UnityEngine;

namespace ProceduralCreature.Tests.Editor
{
    [TestFixture]
    public sealed class CreatureGenerationConfigTests
    {
        private const string ConfigAssetPath = "Assets/Prefabs/CreatureGenerationConfig.asset";

        [Test]
        public void ConfigReferencesSharedPaletteTypes()
        {
            CreatureGenerationConfig config = ScriptableObject.CreateInstance<CreatureGenerationConfig>();
            CreatureMeshPalette meshPalette = ScriptableObject.CreateInstance<CreatureMeshPalette>();
            CreatureMaterialPalette materialPalette = ScriptableObject.CreateInstance<CreatureMaterialPalette>();

            try
            {
                SerializedObjectUtility.SetPrivateField(config, "meshPalette", meshPalette);
                SerializedObjectUtility.SetPrivateField(config, "materialPalette", materialPalette);

                Assert.AreSame(meshPalette, config.MeshPalette);
                Assert.AreSame(materialPalette, config.MaterialPalette);
                Assert.Greater(config.DefaultVoxelsPerUnit, 0f);
            }
            finally
            {
                Object.DestroyImmediate(materialPalette);
                Object.DestroyImmediate(meshPalette);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void SharedConfigAsset_ResolvesBothProjectPalettes()
        {
            // The concrete shared asset must actually reference the project's
            // palette assets. This regression guards the null-reference defect the
            // first audit missed: the config existed but carried no palette refs.
            CreatureGenerationConfig config =
                AssetDatabase.LoadAssetAtPath<CreatureGenerationConfig>(ConfigAssetPath);

            Assert.IsNotNull(config, $"Shared config asset must exist at {ConfigAssetPath}.");
            Assert.IsNotNull(config.MeshPalette, "Shared config mesh palette must be assigned.");
            Assert.IsNotNull(config.MaterialPalette, "Shared config material palette must be assigned.");
            Assert.Greater(config.DefaultVoxelsPerUnit, 0f);
            Assert.IsTrue(config.UsePortableSampling);
        }

        [Test]
        public void SharedConfigAsset_MaterialPalette_ResolvesDefaultMaterial()
        {
            // CC-074: the concrete shared material palette must name a default
            // surface material (the Body material) so the editor preview and the
            // runtime preview both render surfaces that have no explicit region.
            CreatureGenerationConfig config =
                AssetDatabase.LoadAssetAtPath<CreatureGenerationConfig>(ConfigAssetPath);
            Assert.IsNotNull(config);
            Assert.IsNotNull(config.MaterialPalette);

            Material material = MaterialResolver.ResolveDefault(config.MaterialPalette);
            Assert.IsNotNull(material, "Material palette must resolve a default surface material.");
        }

        [Test]
        public void SharedConfigAsset_MeshPalette_ResolvesProjectKeys()
        {
            CreatureGenerationConfig config =
                AssetDatabase.LoadAssetAtPath<CreatureGenerationConfig>(ConfigAssetPath);
            Assert.IsNotNull(config);
            Assert.IsNotNull(config.MeshPalette);

            Assert.IsTrue(config.MeshPalette.TryResolve("Sphere", out Mesh sphere), "Sphere key must resolve.");
            Assert.IsNotNull(sphere);
            Assert.IsTrue(config.MeshPalette.TryResolve("Cylinder", out Mesh cylinder), "Cylinder key must resolve.");
            Assert.IsNotNull(cylinder);
            Assert.IsFalse(config.MeshPalette.HasDuplicateKeys(out _));
        }

        [Test]
        public void SharedConfigDerivedResolver_GeneratesDeterministicMirroredOutput()
        {
            // CC-072 parity: the editor and the runtime preview both resolve mesh
            // keys through the shared config's palette. Build a resolver with the
            // same semantics as CreatureRuntimePreview.ResolveMeshAsset and run the
            // shared generator; the output must be deterministic, mirrored copies
            // must share the same source mesh, and the implicit surface must be
            // watertight.
            CreatureGenerationConfig config =
                AssetDatabase.LoadAssetAtPath<CreatureGenerationConfig>(ConfigAssetPath);
            Assert.IsNotNull(config);
            Assert.IsNotNull(config.MeshPalette);

            CreatureDefinition definition = DefinitionWithMirroredSphereEye();
            System.Func<string, Mesh> resolver =
                key => config.MeshPalette.TryResolve(key, out Mesh mesh) ? mesh : null;

            MeshTopologyReport topology;
            GeneratedCreature first = CreatureMeshGenerator.Generate(
                definition, out topology, diagnostics: null, usePortableSampling: true, meshResolver: resolver,
                cullingMode: SdfCullingMode.Exact);

            Assert.IsTrue(topology.IsWatertight, "Implicit surface must stay watertight with a mesh part present.");
            Assert.AreEqual(3, first.Count, "implicit + original + mirrored mesh item");
            Assert.AreEqual(GeometryType.Implicit, first.Geometry[0].GeometryType);
            Assert.AreEqual("eye", first.Geometry[1].SourcePartId);
            Assert.AreEqual("eye" + GeneratedCreature.MirrorSuffix, first.Geometry[2].SourcePartId);

            // Both copies resolve from the same palette asset: single asset identity.
            Assert.AreSame(first.Geometry[1].SourceMesh, first.Geometry[2].SourceMesh);
            Assert.IsFalse(first.Geometry[1].RigBinding.IsMirrored);
            Assert.IsTrue(first.Geometry[2].RigBinding.IsMirrored);

            MeshTopologyReport secondTopology;
            GeneratedCreature second = CreatureMeshGenerator.Generate(
                definition, out secondTopology, diagnostics: null, usePortableSampling: true, meshResolver: resolver,
                cullingMode: SdfCullingMode.Exact);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first.Geometry[i].SourcePartId, second.Geometry[i].SourcePartId);
                AssertVectorClose(first.Geometry[i].Mesh.bounds.center, second.Geometry[i].Mesh.bounds.center, 0.001f,
                    $"item {i} must be deterministic");
            }
        }

        private static CreatureDefinition DefinitionWithMirroredSphereEye()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 8f };
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.9f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 1.0f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;

            definition.AddPart(new CreaturePart
            {
                Id = "eye",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Eye,
                Transform = new TransformData
                {
                    Position = new Vector3(0.5f, 0.5f, 0f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
                MeshGeometry = new MeshGeometry { MeshAssetKey = "Sphere" },
            });
            return definition;
        }

        private static void AssertVectorClose(Vector3 actual, Vector3 expected, float tolerance, string message)
        {
            Assert.Less(Vector3.Distance(actual, expected), tolerance, message);
        }
    }

    internal static class SerializedObjectUtility
    {
        public static void SetPrivateField(Object target, string fieldName, Object value)
        {
            var serialized = new UnityEditor.SerializedObject(target);
            serialized.FindProperty(fieldName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
