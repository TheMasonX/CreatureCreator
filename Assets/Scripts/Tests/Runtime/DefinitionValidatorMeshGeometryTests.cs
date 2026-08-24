using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// CC-031 pass 1: mesh-geometry validation. A part declares exactly one
    /// geometry source; mesh geometry is rejected when its key is empty, when a
    /// limb chain is also present, or when its attachment is non-finite or has a
    /// scale component below the minimum. Shape is inert for mesh parts, so a mesh
    /// part is not forced to carry a meaningful Shape.
    ///
    /// Runtime assembly — per project convention this fixture is NOT discovered by
    /// the MCP runner; invoke its methods directly via execute_code for evidence.
    /// </summary>
    [TestFixture]
    public class DefinitionValidatorMeshGeometryTests
    {
        private static CreatureDefinition DefinitionWith(MeshGeometry geometry)
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
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MeshGeometry = geometry,
            });
            return definition;
        }

        private static MeshGeometry ValidGeometry()
        {
            return new MeshGeometry
            {
                MeshAssetKey = "eye_mesh",
                Attachment = new GeometryAttachment(),
            };
        }

        [Test]
        public void Validate_ValidMeshGeometry_Passes()
        {
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(ValidGeometry()));
            Assert.IsTrue(result.IsValid, "a mesh part with a key and default identity attachment should validate");
        }

        [Test]
        public void Validate_EmptyMeshAssetKey_ReportsInvalidMeshGeometry()
        {
            MeshGeometry geometry = ValidGeometry();
            geometry.MeshAssetKey = "   ";
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(geometry));
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidMeshGeometry));
        }

        [Test]
        public void Validate_LimbAndMeshGeometry_ReportsInvalidMeshGeometry()
        {
            CreatureDefinition definition = DefinitionWith(ValidGeometry());
            CreaturePart part = definition.FindPart("eye");
            part.PartType = PartType.Limb;
            part.Limb = new LimbChain();
            part.Limb.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            part.Limb.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidMeshGeometry),
                "a part cannot declare both a limb chain and a mesh geometry");
        }

        [Test]
        public void Validate_NonFiniteAttachment_ReportsNonFiniteMeshGeometryAttachment()
        {
            MeshGeometry geometry = ValidGeometry();
            geometry.Attachment.Offset = new Vector3(float.NaN, 0f, 0f);
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(geometry));
            Assert.IsTrue(HasCode(result, ValidationCode.NonFiniteMeshGeometryAttachment));
        }

        [Test]
        public void Validate_ScaleBelowMinimum_ReportsInvalidMeshGeometryScale()
        {
            MeshGeometry geometry = ValidGeometry();
            geometry.Attachment.Scale = new Vector3(0f, 1f, 1f);
            ValidationResult result = DefinitionValidator.Validate(DefinitionWith(geometry));
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidMeshGeometryScale));
        }

        [Test]
        public void Validate_MeshPart_DoesNotRequireMeaningfulShape()
        {
            CreatureDefinition definition = DefinitionWith(ValidGeometry());
            definition.FindPart("eye").Shape = new ShapeDefinition
            {
                Type = ShapeType.Sphere,
                PrimarySize = 0f, // invalid for an implicit part, inert for a mesh part
                SmoothBlendRadius = 0f,
            };

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsFalse(HasCode(result, ValidationCode.InvalidShapeParameter),
                "Shape is inert for a mesh part (ADR-002 §2)");
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
