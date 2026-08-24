using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Serialization;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Phase 3 (CC-018) limb chain serialization + canonicalization. Runtime
    /// assembly — invoke via execute_code, not the MCP runner.
    /// </summary>
    [TestFixture]
    public class JsonDnaSerializerLimbTests
    {
        private JsonDnaSerializer _serializer;

        [SetUp]
        public void SetUp()
        {
            _serializer = new JsonDnaSerializer();
        }

        private static LimbChain LimbChain()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });
            return chain;
        }

        private static CreatureDefinition DefinitionWithLimb()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(new CreaturePart
            {
                Id = "part_leg",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = LimbChain(),
            });
            return definition;
        }

        private static CreatureDefinition DefinitionWithoutLimb()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(new CreaturePart
            {
                Id = "part_leg",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });
            return definition;
        }

        [Test]
        public void RoundTrip_LimbChain_IsByteIdentical()
        {
            CreatureDefinition original = DefinitionWithLimb();

            string first = _serializer.Serialize(original);
            CreatureDefinition loaded = _serializer.Deserialize(first);
            string second = _serializer.Serialize(loaded);

            Assert.AreEqual(first, second,
                "Save -> load -> canonical-save of a limb chain must be byte-identical.");

            LimbChain limb = loaded.FindPart("part_leg").Limb;
            Assert.IsNotNull(limb);
            Assert.AreEqual(2, limb.Joints.Count);
            Assert.AreEqual(1u, limb.Joints[0].Id);
            Assert.AreEqual(new Vector3(0f, -1f, 0f), limb.Joints[1].Position);
            Assert.IsTrue(limb.Thickness.ContentEquals(LimbChain().Thickness),
                "The default thickness profile must round-trip.");
        }

        [Test]
        public void RoundTrip_NonLimbPart_SerializesNullLimbChain()
        {
            string json = _serializer.Serialize(DefinitionWithoutLimb());
            Assert.IsTrue(json.Contains("\"limbChain\":null"),
                "A non-limb part must write an explicit null limbChain for byte stability.");

            CreatureDefinition loaded = _serializer.Deserialize(json);
            Assert.IsNull(loaded.FindPart("part_leg").Limb);
        }

        [Test]
        public void Deserialize_PreCC018FileWithoutLimbChainField_LoadsNull()
        {
            // Simulate a v2 file saved before CC-018: no limbChain key at all.
            string withNull = _serializer.Serialize(DefinitionWithoutLimb());
            string legacy = withNull.Replace(",\"limbChain\":null", string.Empty);
            Assert.IsFalse(legacy.Contains("limbChain"), "Sanity: the legacy file must not mention limbChain.");

            CreatureDefinition loaded = _serializer.Deserialize(legacy);
            Assert.IsNull(loaded.FindPart("part_leg").Limb,
                "A pre-CC-018 file must load with a null limb (additive optional field).");
        }

        [Test]
        public void RoundTrip_ThicknessProfile_WithCustomKeys()
        {
            var definition = DefinitionWithLimb();
            var profile = new ThicknessProfile();
            profile.Keys.Add(new ThicknessKey { T = 0f, Value = 0.4f });
            profile.Keys.Add(new ThicknessKey { T = 0.5f, Value = 0.2f });
            profile.Keys.Add(new ThicknessKey { T = 1f, Value = 0.1f });
            definition.FindPart("part_leg").Limb.Thickness = profile;

            string json = _serializer.Serialize(definition);
            CreatureDefinition loaded = _serializer.Deserialize(json);

            ThicknessProfile restored = loaded.FindPart("part_leg").Limb.Thickness;
            Assert.AreEqual(3, restored.Keys.Count);
            Assert.AreEqual(0.5f, restored.Keys[1].T, 1e-4f);
            Assert.AreEqual(0.2f, restored.Keys[1].Value, 1e-4f);
        }

        [Test]
        public void Canonicalize_QuantizesLimbJointPositions()
        {
            var definition = DefinitionWithLimb();
            definition.FindPart("part_leg").Limb.Joints[1].Position = new Vector3(0f, -1.23456f, 0f);

            CreatureDefinition canonical = DefinitionCanonicalizer.Canonicalize(definition);
            Vector3 position = canonical.FindPart("part_leg").Limb.Joints[1].Position;

            Assert.AreEqual(-1.2346f, position.y, 1e-6f,
                "Limb joint positions must be quantized to 4 decimal places.");
        }

        [Test]
        public void Canonicalize_SortsThicknessKeysByT()
        {
            var definition = DefinitionWithLimb();
            var profile = definition.FindPart("part_leg").Limb.Thickness;
            profile.Keys.Clear();
            profile.Keys.Add(new ThicknessKey { T = 1f, Value = 0.1f });
            profile.Keys.Add(new ThicknessKey { T = 0f, Value = 0.4f });
            profile.Keys.Add(new ThicknessKey { T = 0.5f, Value = 0.2f });

            CreatureDefinition canonical = DefinitionCanonicalizer.Canonicalize(definition);
            ThicknessProfile keys = canonical.FindPart("part_leg").Limb.Thickness;

            Assert.AreEqual(3, keys.Keys.Count);
            Assert.AreEqual(0f, keys.Keys[0].T, 1e-6f);
            Assert.AreEqual(0.5f, keys.Keys[1].T, 1e-6f);
            Assert.AreEqual(1f, keys.Keys[2].T, 1e-6f);
        }

        [Test]
        public void Canonicalize_ThrowsOnNonFiniteJointPosition()
        {
            var definition = DefinitionWithLimb();
            definition.FindPart("part_leg").Limb.Joints[1].Position = new Vector3(float.NaN, 0f, 0f);

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition),
                "Canonicalization is not a repair pass; a non-finite joint is a programmer error.");
        }

        [Test]
        public void Canonicalize_ThrowsOnInvalidThicknessProfile()
        {
            var definition = DefinitionWithLimb();
            definition.FindPart("part_leg").Limb.Thickness.Keys.RemoveAt(1); // only one key

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition));
        }

        [Test]
        public void Canonicalize_ThrowsOnNegativeLimbBlendRadius()
        {
            var definition = DefinitionWithLimb();
            definition.FindPart("part_leg").Limb.BlendRadius = -0.1f;

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition),
                "Canonicalization is not a repair pass; a negative limb blend radius is a programmer error.");
        }

        [Test]
        public void Canonicalize_ThrowsOnNonFiniteLimbBlendRadius()
        {
            var definition = DefinitionWithLimb();
            definition.FindPart("part_leg").Limb.BlendRadius = float.NaN;

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition),
                "Canonicalization is not a repair pass; a non-finite limb blend radius is a programmer error.");
        }

        [Test]
        public void Canonicalize_QuantizesLimbBlendRadius()
        {
            var definition = DefinitionWithLimb();
            definition.FindPart("part_leg").Limb.BlendRadius = 0.123456f;

            CreatureDefinition canonical = DefinitionCanonicalizer.Canonicalize(definition);

            Assert.AreEqual(0.1235f, canonical.FindPart("part_leg").Limb.BlendRadius, 1e-6f,
                "The limb blend radius must be quantized to 4 decimal places.");
        }

        [Test]
        public void Serialize_LimbChain_ProducesCanonicalKeyOrder()
        {
            string json = _serializer.Serialize(DefinitionWithLimb());

            int idIndex = json.IndexOf("\"limbChain\":{\"joints\"", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(idIndex, 0, "limbChain must be emitted as an object with joints.");
            Assert.IsTrue(json.Contains("\"thicknessProfile\":{\"keys\":"),
                "The thickness profile must be emitted as keys.");
        }

        [Test]
        public void RoundTrip_BlendRadius_IsPreserved()
        {
            var definition = DefinitionWithLimb();
            definition.FindPart("part_leg").Limb.BlendRadius = 0.35f;

            string json = _serializer.Serialize(definition);
            CreatureDefinition loaded = _serializer.Deserialize(json);

            Assert.AreEqual(0.35f, loaded.FindPart("part_leg").Limb.BlendRadius, 1e-4f,
                "The authored limb blend radius must round-trip.");
        }

        [Test]
        public void Deserialize_LimbChainWithoutBlendRadius_DefaultsToStandard()
        {
            // A file saved between CC-018 and CC-049 has a limbChain but no
            // blendRadius field. It must load with the standard default so
            // existing creatures generate identically (additive, no version bump).
            string json = _serializer.Serialize(DefinitionWithLimb());
            string legacy = json.Replace(",\"blendRadius\":0.1000", string.Empty);
            Assert.IsFalse(legacy.Contains("blendRadius"), "Sanity: the legacy file must not mention blendRadius.");

            CreatureDefinition loaded = _serializer.Deserialize(legacy);
            Assert.AreEqual(
                ProceduralCreature.Definition.LimbChain.DefaultBlendRadius,
                loaded.FindPart("part_leg").Limb.BlendRadius, 1e-6f,
                "A pre-CC-049 limbChain must load with the default blend radius.");
        }
    }
}
