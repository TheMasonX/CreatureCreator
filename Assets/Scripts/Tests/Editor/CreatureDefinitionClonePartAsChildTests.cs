using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// CC-029 "Add Child as Duplicate": CreatureDefinition.ClonePartAsChild copies
    /// the source's authoring properties (PartType, Shape, Appearance, symmetry,
    /// authored label) into a NEW part parented under the requested parent, with a
    /// fresh Id, identity placement, and a null ParentAttachment. The operation
    /// never mutates the source or the definition's Parts list; the editor adds the
    /// returned part through the single mutation path.
    /// </summary>
    [TestFixture]
    public class CreatureDefinitionClonePartAsChildTests
    {
        private static CreatureDefinition DefinitionWithBodyAndSource(out CreaturePart source)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });

            source = new CreaturePart
            {
                Id = "part_source",
                DisplayName = "MyLeg",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Leg,
                Transform = new TransformData
                {
                    Position = new Vector3(1f, 2f, 3f),
                    Rotation = Quaternion.Euler(0f, 45f, 0f),
                    Scale = Vector3.one * 2f,
                },
                Shape = new ShapeDefinition { Type = ShapeType.Capsule, PrimarySize = 1.5f, SmoothBlendRadius = 0.25f },
                Appearance = new AppearanceDefinition { BaseColor = Color.red, NoiseSeed = 42, NoiseScale = 1.5f },
                MirrorAcrossSymmetryPlane = true,
                ParentAttachment = new BodySurfaceAnchor
                {
                    SegmentStartSampleId = 1,
                    SegmentT = 0.5f,
                    RadialAngle = 0.25f,
                    SurfaceOffset = 0.1f,
                    Roll = 0.2f,
                },
            };
            definition.AddPart(source);
            return definition;
        }

        [Test]
        public void ClonePartAsChild_CopiesAuthoringProperties()
        {
            CreatureDefinition definition = DefinitionWithBodyAndSource(out CreaturePart source);

            CreaturePart clone = definition.ClonePartAsChild(source.Id, source.Id);

            Assert.AreEqual(PartType.Leg, clone.PartType, "PartType is an authoring property to copy.");
            Assert.AreEqual(source.Shape, clone.Shape, "Shape should be copied.");
            Assert.AreEqual(source.Appearance, clone.Appearance, "Appearance should be copied.");
            Assert.AreEqual(source.MirrorAcrossSymmetryPlane, clone.MirrorAcrossSymmetryPlane, "Symmetry flag should be copied.");
            Assert.AreEqual(source.DisplayName, clone.DisplayName, "The authored label should be carried to the duplicate.");
        }

        [Test]
        public void ClonePartAsChild_GeneratesFreshIdentity()
        {
            CreatureDefinition definition = DefinitionWithBodyAndSource(out CreaturePart source);

            CreaturePart clone = definition.ClonePartAsChild(source.Id, "part_otherParent");

            Assert.AreNotEqual(source.Id, clone.Id, "A duplicate must never reuse the source Id.");
            Assert.IsTrue(PartIdGenerator.LooksValid(clone.Id), "Fresh Id must look like a generated part id.");
            Assert.AreEqual("part_otherParent", clone.ParentId, "ParentId must be the requested new parent.");
        }

        [Test]
        public void ClonePartAsChild_ResetsPlacementAndAttachment()
        {
            CreatureDefinition definition = DefinitionWithBodyAndSource(out CreaturePart source);

            CreaturePart clone = definition.ClonePartAsChild(source.Id, source.Id);

            Assert.AreEqual(source.Id, clone.ParentId, "Duplicate-as-child parents under the source part.");
            Assert.AreEqual(TransformData.Identity.Position, clone.Transform.Position, "Placement is identity relative to the new parent.");
            Assert.AreEqual(TransformData.Identity.Rotation, clone.Transform.Rotation, "Placement is identity relative to the new parent.");
            Assert.AreEqual(TransformData.Identity.Scale, clone.Transform.Scale, "Placement is identity relative to the new parent.");
            Assert.IsNull(clone.ParentAttachment, "Attachment is recreated fresh, never copied from the source.");
        }

        [Test]
        public void ClonePartAsChild_LeavesSourceAndDefinitionUntouched()
        {
            CreatureDefinition definition = DefinitionWithBodyAndSource(out CreaturePart source);
            int partCountBefore = definition.Parts.Count;

            CreaturePart clone = definition.ClonePartAsChild(source.Id, source.Id);

            Assert.AreEqual(partCountBefore, definition.Parts.Count, "The operation must not add the clone itself.");
            Assert.AreEqual("part_source", source.Id, "Source identity unchanged.");
            Assert.AreEqual(CreatureDefinition.BodyId, source.ParentId, "Source parent unchanged.");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), source.Transform.Position, "Source placement unchanged.");
            Assert.IsNotNull(source.ParentAttachment, "Source attachment unchanged.");
            Assert.AreEqual(1u, source.ParentAttachment.SegmentStartSampleId, "Source attachment unchanged.");

            // The clone is independent — mutating it never touches the source.
            clone.Shape.PrimarySize = 99f;
            clone.Appearance.BaseColor = Color.green;
            clone.Transform.Position = Vector3.one * 5f;
            Assert.AreEqual(1.5f, source.Shape.PrimarySize, "Clone shape edits must not leak to the source.");
            Assert.AreEqual(Color.red, source.Appearance.BaseColor, "Clone appearance edits must not leak to the source.");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), source.Transform.Position, "Clone transform edits must not leak to the source.");
        }

        [Test]
        public void ClonePartAsChild_UnknownSource_Throws()
        {
            CreatureDefinition definition = DefinitionWithBodyAndSource(out CreaturePart _);

            Assert.Throws<DomainException>(() =>
                definition.ClonePartAsChild("part_doesNotExist", CreatureDefinition.BodyId));
        }

        [Test]
        public void CloneThenAddPart_ProducesValidDefinition()
        {
            CreatureDefinition definition = DefinitionWithBodyAndSource(out CreaturePart source);

            CreaturePart clone = definition.ClonePartAsChild(source.Id, source.Id);
            definition.AddPart(clone);

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(result.IsValid,
                "A Body + source part + cloned child must validate (fresh Id, parent links intact).");
        }
    }
}
