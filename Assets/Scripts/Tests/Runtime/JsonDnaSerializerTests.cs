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
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(new CreaturePart
            {
                Id = "part_leg",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Leg,
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
                Id = "part_foot",
                ParentId = "part_leg",
                PartType = PartType.Foot,
                Transform = new TransformData
                {
                    Position = new Vector3(0f, -0.5f, 0f),
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
            Assert.AreEqual(originalLeg.DisplayName, reconstructedLeg.DisplayName);
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
        public void RoundTrip_PreservesPartAndEyePartTypes()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            definition.AddPart(new CreaturePart
            {
                Id = "part_generic",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Part,
                DisplayName = "Part",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_eye",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Eye,
                DisplayName = "Eye",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });

            string json = _serializer.Serialize(definition);
            CreatureDefinition reconstructed = _serializer.Deserialize(json);

            Assert.AreEqual(PartType.Part, reconstructed.FindPart("part_generic").PartType);
            Assert.AreEqual(PartType.Eye, reconstructed.FindPart("part_eye").PartType);
            Assert.AreEqual("Part", reconstructed.FindPart("part_generic").DisplayName);
            Assert.AreEqual("Eye", reconstructed.FindPart("part_eye").DisplayName);
        }

        [Test]
        public void Serialize_IsStableAcrossPartInsertionOrder()
        {
            CreatureDefinition definitionA = MakeTwoPartDefinition();

            CreatureDefinition definitionB = CreatureDefinition.CreateEmpty();
            definitionB.SymmetryMode = definitionA.SymmetryMode;
            definitionB.Forward = definitionA.Forward;
            definitionB.Body = definitionA.Body.Clone();
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
            const string json = "{\"schemaVersion\":2,\"symmetryMode\":\"None\"}"; // missing bounds/generation/body/parts
            Assert.Throws<DnaDeserializationException>(() => _serializer.Deserialize(json));
        }

        [Test]
        public void Deserialize_RejectsV1Explicitly()
        {
            const string json = "{\"schemaVersion\":1}";
            DnaDeserializationException exception = Assert.Throws<DnaDeserializationException>(
                () => _serializer.Deserialize(json));
            StringAssert.Contains("Schema version 1 is unsupported", exception.Message);
        }

        [Test]
        public void Deserialize_PreservesBodySplineAndForward()
        {
            CreatureDefinition original = MakeTwoPartDefinition();
            string json = _serializer.Serialize(original);

            CreatureDefinition reconstructed = _serializer.Deserialize(json);

            Assert.AreEqual(2, reconstructed.Body.Samples.Count);
            Assert.AreEqual(1u, reconstructed.Body.Samples[0].Id);
            Assert.AreEqual(0.75f, reconstructed.Body.Samples[0].Radius, 1e-4f);
            Assert.AreEqual(2u, reconstructed.Body.Samples[1].Id);
            Assert.AreEqual(Vector3.forward, reconstructed.Forward);
        }
    }
}
