using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Serialization;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// CC-031 pass 1: canonical JSON round-trip for a part's mesh-asset geometry
    /// source (additive "meshGeometry" field, null default, no schema version
    /// bump — pre-CC-031 files load unchanged).
    ///
    /// Runtime assembly — per project convention this fixture is NOT discovered by
    /// the MCP runner; invoke its methods directly via execute_code for evidence.
    /// </summary>
    [TestFixture]
    public class JsonDnaSerializerMeshGeometryTests
    {
        private static CreatureDefinition DefinitionWithMeshEye()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(new CreaturePart
            {
                Id = "eye",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Eye,
                Transform = new TransformData { Position = new Vector3(0f, 0.5f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MeshGeometry = new MeshGeometry
                {
                    MeshAssetKey = "eye_mesh",
                    Attachment = new GeometryAttachment { Offset = new Vector3(0f, 0.25f, 0f) },
                },
            });
            return definition;
        }

        [Test]
        public void Serialize_ThenDeserialize_MeshGeometryRoundTrips()
        {
            var serializer = new JsonDnaSerializer();
            string json = serializer.Serialize(DefinitionWithMeshEye());
            CreatureDefinition loaded = serializer.Deserialize(json);

            CreaturePart part = loaded.FindPart("eye");
            Assert.IsNotNull(part);
            Assert.IsNotNull(part.MeshGeometry, "meshGeometry must survive the round trip");
            Assert.AreEqual("eye_mesh", part.MeshGeometry.MeshAssetKey);
            AssertVectorClose(new Vector3(0f, 0.25f, 0f), part.MeshGeometry.Attachment.Offset, 1e-4f);
        }

        [Test]
        public void Serialize_SaveLoadSave_IsByteStable()
        {
            var serializer = new JsonDnaSerializer();
            string first = serializer.Serialize(DefinitionWithMeshEye());
            string second = serializer.Serialize(serializer.Deserialize(first));
            Assert.AreEqual(first, second, "canonical JSON must be byte-stable across save/load/save");
        }

        [Test]
        public void Serialize_PartWithoutMeshGeometry_EmitsNullAndLoadsNull()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(new CreaturePart
            {
                Id = "head",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });

            var serializer = new JsonDnaSerializer();
            string json = serializer.Serialize(definition);
            StringAssert.Contains("\"meshGeometry\":null", json);

            CreatureDefinition loaded = serializer.Deserialize(json);
            Assert.IsNull(loaded.FindPart("head").MeshGeometry,
                "a part without mesh geometry must load with a null source");
        }

        [Test]
        public void Canonicalize_QuantizesMeshGeometryAttachment()
        {
            CreatureDefinition definition = DefinitionWithMeshEye();
            definition.FindPart("eye").MeshGeometry.Attachment.Offset = new Vector3(0f, 0.123456f, 0f);

            CreatureDefinition canonical = DefinitionCanonicalizer.Canonicalize(definition);
            GeometryAttachment attachment = canonical.FindPart("eye").MeshGeometry.Attachment;

            Assert.AreEqual(0.1235f, attachment.Offset.y, 1e-4f,
                "canonicalization must quantize the attachment offset");
            Quaternion q = attachment.Orientation;
            float magnitude = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            Assert.IsTrue(Mathf.Abs(magnitude - 1f) < 1e-4f,
                "canonicalization must normalize the attachment orientation");
        }

        private static void AssertVectorClose(Vector3 expected, Vector3 actual, float tolerance, string message = "")
        {
            Assert.IsTrue(Vector3.Distance(expected, actual) <= tolerance,
                $"{message} Expected {expected}, got {actual}.");
        }
    }
}
