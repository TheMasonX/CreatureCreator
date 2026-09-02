using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class DefinitionCanonicalizerTests
    {
        private static CreatureDefinition MakeSinglePartDefinition(TransformData transform)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = Vector3.zero,
                Radius = 1f,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_a",
                ParentId = CreatureDefinition.BodyId,
                Transform = transform,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                PartType = PartType.Limb,
            });
            return definition;
        }

        [Test]
        public void Canonicalize_QuantizesPositionToFourDecimalPlaces()
        {
            var transform = new TransformData
            {
                Position = new Vector3(1.23456f, 0f, 0f),
                Rotation = Quaternion.identity,
                Scale = Vector3.one,
            };
            var definition = MakeSinglePartDefinition(transform);

            CreatureDefinition result = DefinitionCanonicalizer.Canonicalize(definition);

            Assert.AreEqual(1.2346f, result.Parts[0].Transform.Position.x, 1e-6f,
                "Expected away-from-zero rounding to 4 decimal places.");
        }

        [Test]
        public void Canonicalize_NormalizesNonUnitRotation()
        {
            var transform = new TransformData
            {
                Position = Vector3.zero,
                Rotation = new Quaternion(0f, 0f, 0f, 2f), // unnormalized
                Scale = Vector3.one,
            };
            var definition = MakeSinglePartDefinition(transform);

            CreatureDefinition result = DefinitionCanonicalizer.Canonicalize(definition);
            Quaternion rotation = result.Parts[0].Transform.Rotation;
            float magnitude = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y
                                          + rotation.z * rotation.z + rotation.w * rotation.w);

            Assert.AreEqual(1f, magnitude, 1e-3f,
                "Canonicalization must normalize rotation before quantizing (§2.3).");
        }

        [Test]
        public void Canonicalize_ReNormalizesQuantizedRotation()
        {
            var transform = new TransformData
            {
                Position = Vector3.zero,
                Rotation = new Quaternion(0f, 0.1305f, 0f, 0.9914f),
                Scale = Vector3.one,
            };
            var definition = MakeSinglePartDefinition(transform);

            CreatureDefinition result = DefinitionCanonicalizer.Canonicalize(definition);
            Quaternion rotation = result.Parts[0].Transform.Rotation;
            float magnitude = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y
                                          + rotation.z * rotation.z + rotation.w * rotation.w);

            Assert.That(magnitude, Is.EqualTo(1f).Within(1e-4f),
                "Quantized rotations must be renormalized to avoid invalid Unity TRS warnings.");
        }

        [Test]
        public void Canonicalize_ThrowsOnNaNPosition()
        {
            var transform = new TransformData
            {
                Position = new Vector3(float.NaN, 0f, 0f),
                Rotation = Quaternion.identity,
                Scale = Vector3.one,
            };
            var definition = MakeSinglePartDefinition(transform);

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition),
                "Canonicalizing invalid data is a programmer error — validate first (§14).");
        }

        [Test]
        public void Canonicalize_ThrowsOnInfiniteScale()
        {
            var transform = new TransformData
            {
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                Scale = new Vector3(float.PositiveInfinity, 1f, 1f),
            };
            var definition = MakeSinglePartDefinition(transform);

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition));
        }

        [Test]
        public void Canonicalize_SortsPartsByIdRegardlessOfInputOrder()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            definition.AddPart(new CreaturePart
            {
                Id = "part_z", ParentId = CreatureDefinition.BodyId, Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_a", ParentId = CreatureDefinition.BodyId, Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            });

            CreatureDefinition result = DefinitionCanonicalizer.Canonicalize(definition);

            Assert.AreEqual("part_a", result.Parts[0].Id);
            Assert.AreEqual("part_z", result.Parts[1].Id);
        }

        [Test]
        public void Canonicalize_DoesNotMutateInputDefinition()
        {
            var transform = new TransformData
            {
                Position = new Vector3(1.23456f, 0f, 0f),
                Rotation = Quaternion.identity,
                Scale = Vector3.one,
            };
            var definition = MakeSinglePartDefinition(transform);

            DefinitionCanonicalizer.Canonicalize(definition);

            Assert.AreEqual(1.23456f, definition.Parts[0].Transform.Position.x,
                "Canonicalize must return a new definition, not mutate the input in place.");
        }

        [Test]
        public void Canonicalize_RejectsNullPartWithDomainException()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Parts.Add(null);

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition));
        }

        [Test]
        public void Canonicalize_RejectsParentCycleWithDomainException()
        {
            var definition = MakeSinglePartDefinition(TransformData.Identity);
            definition.Parts[0].ParentId = definition.Parts[0].Id;

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition));
        }
    }
}
