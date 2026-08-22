using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class CreatureDefinitionTests
    {
        private static CreaturePart MakePart(string id, string parentId = null)
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
        public void Clone_ProducesEqualButIndependentParts()
        {
            var original = CreatureDefinition.CreateEmpty();
            original.AddPart(MakePart("part_a"));

            CreatureDefinition clone = original.Clone();
            clone.Parts[0].Transform = new TransformData
            {
                Position = new UnityEngine.Vector3(1, 2, 3),
                Rotation = UnityEngine.Quaternion.identity,
                Scale = UnityEngine.Vector3.one,
            };

            Assert.AreEqual(UnityEngine.Vector3.zero, original.Parts[0].Transform.Position,
                "Mutating the clone must not affect the original (deep clone).");
        }

        [Test]
        public void FindPart_IsUnaffectedByListReordering()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(MakePart("part_a"));
            definition.AddPart(MakePart("part_b", "part_a"));

            definition.Parts.Reverse();

            CreaturePart found = definition.FindPart("part_b");
            Assert.IsNotNull(found);
            Assert.AreEqual("part_a", found.ParentId,
                "Identity/relationships must survive reordering (design doc §2.2).");
        }

        [Test]
        public void HasParentCycle_DetectsSelfReference()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var part = MakePart("part_a");
            part.ParentId = "part_a"; // points to itself
            definition.AddPart(part);

            bool hasCycle = definition.HasParentCycle(out List<string> ids);

            Assert.IsTrue(hasCycle);
            CollectionAssert.Contains(ids, "part_a");
        }

        [Test]
        public void HasParentCycle_DetectsIndirectCycle()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(MakePart("part_a", "part_b"));
            definition.AddPart(MakePart("part_b", "part_a"));

            Assert.IsTrue(definition.HasParentCycle(out _));
        }

        [Test]
        public void HasParentCycle_FalseForValidTree()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(MakePart("root"));
            definition.AddPart(MakePart("child", "root"));
            definition.AddPart(MakePart("grandchild", "child"));

            Assert.IsFalse(definition.HasParentCycle(out _));
        }

        [Test]
        public void CloneAsDuplicate_GeneratesNewId()
        {
            CreaturePart original = MakePart("part_original");
            CreaturePart duplicate = original.CloneAsDuplicate();

            Assert.AreNotEqual(original.Id, duplicate.Id,
                "Duplication must generate a new ID rather than copying the original (§2.2).");
            Assert.AreEqual(original.PartType, duplicate.PartType);
        }
    }
}
