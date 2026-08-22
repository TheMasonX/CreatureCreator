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
                ParentId = parentId ?? CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            };
        }

        [Test]
        public void Validate_EmptyDefinitionReportsMissingBody()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(HasCode(result, ValidationCode.MissingBody));
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

        // ---- v2 Body spline rules ------------------------------------------------------

        private static CreatureDefinition ValidDefinitionWithBody()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(ValidPart("part_leg"));
            return definition;
        }

        [Test]
        public void Validate_ValidBodySplineAndPart_Passes()
        {
            ValidationResult result = DefinitionValidator.Validate(ValidDefinitionWithBody());
            Assert.IsTrue(result.IsValid, "A definition with one Body spline, a valid Forward, and a Body descendant should validate.");
        }

        [Test]
        public void Validate_DetectsDuplicateBodySampleIds()
        {
            CreatureDefinition definition = ValidDefinitionWithBody();
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, 2f), Radius = 0.5f });

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.DuplicateBodySampleId));
        }

        [Test]
        public void Validate_DetectsNonIncreasingBodySampleIds()
        {
            CreatureDefinition definition = ValidDefinitionWithBody();
            definition.Body.Samples[1].Id = 1; // must increase with spline order

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.DuplicateBodySampleId));
        }

        [Test]
        public void Validate_DetectsInvalidBodySamplePosition()
        {
            CreatureDefinition definition = ValidDefinitionWithBody();
            definition.Body.Samples[0].Position = new Vector3(float.NaN, 0f, 0f);

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.InvalidBodySample));
        }

        [Test]
        public void Validate_DetectsNonPositiveBodySampleRadius()
        {
            CreatureDefinition definition = ValidDefinitionWithBody();
            definition.Body.Samples[0].Radius = 0f;

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.InvalidBodySample));
        }

        [Test]
        public void Validate_DetectsUnevenBodySpacing()
        {
            CreatureDefinition definition = ValidDefinitionWithBody();
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 5f), Radius = 0.6f });
            definition.Body.Samples.Add(new BodySample { Id = 4, Position = new Vector3(0f, 0f, 5.4f), Radius = 0.6f });

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.UnevenBodySpacing));
        }

        [Test]
        public void Validate_DetectsZeroForward()
        {
            CreatureDefinition definition = ValidDefinitionWithBody();
            definition.Forward = Vector3.zero;

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.InvalidForward));
        }

        [Test]
        public void Validate_RejectsPartWithNoParent()
        {
            CreatureDefinition definition = ValidDefinitionWithBody();
            definition.AddPart(ValidPart("part_root", parentId: null));

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.InvalidBodyParent),
                "Every non-Body part must descend from the Body in v2.");
        }

        [Test]
        public void Validate_RejectsReservedBodyPartType()
        {
            CreatureDefinition definition = ValidDefinitionWithBody();
            CreaturePart part = ValidPart("part_bad");
            part.PartType = PartType.Body;
            definition.AddPart(part);

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.UnsupportedPartType),
                "PartType.Body is reserved for the dedicated BodySpline in v2.");
        }

        [Test]
        public void Validate_RejectsIndependentRootTail()
        {
            CreatureDefinition definition = ValidDefinitionWithBody();
            CreaturePart part = ValidPart("part_tail");
            part.PartType = PartType.Tail;
            definition.AddPart(part);

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.InvalidBodyParent),
                "An independent Tail directly on the Body is not allowed in v2.");
        }

        [Test]
        public void Validate_DetectsInvalidAttachmentAnchor()
        {
            CreatureDefinition definition = ValidDefinitionWithBody();
            CreaturePart part = ValidPart("part_leg");
            part.ParentAttachment = new BodySurfaceAnchor
            {
                SegmentStartSampleId = 1,
                SegmentT = 1.5f, // outside [0,1]
                RadialAngle = 0f,
                SurfaceOffset = 0.1f,
                Roll = 0f,
            };
            definition.AddPart(part);

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.InvalidAttachmentAnchor));
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
