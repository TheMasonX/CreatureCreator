using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// CC-031 pass 1: multi-item generator output. A creature with only implicit
    /// geometry yields a single implicit item; adding a mesh-asset part produces a
    /// second item placed at the part's local-space position (attachment offset
    /// applied in the part's local frame). Also covers deterministic ordering,
    /// mirroring, the mesh-resolver contract, and CreaturePart.Clone propagation of
    /// the new MeshGeometry field.
    ///
    /// Runtime assembly — per project convention this fixture is NOT discovered by
    /// the MCP runner; invoke its methods directly via execute_code for evidence.
    /// </summary>
    [TestFixture]
    public class GeneratedCreatureTests
    {
        private static CreatureDefinition DefinitionWithBody()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 8f };
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.9f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 1.0f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            return definition;
        }

        private static CreaturePart MeshEyePart(string id, Vector3 position, MeshGeometry geometry)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Eye,
                Transform = new TransformData { Position = position, Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MeshGeometry = geometry,
            };
        }

        private static MeshGeometry EyeGeometry(string key, Vector3 offset)
        {
            return new MeshGeometry
            {
                MeshAssetKey = key,
                Attachment = new GeometryAttachment { Offset = offset },
            };
        }

        /// <summary>A unit cube centred at the origin (half-size 0.5).</summary>
        private static Mesh UnitCube()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                3, 7, 6, 3, 6, 2,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        [Test]
        public void Generate_BodyOnly_ProducesSingleImplicitItem()
        {
            GeneratedCreature generated = CreatureMeshGenerator.Generate(DefinitionWithBody(), out _);

            Assert.AreEqual(1, generated.Count);
            Assert.AreEqual(GeneratedCreature.ImplicitSurfaceSourceId, generated.Geometry[0].SourcePartId);
            Assert.AreEqual(GeometryType.Implicit, generated.Geometry[0].GeometryType);
            Assert.IsNotNull(generated.Geometry[0].Mesh);
            Assert.IsNotNull(generated.MainMesh);
        }

        [Test]
        public void Generate_MeshAssetPart_ProducesImplicitAndMeshItems()
        {
            CreatureDefinition definition = DefinitionWithBody();
            definition.AddPart(MeshEyePart("eye_L", new Vector3(0.5f, 0.5f, 0f), EyeGeometry("eye", Vector3.zero)));

            GeneratedCreature generated = GenerateWithResolver(definition, _ => UnitCube());

            Assert.AreEqual(2, generated.Count, "implicit surface + one mesh-asset part");
            Assert.AreEqual(GeometryType.Implicit, generated.Geometry[0].GeometryType);
            Assert.AreEqual(GeometryType.MeshAsset, generated.Geometry[1].GeometryType);
            Assert.AreEqual("eye_L", generated.Geometry[1].SourcePartId);
            Assert.AreEqual("eye_L", generated.Geometry[1].RigBinding.SourcePartId);
                Assert.IsNotNull(generated.Geometry[1].SourceMesh);
                Assert.IsFalse(generated.Geometry[1].RigBinding.IsMirrored);
        }

        [Test]
        public void Generate_MeshAssetPart_PlacesMeshAtPartLocalPosition()
        {
            CreatureDefinition definition = DefinitionWithBody();
            Vector3 partPosition = new Vector3(0.5f, 1f, 0f);
            definition.AddPart(MeshEyePart("eye", partPosition, EyeGeometry("eye", Vector3.zero)));

            GeneratedCreature generated = GenerateWithResolver(definition, _ => UnitCube());

            Assert.AreEqual(2, generated.Count);
            AssertVectorClose(partPosition, generated.Geometry[1].Mesh.bounds.center, 0.001f,
                "the mesh asset should sit at the part's local-space position");
        }

        [Test]
        public void Generate_MeshAssetAttachmentOffset_AppliesLocalOffset()
        {
            CreatureDefinition definition = DefinitionWithBody();
            Vector3 partPosition = new Vector3(0f, 0.5f, 0f);
            definition.AddPart(MeshEyePart("eye", partPosition, EyeGeometry("eye", new Vector3(0f, 0.2f, 0f))));

            GeneratedCreature generated = GenerateWithResolver(definition, _ => UnitCube());

            Assert.AreEqual(2, generated.Count);
            AssertVectorClose(partPosition + new Vector3(0f, 0.2f, 0f), generated.Geometry[1].Mesh.bounds.center, 0.001f,
                "the attachment offset should translate the mesh within the part's local frame");
        }

        [Test]
        public void Generate_MeshParts_OrderedBySourcePartId_AndDeterministic()
        {
            CreatureDefinition definition = DefinitionWithBody();
            definition.AddPart(MeshEyePart("eye_z", new Vector3(0f, 0.5f, 1.5f), EyeGeometry("eye", Vector3.zero)));
            definition.AddPart(MeshEyePart("eye_a", new Vector3(0f, 0.5f, -1.5f), EyeGeometry("eye", Vector3.zero)));

            GeneratedCreature first = GenerateWithResolver(definition, _ => UnitCube());
            GeneratedCreature second = GenerateWithResolver(definition, _ => UnitCube());

            Assert.AreEqual(3, first.Count, "implicit + two mesh parts");
            Assert.AreEqual("eye_a", first.Geometry[1].SourcePartId);
            Assert.AreEqual("eye_z", first.Geometry[2].SourcePartId);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first.Geometry[i].SourcePartId, second.Geometry[i].SourcePartId);
                AssertVectorClose(first.Geometry[i].Mesh.bounds.center, second.Geometry[i].Mesh.bounds.center, 0.001f,
                    $"item {i} must be deterministic");
            }
        }

        [Test]
        public void Generate_MirroredMeshPart_EmitsMirroredCopy()
        {
            CreatureDefinition definition = DefinitionWithBody();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            CreaturePart eye = MeshEyePart("eye", new Vector3(0.5f, 0.5f, 0f), EyeGeometry("eye", Vector3.zero));
            eye.MirrorAcrossSymmetryPlane = true;
            definition.AddPart(eye);

            GeneratedCreature generated = GenerateWithResolver(definition, _ => UnitCube());

            Assert.AreEqual(3, generated.Count, "implicit + original + mirrored");
            Assert.AreEqual("eye", generated.Geometry[1].SourcePartId);
            Assert.AreEqual("eye" + GeneratedCreature.MirrorSuffix, generated.Geometry[2].SourcePartId);
            AssertVectorClose(new Vector3(0.5f, 0.5f, 0f), generated.Geometry[1].Mesh.bounds.center, 0.001f, "original copy");
            AssertVectorClose(new Vector3(-0.5f, 0.5f, 0f), generated.Geometry[2].Mesh.bounds.center, 0.001f, "mirrored copy");
                Assert.IsTrue(generated.Geometry[2].RigBinding.IsMirrored);
                Assert.AreEqual(generated.Geometry[1].SourceMesh, generated.Geometry[2].SourceMesh);
                Assert.AreEqual(new Vector3(-0.5f, 0.5f, 0f),
                    generated.Geometry[2].RestPlacement.MultiplyPoint3x4(Vector3.zero));
        }

        [Test]
        public void Generate_MirroredMeshPart_PreservesOutwardWinding()
        {
            CreatureDefinition definition = DefinitionWithBody();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            CreaturePart eye = MeshEyePart("eye", new Vector3(0.5f, 0.5f, 0f), EyeGeometry("eye", Vector3.zero));
            eye.MirrorAcrossSymmetryPlane = true;
            definition.AddPart(eye);

            GeneratedCreature generated = GenerateWithResolver(definition, _ => UnitCube());
            Mesh original = generated.Geometry[1].Mesh;
            Mesh mirrored = generated.Geometry[2].Mesh;
            Vector3 originalNormal = Vector3.Cross(
                original.vertices[original.triangles[1]] - original.vertices[original.triangles[0]],
                original.vertices[original.triangles[2]] - original.vertices[original.triangles[0]]).normalized;
            Vector3 mirroredNormal = Vector3.Cross(
                mirrored.vertices[mirrored.triangles[1]] - mirrored.vertices[mirrored.triangles[0]],
                mirrored.vertices[mirrored.triangles[2]] - mirrored.vertices[mirrored.triangles[0]]).normalized;

            Assert.Greater(Vector3.Dot(originalNormal, original.vertices[original.triangles[0]] - original.bounds.center), 0f);
            Assert.Greater(Vector3.Dot(mirroredNormal, mirrored.vertices[mirrored.triangles[0]] - mirrored.bounds.center), 0f);
        }

        [Test]
        public void Generate_MeshAssetPart_BakesAuthoredVertexColors()
        {
            // CC-031 pass 2: a mesh-asset item must carry the part's OWN authored
            // appearance as vertex colors (like the implicit surface does), so a
            // non-white authored color reaches the mesh rather than staying white.
            CreatureDefinition definition = DefinitionWithBody();
            CreaturePart eye = MeshEyePart("eye", new Vector3(0f, 0.5f, 0f), EyeGeometry("eye", Vector3.zero));
            eye.Appearance = new AppearanceDefinition { BaseColor = new Color(1f, 0f, 0f), NoiseSeed = 0, NoiseScale = 1f };
            definition.AddPart(eye);

            GeneratedCreature generated = GenerateWithResolver(definition, _ => UnitCube());
            Mesh mesh = generated.Geometry[1].Mesh;
            Color[] colors = mesh.colors;

            Assert.AreEqual(mesh.vertexCount, colors.Length, "every mesh-asset vertex must carry a color");
            Assert.Greater(colors.Length, 0);
            foreach (Color c in colors)
            {
                Assert.GreaterOrEqual(c.r, 0.84f, "red channel stays near the authored base within the brightness band");
                Assert.LessOrEqual(c.r, 1.16f);
                Assert.AreEqual(0f, c.g, "a pure-red authored color must not gain green");
                Assert.AreEqual(0f, c.b, "a pure-red authored color must not gain blue");
            }
        }

        [Test]
        public void Generate_MirroredMeshPart_BakesVertexColorsOnBothCopies()
        {
            CreatureDefinition definition = DefinitionWithBody();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            CreaturePart eye = MeshEyePart("eye", new Vector3(0.5f, 0.5f, 0f), EyeGeometry("eye", Vector3.zero));
            eye.MirrorAcrossSymmetryPlane = true;
            eye.Appearance = new AppearanceDefinition { BaseColor = new Color(1f, 0f, 0f), NoiseSeed = 0, NoiseScale = 1f };
            definition.AddPart(eye);

            GeneratedCreature generated = GenerateWithResolver(definition, _ => UnitCube());

            Assert.AreEqual(3, generated.Count, "implicit + original + mirrored");
            AssertColorsBaked(generated.Geometry[1].Mesh, "original copy");
            AssertColorsBaked(generated.Geometry[2].Mesh, "mirrored copy");
        }

        private static void AssertColorsBaked(Mesh mesh, string message)
        {
            Color[] colors = mesh.colors;
            Assert.AreEqual(mesh.vertexCount, colors.Length, $"{message}: every vertex must carry a color");
            Assert.Greater(colors.Length, 0);
            foreach (Color c in colors)
            {
                Assert.AreEqual(0f, c.g, $"{message}: a pure-red authored color must not gain green");
                Assert.AreEqual(0f, c.b, $"{message}: a pure-red authored color must not gain blue");
                Assert.GreaterOrEqual(c.r, 0.84f);
            }
        }

        [Test]
        public void Generate_MeshPart_WithoutResolver_ThrowsDomainException()
        {
            CreatureDefinition definition = DefinitionWithBody();
            definition.AddPart(MeshEyePart("eye", new Vector3(0f, 0.5f, 0f), EyeGeometry("eye", Vector3.zero)));

            Assert.Throws<DomainException>(() => CreatureMeshGenerator.Generate(definition, out _));
        }

        [Test]
        public void Generate_MeshPart_WithUnresolvableKey_ThrowsDomainException()
        {
            CreatureDefinition definition = DefinitionWithBody();
            definition.AddPart(MeshEyePart("eye", new Vector3(0f, 0.5f, 0f), EyeGeometry("eye", Vector3.zero)));

            Assert.Throws<DomainException>(() =>
                GenerateWithResolver(definition, _ => null));
        }

        [Test]
        public void Generate_MeshPart_WithMaterialKey_PopulatesMaterialRegions()
        {
            // CC-028: a mesh-asset part carrying a submaterial key surfaces it as a
            // MaterialRegion on its geometry item (key only — resolution to a
            // UnityEngine.Material is render-layer), including the mirrored copy.
            CreatureDefinition definition = DefinitionWithBody();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            CreaturePart eye = MeshEyePart("eye", new Vector3(0.5f, 0.5f, 0f), EyeGeometry("eye", Vector3.zero));
            eye.MirrorAcrossSymmetryPlane = true;
            eye.Appearance = new AppearanceDefinition { BaseColor = Color.white, NoiseSeed = 0, NoiseScale = 1f, MaterialKey = "eye_white" };
            definition.AddPart(eye);

            GeneratedCreature generated = GenerateWithResolver(definition, _ => UnitCube());

            Assert.AreEqual(3, generated.Count, "implicit + original + mirrored");
            AssertMaterialRegion(generated.Geometry[1], "eye_white", "original copy");
            AssertMaterialRegion(generated.Geometry[2], "eye_white", "mirrored copy");
            Assert.AreEqual(0, generated.Geometry[0].MaterialRegions.Count,
                "the implicit combined item keeps the vertex-color default path (no material regions)");
        }

        [Test]
        public void Generate_MeshPart_WithoutMaterialKey_HasNoMaterialRegions()
        {
            CreatureDefinition definition = DefinitionWithBody();
            definition.AddPart(MeshEyePart("eye", new Vector3(0f, 0.5f, 0f), EyeGeometry("eye", Vector3.zero)));

            GeneratedCreature generated = GenerateWithResolver(definition, _ => UnitCube());

            Assert.AreEqual(0, generated.Geometry[1].MaterialRegions.Count,
                "a part without a submaterial override keeps the nearest-part appearance path");
        }

        private static void AssertMaterialRegion(GeometryItem item, string expectedKey, string message)
        {
            Assert.AreEqual(1, item.MaterialRegions.Count, $"{message}: one region per mesh-asset item");
            Assert.AreEqual(expectedKey, item.MaterialRegions[0].MaterialKey, $"{message}: region carries the part's key");
            Assert.AreEqual(0, item.MaterialRegions[0].StartIndex, $"{message}: region starts at the first index");
            Assert.Greater(item.MaterialRegions[0].IndexCount, 0, $"{message}: region covers the item's indices");
        }

        [Test]
        public void CreaturePart_Clone_CopiesMeshGeometryIndependently()
        {
            CreaturePart part = MeshEyePart("eye", new Vector3(0f, 0.5f, 0f), EyeGeometry("eye", new Vector3(0f, 0.1f, 0f)));

            CreaturePart clone = part.Clone();

            Assert.IsNotNull(clone.MeshGeometry);
            Assert.AreEqual("eye", clone.MeshGeometry.MeshAssetKey);
            Assert.AreEqual(part.MeshGeometry.Attachment.Offset, clone.MeshGeometry.Attachment.Offset);

            clone.MeshGeometry.MeshAssetKey = "other";
            Assert.AreEqual("eye", part.MeshGeometry.MeshAssetKey, "the clone must be independent of the source");
        }

        private static GeneratedCreature GenerateWithResolver(CreatureDefinition definition, System.Func<string, Mesh> resolver)
        {
            return CreatureMeshGenerator.Generate(definition, out _, null, resolver);
        }

        private static void AssertVectorClose(Vector3 expected, Vector3 actual, float tolerance, string message)
        {
            Assert.IsTrue(Vector3.Distance(expected, actual) <= tolerance,
                $"{message}. Expected {expected}, got {actual}.");
        }
    }
}
