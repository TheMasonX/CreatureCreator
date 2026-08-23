using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProceduralCreature.Definition;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// CC-020 (rev 2): sibling ordering is a strategy (IPartSiblingOrderer), so
    /// the tree's ordering policy is swappable without touching DNA. These tests
    /// pin the alphabetical default and the grouped (OrderBy/ThenBy) alternative.
    /// </summary>
    [TestFixture]
    public class PartSiblingOrdererTests
    {
        private static CreaturePart Part(string id, string displayName, PartType type)
        {
            return new CreaturePart { Id = id, DisplayName = displayName, PartType = type };
        }

        [Test]
        public void Alphabetical_OrdersByDisplayNameThenId()
        {
            var parts = new List<CreaturePart>
            {
                Part("part_c", "Zebra", PartType.Leg),
                Part("part_a", "Alpha", PartType.Eye),
                Part("part_b", "Alpha", PartType.Arm), // tie on name -> id tiebreak
            };

            var ordered = PartSiblingOrderers.Alphabetical.OrderSiblings(parts).ToList();

            string[] ids = ordered.Select(p => p.Id).ToArray();
            CollectionAssert.AreEqual(new[] { "part_a", "part_b", "part_c" }, ids);
        }

        [Test]
        public void Alphabetical_EmptyNamesSortFirst()
        {
            var parts = new List<CreaturePart>
            {
                Part("part_named", "Named", PartType.Part),
                Part("part_empty", "", PartType.Part),
                Part("part_null", null, PartType.Part),
            };

            var ordered = PartSiblingOrderers.Alphabetical.OrderSiblings(parts).ToList();

            Assert.AreEqual("part_empty", ordered[0].Id, "Blank names sort first.");
            Assert.AreEqual("part_null", ordered[1].Id, "Null names sort first.");
            Assert.AreEqual("part_named", ordered[2].Id);
        }

        [Test]
        public void Grouped_OrdersByPartTypeThenName()
        {
            var parts = new List<CreaturePart>
            {
                Part("part_e", "Eye", PartType.Eye),
                Part("part_l2", "Leg", PartType.Leg),
                Part("part_a", "Arm", PartType.Arm),
                Part("part_l1", "ArmLimb", PartType.Leg),
                Part("part_p", "Pupil", PartType.Eye),
            };

            var ordered = PartSiblingOrderers.Grouped.OrderSiblings(parts).ToList();

            // PartType ordering follows the enum values (Leg=2 < Arm=3 < Eye=8), then
            // alphabetical by name within each type group. A display-priority type
            // order would be a refinement of this strategy later.
            CollectionAssert.AreEqual(
                new[] { "part_l1", "part_l2", "part_a", "part_e", "part_p" },
                ordered.Select(p => p.Id).ToArray());
        }

        [Test]
        public void Strategies_AreStatelessAndReusable()
        {
            var parts = new List<CreaturePart>
            {
                Part("part_b", "Beta", PartType.Part),
                Part("part_a", "Alpha", PartType.Part),
            };

            // The same strategy instance can be applied repeatedly and yields a
            // deterministic order each time (a swappable, stateless policy).
            var first = PartSiblingOrderers.Alphabetical.OrderSiblings(parts).Select(p => p.Id).ToArray();
            var second = PartSiblingOrderers.Alphabetical.OrderSiblings(parts).Select(p => p.Id).ToArray();
            CollectionAssert.AreEqual(new[] { "part_a", "part_b" }, first);
            CollectionAssert.AreEqual(first, second);
        }
    }
}
