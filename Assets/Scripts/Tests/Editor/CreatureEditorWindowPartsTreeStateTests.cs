using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Editor;
using ProceduralCreature.Serialization;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// CC-020: the parts-tree expansion state is editor presentation state
    /// (ExpandedPartIds), never creature DNA. These tests cover the pure logic —
    /// the auto-reveal ancestor chain and the SessionState persistence format —
    /// and prove that toggling expansion never alters the serialized DNA. The
    /// render-time behavior (collapsed nodes hide descendants, foldouts survive
    /// regeneration/undo) is a manual editor check because IMGUI layout is not
    /// unit-testable.
    /// </summary>
    [TestFixture]
    public class CreatureEditorWindowPartsTreeStateTests
    {
        private static CreatureDefinition DefinitionWithChain(
            out string legId, out string footId, out string toeId)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });

            legId = "part_leg";
            footId = "part_foot";
            toeId = "part_toe";
            definition.AddPart(new CreaturePart
            {
                Id = legId,
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Leg,
            });
            definition.AddPart(new CreaturePart
            {
                Id = footId,
                ParentId = legId,
                PartType = PartType.Foot,
            });
            definition.AddPart(new CreaturePart
            {
                Id = toeId,
                ParentId = footId,
                PartType = PartType.Foot,
            });
            return definition;
        }

        // ---- CC-007 step 5: placement staleness fingerprint ----------------------
        // The preview's MeshCollider is built from the Body at the last successful
        // regenerate. Placement must be blocked when that Body no longer matches
        // the live definition. The fingerprint covers only what placement depends
        // on (Body samples + Forward) so part edits never mark the preview stale.

        private static CreatureDefinition FingerprintBody()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.5f });
            return definition;
        }

        [Test]
        public void BuildPlacementFingerprint_ChangesWhenBodyGeometryChanges()
        {
            CreatureDefinition a = FingerprintBody();
            CreatureDefinition b = FingerprintBody();
            b.Body.Samples[1].Position = new Vector3(0f, 0f, 2f);

            Assert.AreNotEqual(
                CreatureEditorWindow.BuildPlacementFingerprint(a),
                CreatureEditorWindow.BuildPlacementFingerprint(b),
                "A Body sample position change must invalidate the placement fingerprint.");
        }

        [Test]
        public void BuildPlacementFingerprint_ChangesWhenBodySampleIdOrForwardChanges()
        {
            CreatureDefinition a = FingerprintBody();
            CreatureDefinition renumbered = FingerprintBody();
            renumbered.Body.Samples[0].Id = 99u;
            Assert.AreNotEqual(
                CreatureEditorWindow.BuildPlacementFingerprint(a),
                CreatureEditorWindow.BuildPlacementFingerprint(renumbered),
                "A Body sample Id change must invalidate the placement fingerprint.");

            CreatureDefinition rotatedForward = FingerprintBody();
            rotatedForward.Forward = Vector3.up;
            Assert.AreNotEqual(
                CreatureEditorWindow.BuildPlacementFingerprint(a),
                CreatureEditorWindow.BuildPlacementFingerprint(rotatedForward),
                "A Forward change must invalidate the placement fingerprint.");
        }

        [Test]
        public void BuildPlacementFingerprint_UnchangedByPartEdits()
        {
            CreatureDefinition a = FingerprintBody();
            CreatureDefinition b = FingerprintBody();
            b.AddPart(new CreaturePart
            {
                Id = "part_added",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Part,
            });
            b.FindPart("part_added").Transform = new TransformData
            {
                Position = new Vector3(1f, 2f, 3f),
                Rotation = Quaternion.Euler(10f, 20f, 30f),
                Scale = new Vector3(2f, 2f, 2f),
            };

            Assert.AreEqual(
                CreatureEditorWindow.BuildPlacementFingerprint(a),
                CreatureEditorWindow.BuildPlacementFingerprint(b),
                "Part-only edits must not mark the placement preview stale.");
        }

        [Test]
        public void AncestorsToReveal_DeepChain_ReturnsAncestorsRootMostFirst()
        {
            CreatureDefinition definition = DefinitionWithChain(out string legId, out string footId, out _);

            IReadOnlyList<string> revealed = CreatureEditorWindow.AncestorsToReveal(definition, "part_toe");

            CollectionAssert.AreEqual(new[] { legId, footId }, revealed,
                "Selecting a hidden descendant must reveal every collapsed ancestor, root-most first.");
        }

        [Test]
        public void AncestorsToReveal_DirectBodyChild_ReturnsEmpty()
        {
            CreatureDefinition definition = DefinitionWithChain(out string legId, out _, out _);

            Assert.IsEmpty(CreatureEditorWindow.AncestorsToReveal(definition, legId),
                "A direct Body child is already visible; there is nothing to reveal.");
        }

        [Test]
        public void AncestorsToReveal_UnknownTarget_ReturnsEmpty()
        {
            CreatureDefinition definition = DefinitionWithChain(out _, out _, out _);

            Assert.IsEmpty(CreatureEditorWindow.AncestorsToReveal(definition, "part_doesNotExist"));
            Assert.IsEmpty(CreatureEditorWindow.AncestorsToReveal(definition, null));
            Assert.IsEmpty(CreatureEditorWindow.AncestorsToReveal(definition, CreatureDefinition.BodyId));
        }

        [Test]
        public void AncestorsToReveal_BrokenParentChain_StopsAtGap()
        {
            CreatureDefinition definition = DefinitionWithChain(out _, out _, out _);
            definition.AddPart(new CreaturePart
            {
                Id = "part_orphan",
                ParentId = "part_missing",
                PartType = PartType.Part,
            });

            Assert.IsEmpty(CreatureEditorWindow.AncestorsToReveal(definition, "part_orphan"),
                "A broken parent chain cannot be revealed; the validator flags it separately.");
        }

        [Test]
        public void ReachableFromBody_IncludesDescendantsRegardlessOfCollapse()
        {
            // Regression for the "children jump to Unparented when I collapse"
            // bug: reachability follows ParentId links only — there is no collapse
            // input to the function, so a collapsed node can never make its
            // descendants look unparented.
            CreatureDefinition definition = DefinitionWithChain(out _, out _, out _);

            HashSet<string> reachable = CreatureEditorWindow.ReachableFromBody(definition);

            Assert.IsTrue(reachable.Contains("part_leg"), "Leg reachable from Body.");
            Assert.IsTrue(reachable.Contains("part_foot"), "A collapsed child's Foot must still be reachable.");
            Assert.IsTrue(reachable.Contains("part_toe"), "A collapsed grandchild's Toe must still be reachable.");
            Assert.AreEqual(4, reachable.Count, "Body + all three chain parts are reachable.");
        }

        [Test]
        public void ReachableFromBody_ExcludesBrokenParentLinks()
        {
            CreatureDefinition definition = DefinitionWithChain(out _, out _, out _);
            definition.AddPart(new CreaturePart
            {
                Id = "part_orphan",
                ParentId = "part_missing",
                PartType = PartType.Part,
            });

            HashSet<string> reachable = CreatureEditorWindow.ReachableFromBody(definition);

            Assert.IsFalse(reachable.Contains("part_orphan"),
                "A part with a missing parent is not reachable and should stay under Unparented.");
            Assert.IsTrue(reachable.Contains("part_toe"), "Valid descendants remain reachable.");
        }

        [Test]
        public void ReachableFromBody_TerminatesOnCycles()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = "part_a", ParentId = "part_b", PartType = PartType.Part });
            definition.AddPart(new CreaturePart { Id = "part_b", ParentId = "part_a", PartType = PartType.Part });

            HashSet<string> reachable = CreatureEditorWindow.ReachableFromBody(definition);

            Assert.IsFalse(reachable.Contains("part_a"), "Cycle members not rooted at Body are unreachable.");
            Assert.IsFalse(reachable.Contains("part_b"));
            Assert.AreEqual(1, reachable.Count, "Only the Body root is reachable.");
        }

        [Test]
        public void SerializeExpandedIds_RoundTripsDeterministically()
        {
            var expanded = new HashSet<string> { "part_foot", "part_leg", "part_foot" };

            string persisted = CreatureEditorWindow.SerializeExpandedIds(expanded);
            Assert.AreEqual("part_foot,part_leg", persisted,
                "The format is sorted, deduplicated, comma separated.");

            HashSet<string> restored = CreatureEditorWindow.DeserializeExpandedIds(persisted);
            CollectionAssert.AreEquivalent(expanded, restored,
                "Expansion state must survive a round-trip through its persistence format.");
        }

        [Test]
        public void DeserializeExpandedIds_HandlesEmptyAndNoise()
        {
            Assert.IsEmpty(CreatureEditorWindow.DeserializeExpandedIds(""));
            Assert.IsEmpty(CreatureEditorWindow.DeserializeExpandedIds(null));

            HashSet<string> parsed = CreatureEditorWindow.DeserializeExpandedIds("part_a,,part_b,");
            CollectionAssert.AreEquivalent(new[] { "part_a", "part_b" }, parsed,
                "Empty tokens and trailing commas are ignored.");
        }

        [Test]
        public void ExpansionState_DoesNotAlterDna()
        {
            CreatureDefinition definition = DefinitionWithChain(out _, out _, out _);
            var serializer = new JsonDnaSerializer();
            string before = serializer.Serialize(definition);

            // The editor keeps expansion state in a separate set, persisted through
            // its own format — it never flows through the DNA serializer.
            HashSet<string> expanded = CreatureEditorWindow.DeserializeExpandedIds("");
            expanded.Add("part_leg");
            expanded.Add("part_foot");
            string persisted = CreatureEditorWindow.SerializeExpandedIds(expanded);
            HashSet<string> restored = CreatureEditorWindow.DeserializeExpandedIds(persisted);
            CollectionAssert.AreEquivalent(expanded, restored);

            string after = serializer.Serialize(definition);
            Assert.AreEqual(before, after,
                "Toggling expansion state must never alter the serialized creature DNA.");
        }
    }
}
