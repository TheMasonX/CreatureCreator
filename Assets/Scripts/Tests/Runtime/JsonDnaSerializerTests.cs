using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Serialization;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class JsonDnaSerializerTests
    {
        private JsonDnaSerializer _serializer;

        [SetUp]
        public void SetUp()
        {
            _serializer = new JsonDnaSerializer();
        }

        private static CreatureDefinition MakeTwoPartDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.AddPart(new CreaturePart
            {
                Id = "part_body",
                ParentId = null,
                PartType = PartType.Body,
                Transform = new TransformData
                {
                    Position = Vector3.zero,
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_leg",
                ParentId = "part_body",
                PartType = PartType.Leg,
                Transform = new TransformData
                {
                    Position = new Vector3(0.5f, -1f, 0f),
                    Rotation = Quaternion.identity,
                    Scale = new Vector3(0.3f, 0.3f, 0.3f),
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
            });
            return definition;
        }

        [Test]
        public void RoundTrip_ReconstructsEquivalentDefinition()
        {
            CreatureDefinition original = MakeTwoPartDefinition();

            string json = _serializer.Serialize(original);
            CreatureDefinition reconstructed = _serializer.Deserialize(json);

            Assert.AreEqual(original.Parts.Count, reconstructed.Parts.Count);
            Assert.AreEqual(original.SymmetryMode, reconstructed.SymmetryMode);

            CreaturePart originalLeg = original.FindPart("part_leg");
            CreaturePart reconstructedLeg = reconstructed.FindPart("part_leg");
            Assert.IsNotNull(reconstructedLeg);
            Assert.AreEqual(originalLeg.ParentId, reconstructedLeg.ParentId);
            Assert.AreEqual(originalLeg.MirrorAcrossSymmetryPlane, reconstructedLeg.MirrorAcrossSymmetryPlane);
            Assert.AreEqual(originalLeg.Transform.Position.x, reconstructedLeg.Transform.Position.x, 1e-4f);
        }

        [Test]
        public void Serialize_IsByteStableAcrossRepeatedCalls()
        {
            CreatureDefinition definition = MakeTwoPartDefinition();

            string first = _serializer.Serialize(definition);
            string second = _serializer.Serialize(definition);

            Assert.AreEqual(first, second,
                "Serializing the same definition twice must produce identical text " +
                "(Sprint 1.3 exit gate: byte-stable canonical JSON).");
        }

        [Test]
        public void Serialize_IsStableAcrossPartInsertionOrder()
        {
            CreatureDefinition definitionA = MakeTwoPartDefinition();

            CreatureDefinition definitionB = CreatureDefinition.CreateEmpty();
            definitionB.SymmetryMode = definitionA.SymmetryMode;
            definitionB.AddPart(definitionA.Parts[1].Clone()); // leg first
            definitionB.AddPart(definitionA.Parts[0].Clone()); // body second

            string jsonA = _serializer.Serialize(definitionA);
            string jsonB = _serializer.Serialize(definitionB);

            Assert.AreEqual(jsonA, jsonB,
                "Canonical output must not depend on Parts list insertion order (§13.4).");
        }

        [Test]
        public void SaveLoadSave_ProducesByteStableJson()
        {
            CreatureDefinition original = MakeTwoPartDefinition();

            string firstSave = _serializer.Serialize(original);
            CreatureDefinition loaded = _serializer.Deserialize(firstSave);
            string secondSave = _serializer.Serialize(loaded);

            Assert.AreEqual(firstSave, secondSave,
                "Save -> load -> canonical-save must be byte-stable (Sprint 1.3 exit gate).");
        }

        [Test]
        public void Deserialize_ThrowsOnMalformedJson()
        {
            Assert.Throws<DnaDeserializationException>(() => _serializer.Deserialize("{ not valid json"));
        }

        [Test]
        public void Deserialize_ThrowsOnMissingRequiredField()
        {
            const string json = "{\"schemaVersion\":1,\"symmetryMode\":\"None\"}"; // missing bounds/generation/parts
            Assert.Throws<DnaDeserializationException>(() => _serializer.Deserialize(json));
        }

        [Test]
        public void Deserialize_PreservesNullParentId()
        {
            CreatureDefinition original = MakeTwoPartDefinition();
            string json = _serializer.Serialize(original);

            CreatureDefinition reconstructed = _serializer.Deserialize(json);

            Assert.IsNull(reconstructed.FindPart("part_body").ParentId);
        }
    }
}
