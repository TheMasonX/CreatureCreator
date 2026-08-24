using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Serialization;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// CC-028: canonical JSON round-trip for a part's optional submaterial key
    /// (additive "materialKey" field on "appearance", null default, no schema
    /// version bump — pre-CC-028 files load unchanged). Keys serialize by stable
    /// name, never a UnityEngine.Object reference.
    ///
    /// Runtime assembly — per project convention this fixture is NOT discovered by
    /// the MCP runner; invoke its methods directly via execute_code for evidence.
    /// </summary>
    [TestFixture]
    public class JsonDnaSerializerMaterialKeyTests
    {
        private static CreatureDefinition DefinitionWithMaterialEye()
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
                Appearance = new AppearanceDefinition
                {
                    BaseColor = new Color(1f, 1f, 1f, 1f),
                    NoiseSeed = 0,
                    NoiseScale = 1f,
                    MaterialKey = "eye_white",
                },
            });
            return definition;
        }

        [Test]
        public void Serialize_ThenDeserialize_MaterialKeyRoundTrips()
        {
            var serializer = new JsonDnaSerializer();
            string json = serializer.Serialize(DefinitionWithMaterialEye());
            CreatureDefinition loaded = serializer.Deserialize(json);

            CreaturePart part = loaded.FindPart("eye");
            Assert.IsNotNull(part);
            Assert.AreEqual("eye_white", part.Appearance.MaterialKey, "materialKey must survive the round trip");
        }

        [Test]
        public void Serialize_EmitsMaterialKeyByNameNotObject()
        {
            var serializer = new JsonDnaSerializer();
            string json = serializer.Serialize(DefinitionWithMaterialEye());

            StringAssert.Contains("\"materialKey\":\"eye_white\"", json);
            StringAssert.DoesNotContain("UnityEngine.Material", json);
        }

        [Test]
        public void Serialize_PartWithoutMaterialKey_EmitsNullAndLoadsNull()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
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
            StringAssert.Contains("\"materialKey\":null", json);

            CreatureDefinition loaded = serializer.Deserialize(json);
            Assert.IsNull(loaded.FindPart("head").Appearance.MaterialKey,
                "a part without a material override must load with a null key");
        }

        [Test]
        public void Serialize_SaveLoadSave_IsByteStable()
        {
            var serializer = new JsonDnaSerializer();
            string first = serializer.Serialize(DefinitionWithMaterialEye());
            string second = serializer.Serialize(serializer.Deserialize(first));
            Assert.AreEqual(first, second, "canonical JSON must be byte-stable across save/load/save");
        }

        [Test]
        public void Deserialize_PreCc028FileWithoutField_LoadsNull()
        {
            // A v2 file authored before CC-028 has no "materialKey" inside
            // "appearance". Serialize a valid definition, strip the additive field
            // (the writer emits it immediately after noiseScale), then load the
            // resulting pre-CC-028 JSON.
            var serializer = new JsonDnaSerializer();
            string withKey = serializer.Serialize(DefinitionWithMaterialEye());
            string preCc028Json = withKey.Replace(",\"materialKey\":\"eye_white\"", string.Empty);

            CreatureDefinition loaded = serializer.Deserialize(preCc028Json);

            Assert.IsNull(loaded.FindPart("eye").Appearance.MaterialKey,
                "a pre-CC-028 file must load with a null material key");
        }
    }
}
