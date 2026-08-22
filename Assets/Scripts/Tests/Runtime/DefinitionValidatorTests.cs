using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class DefinitionValidatorTests
    {
        private static CreaturePart ValidPart(string id, string parentId = null)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = parentId,
                PartType = PartType.Body,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            };
        }

        [Test]
        public void Validate_EmptyDefinitionIsValid()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void Validate_DetectsDuplicateIds()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(ValidPart("part_a"));
            definition.AddPart(ValidPart("part_a"));

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(HasCode(result, ValidationCode.DuplicatePartId));
        }

        [Test]
        public void Validate_DetectsMissingParent()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(ValidPart("part_a", parentId: "part_does_not_exist"));

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(HasCode(result, ValidationCode.MissingParent));
        }

        [Test]
        public void Validate_DetectsParentCycle()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(ValidPart("part_a", "part_b"));
            definition.AddPart(ValidPart("part_b", "part_a"));

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(HasCode(result, ValidationCode.ParentCycle));
        }

        [Test]
        public void Validate_DetectsNonFiniteTransform()
        {
            var definition = CreatureDefinition.CreateEmpty();
            CreaturePart part = ValidPart("part_a");
            part.Transform = new TransformData
            {
                Position = new Vector3(float.NaN, 0, 0),
                Rotation = Quaternion.identity,
                Scale = Vector3.one,
            };
            definition.AddPart(part);

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(HasCode(result, ValidationCode.NonFiniteTransform));
        }

        [Test]
        public void Validate_DetectsInfiniteTransform()
        {
            var definition = CreatureDefinition.CreateEmpty();
            CreaturePart part = ValidPart("part_a");
            part.Transform = new TransformData
            {
                Position = new Vector3(float.PositiveInfinity, 0, 0),
                Rotation = Quaternion.identity,
                Scale = Vector3.one,
            };
            definition.AddPart(part);

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.NonFiniteTransform));
        }

        [Test]
        public void Validate_DetectsZeroScale()
        {
            var definition = CreatureDefinition.CreateEmpty();
            CreaturePart part = ValidPart("part_a");
            part.Transform = new TransformData
            {
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                Scale = new Vector3(0f, 1f, 1f),
            };
            definition.AddPart(part);

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.InvalidScale));
        }

        [Test]
        public void Validate_DetectsNegativeScale()
        {
            var definition = CreatureDefinition.CreateEmpty();
            CreaturePart part = ValidPart("part_a");
            part.Transform = new TransformData
            {
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                Scale = new Vector3(-1f, 1f, 1f),
            };
            definition.AddPart(part);

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.InvalidScale));
        }

        [Test]
        public void Validate_DetectsOutOfBoundsPosition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Bounds = new BoundsDefinition { MaxX = 1f, MaxY = 1f, MaxZ = 1f };
            CreaturePart part = ValidPart("part_a");
            part.Transform = new TransformData
            {
                Position = new Vector3(5f, 0f, 0f),
                Rotation = Quaternion.identity,
                Scale = Vector3.one,
            };
            definition.AddPart(part);

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.OutOfBoundsTransform));
        }

        [Test]
        public void Validate_DetectsInvalidShapeParameter()
        {
            var definition = CreatureDefinition.CreateEmpty();
            CreaturePart part = ValidPart("part_a");
            part.Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = -0.5f, SmoothBlendRadius = 0f };
            definition.AddPart(part);

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.InvalidShapeParameter));
        }

        [Test]
        public void Validate_DetectsUnsupportedSchemaVersion()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.SchemaVersion = 999;

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.UnsupportedSchemaVersion));
        }

        [Test]
        public void Validate_DetectsGenerationBudgetExceeded()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Bounds = new BoundsDefinition { MaxX = 1000f, MaxY = 1000f, MaxZ = 1000f };
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 64f };

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.GenerationBudgetExceeded));
        }

        [Test]
        public void Validate_IsOrderIndependent()
        {
            var definitionA = CreatureDefinition.CreateEmpty();
            definitionA.AddPart(ValidPart("part_a"));
            definitionA.AddPart(ValidPart("part_a")); // duplicate id -> one DuplicatePartId issue

            var definitionB = CreatureDefinition.CreateEmpty();
            definitionB.AddPart(ValidPart("part_a"));
            definitionB.AddPart(ValidPart("part_a"));
            definitionB.Parts.Reverse();

            ValidationResult resultA = DefinitionValidator.Validate(definitionA);
            ValidationResult resultB = DefinitionValidator.Validate(definitionB);

            Assert.AreEqual(resultA.Issues.Count, resultB.Issues.Count);
            for (int i = 0; i < resultA.Issues.Count; i++)
            {
                Assert.AreEqual(resultA.Issues[i].Code, resultB.Issues[i].Code);
            }
        }

        private static bool HasCode(ValidationResult result, ValidationCode code)
        {
            foreach (ValidationIssue issue in result.Issues)
            {
                if (issue.Code == code) return true;
            }
            return false;
        }
    }
}
