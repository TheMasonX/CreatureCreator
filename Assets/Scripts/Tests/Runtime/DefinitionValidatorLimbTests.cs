using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Phase 2 (CC-018) limb chain validation. Runtime assembly — per project
    /// convention this fixture is NOT discovered by the MCP runner; invoke its
    /// methods directly via execute_code for evidence.
    /// </summary>
    [TestFixture]
    public class DefinitionValidatorLimbTests
    {
        private static CreaturePart LimbPart(string id, LimbChain limb)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = limb,
            };
        }

        private static LimbChain StraightChain()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });
            return chain;
        }

        private static CreatureDefinition DefinitionWith(LimbChain limb)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(LimbPart("part_leg", limb));
            return definition;
        }

        [Test]
        public void Validate_ValidLimbChain_Passes()
        {
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(StraightChain()));
            Assert.IsTrue(result.IsValid, "A straight two-joint chain at the local origin with a default profile should validate.");
        }

        [Test]
        public void Validate_LimbWithNoJoints_ReportsInvalidLimbChain()
        {
            var chain = new LimbChain(); // no joints
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidLimbChain));
        }

        [Test]
        public void Validate_LimbWithSingleJoint_ReportsJointCountOutOfRange()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.LimbJointCountOutOfRange));
        }

        [Test]
        public void Validate_LimbWithTooManyJoints_ReportsJointCountOutOfRange()
        {
            var chain = new LimbChain();
            for (uint i = 1; i <= GenerationTolerances.MaxLimbJointCount + 1; i++)
            {
                chain.Joints.Add(new LimbJoint { Id = i, Position = new Vector3(0f, -(float)i * 0.5f, 0f) });
            }
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.LimbJointCountOutOfRange));
        }

        [Test]
        public void Validate_LimbWithDuplicateJointIds_ReportsDuplicateLimbJointId()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 1, Position = new Vector3(0f, -1f, 0f) });
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.DuplicateLimbJointId));
        }

        [Test]
        public void Validate_LimbWithNonIncreasingJointIds_ReportsOrderNotDeterministic()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 2, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 1, Position = new Vector3(0f, -1f, 0f) });
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.LimbJointOrderNotDeterministic));
        }

        [Test]
        public void Validate_LimbWithNonFiniteJoint_ReportsNonFiniteLimbJoint()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(float.NaN, -1f, 0f) });
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.NonFiniteLimbJoint));
        }

        [Test]
        public void Validate_LimbWithZeroLengthSegment_ReportsSegmentTooShort()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = Vector3.zero }); // collapses onto root
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.LimbSegmentTooShort));
        }

        [Test]
        public void Validate_LimbWithOutOfBoundsJoint_ReportsJointOutOfBounds()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(50f, 0f, 0f) }); // outside default bounds (4)
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.LimbJointOutOfBounds));
        }

        [Test]
        public void Validate_LimbRootAwayFromOrigin_ReportsRootNotAtOrigin()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = new Vector3(1f, 0f, 0f) }); // not ≈ zero
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(1f, -1f, 0f) });
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.LimbRootNotAtOrigin));
        }

        [Test]
        public void Validate_LimbWithNullThickness_ReportsInvalidThicknessProfile()
        {
            var chain = StraightChain();
            chain.Thickness = null;
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidThicknessProfile));
        }

        [Test]
        public void Validate_LimbWithNonFiniteThickness_ReportsNonFiniteThickness()
        {
            var chain = StraightChain();
            chain.Thickness.Keys[1].Value = float.PositiveInfinity;
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.NonFiniteThickness));
        }

        [Test]
        public void Validate_LimbWithSingleThicknessKey_ReportsInvalidThicknessProfile()
        {
            var chain = StraightChain();
            chain.Thickness.Keys.RemoveAt(1); // only one key left
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidThicknessProfile));
        }

        [Test]
        public void Validate_LimbWithOutOfRangeThicknessT_ReportsInvalidThicknessProfile()
        {
            var chain = StraightChain();
            chain.Thickness.Keys[1].T = 1.5f; // outside [0, 1]
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidThicknessProfile));
        }

        [Test]
        public void Validate_LimbWithDuplicateThicknessT_ReportsInvalidThicknessProfile()
        {
            var chain = StraightChain();
            chain.Thickness.Keys[1].T = 0f; // collides with key 0
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidThicknessProfile));
        }

        [Test]
        public void Validate_LimbWithNonPositiveThicknessValue_ReportsInvalidThicknessProfile()
        {
            var chain = StraightChain();
            chain.Thickness.Keys[1].Value = 0f;
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(chain));
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidThicknessProfile));
        }

        [Test]
        public void Validate_LimbPartIgnoresShape()
        {
            // ADR-001 §2: a limb's geometry derives from the chain; Shape is inert,
            // so an otherwise-invalid Shape must not fail a limb part.
            var chain = StraightChain();
            CreatureDefinition definition = DefinitionWith(chain);
            definition.FindPart("part_leg").Shape = new ShapeDefinition
            {
                Type = ShapeType.Sphere,
                PrimarySize = 0f,        // invalid for a non-limb
                SmoothBlendRadius = -1f, // invalid for a non-limb
            };

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(result.IsValid, "A limb part with an inert (invalid-as-a-primitive) Shape should still validate.");
            Assert.IsFalse(HasCode(result, ValidationCode.InvalidShapeParameter));
        }

        [Test]
        public void Validate_NonLimbPartStillChecksShape()
        {
            var chain = StraightChain();
            CreatureDefinition definition = DefinitionWith(chain);
            CreaturePart primitive = definition.FindPart("part_leg");
            primitive.Limb = null; // becomes a plain primitive
            primitive.Shape = new ShapeDefinition
            {
                Type = ShapeType.Sphere,
                PrimarySize = 0f,
                SmoothBlendRadius = 0f,
            };

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidShapeParameter),
                "A non-limb part must still have a valid Shape.");
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
