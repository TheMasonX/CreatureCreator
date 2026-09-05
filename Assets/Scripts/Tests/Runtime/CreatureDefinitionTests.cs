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
        public void MalformedHierarchy_IsTotalAndPreservesDiagnostics()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Parts.Add(null);
            definition.Parts.Add(MakePart("duplicate", "cycle"));
            definition.Parts.Add(MakePart("duplicate", CreatureDefinition.BodyId));
            definition.Parts.Add(MakePart("cycle", "duplicate"));

            CreaturePartHierarchyIndex hierarchy = definition.CreateHierarchyIndex();

            Assert.IsTrue(hierarchy.HasNullEntries);
            CollectionAssert.Contains(hierarchy.DuplicateIds, "duplicate");
            Assert.IsTrue(hierarchy.TryResolve("duplicate", out _));
            Assert.IsTrue(hierarchy.HasParentCycle(out List<string> cycleIds));
            CollectionAssert.Contains(cycleIds, "duplicate");
            Assert.IsNotNull(definition.FindPart("duplicate"));
            Assert.DoesNotThrow(() => definition.GetChildren(CreatureDefinition.BodyId));
        }

        [Test]
        public void HierarchyIndex_Parts_IsDetachedSnapshotOfDefinition()
        {
            // CC-089 read-only aliasing: the tolerant index must be a snapshot of
            // the parts list taken at construction. Later mutation of the
            // definition's Parts list must not change what the index's Parts
            // enumeration, first-wins lookup, or cached maps report.
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(MakePart("part_a"));
            definition.AddPart(MakePart("part_b", "part_a"));

            CreaturePartHierarchyIndex hierarchy = definition.CreateHierarchyIndex();

            definition.Parts.Add(MakePart("part_c"));

            Assert.AreEqual(2, hierarchy.Parts.Count,
                "A part added to the definition after index construction must not appear in the index snapshot.");
            Assert.AreEqual("part_a", hierarchy.Parts[0].Id);
            Assert.AreEqual("part_b", hierarchy.Parts[1].Id);
            Assert.IsFalse(hierarchy.TryResolve("part_c", out _),
                "First-wins lookup must not observe a part added after index construction.");

            definition.Parts.RemoveAll(p => p != null && p.Id == "part_a");
            Assert.IsTrue(hierarchy.TryResolve("part_a", out _),
                "Removing a part from the definition after construction must not change the index snapshot.");
            Assert.AreEqual(2, hierarchy.Parts.Count,
                "Removal from the live definition must not shrink the detached index snapshot.");
        }

        [Test]
        public void HierarchyIndex_Parts_CannotBeMutatedThroughReadOnlyView()
        {
            // CC-089 read-only aliasing: the Parts surface is a private copy, not
            // the definition's live list. A caller must not be able to mutate the
            // definition through the index's read-only view.
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(MakePart("part_a"));
            CreaturePartHierarchyIndex hierarchy = definition.CreateHierarchyIndex();

            var partsView = hierarchy.Parts as System.Collections.Generic.IList<CreaturePart>;
            Assert.IsNotNull(partsView, "Array.AsReadOnly must expose an IList surface.");
            Assert.IsTrue(partsView.IsReadOnly, "Parts must be read-only over the private copy.");
            Assert.Throws<System.NotSupportedException>(() => partsView[0] = MakePart("part_x"));

            Assert.AreEqual(1, definition.Parts.Count,
                "Definition must be unchanged by an attempted mutation through the index view.");
            Assert.AreEqual("part_a", definition.Parts[0].Id);
        }

        [Test]
        public void Clone_PreservesNullPartEntriesWithoutThrowing()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Parts.Add(null);

            CreatureDefinition clone = definition.Clone();

            Assert.AreEqual(1, clone.Parts.Count);
            Assert.IsNull(clone.Parts[0]);
        }

        [Test]
        public void RemovePart_SkipsNullEntriesAndRemovesOnlyFirstDuplicate()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Parts.Add(null);
            definition.Parts.Add(MakePart("duplicate"));
            definition.Parts.Add(MakePart("duplicate"));

            Assert.DoesNotThrow(() => Assert.IsTrue(definition.RemovePart("duplicate")));
            Assert.AreEqual(2, definition.Parts.Count);
            Assert.IsNull(definition.Parts[0]);
            Assert.IsNotNull(definition.Parts[1]);
            Assert.AreEqual("duplicate", definition.Parts[1].Id,
                "Duplicate removal must be deterministic and remove only the first match.");
        }

        [Test]
        public void RemovePart_ReturnsFalseForNullPartList()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Parts = null;

            Assert.IsFalse(definition.RemovePart("missing"));
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
