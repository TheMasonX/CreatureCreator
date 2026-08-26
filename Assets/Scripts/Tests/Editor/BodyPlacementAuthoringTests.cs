using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// CC-007 step 4: BodyPlacementAuthoring converts a preview-mesh hit
    /// (creature-space position + outward normal) into the semantic
    /// BodySurfaceAnchor that becomes authoritative DNA for a direct Body child.
    /// The hit is interaction input only; only the returned anchor may be stored.
    /// Anchors are produced in the canonical (1..N renumbered) sample-ID space so
    /// they survive the editor mutation path's RenumberSamplesInOrder.
    /// </summary>
    [TestFixture]
    public class BodyPlacementAuthoringTests
    {
        private static CreatureDefinition StraightBody()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.5f });
            return definition;
        }

        [Test]
        public void TryProjectToAnchor_ProjectsHitToSurfaceAnchor()
        {
            bool ok = BodyPlacementAuthoring.TryProjectToAnchor(
                StraightBody(), new Vector3(0f, 0.5f, 0f), Vector3.up, out BodySurfaceAnchor anchor);

            Assert.IsTrue(ok);
            Assert.AreEqual(1u, anchor.SegmentStartSampleId);
            Assert.AreEqual(0.5f, anchor.SegmentT, 1e-4f);
            Assert.AreEqual(0f, anchor.RadialAngle, 1e-4f);
            Assert.AreEqual(0f, anchor.SurfaceOffset, 1e-4f);
            Assert.AreEqual(0f, anchor.Roll, 1e-4f);
        }

        [Test]
        public void TryProjectToAnchor_RollAlignsOutwardNormalAroundTangent()
        {
            bool ok = BodyPlacementAuthoring.TryProjectToAnchor(
                StraightBody(), new Vector3(0f, 0.5f, 0f), Vector3.right, out BodySurfaceAnchor anchor);

            Assert.IsTrue(ok);
            // Outward normal +X at a +Y radial is a -90° roll around the +Z tangent.
            Assert.AreEqual(-Mathf.PI * 0.5f, anchor.Roll, 1e-4f);
        }

        [Test]
        public void TryProjectToAnchor_SelectsTheClosestSegment()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 3f), Radius = 0.5f });

            // A hit clearly on the second segment (samples 2 -> 3).
            bool ok = BodyPlacementAuthoring.TryProjectToAnchor(
                definition, new Vector3(0f, 0.5f, 2f), Vector3.up, out BodySurfaceAnchor anchor);

            Assert.IsTrue(ok);
            Assert.AreEqual(2u, anchor.SegmentStartSampleId);
            Assert.AreEqual(0.5f, anchor.SegmentT, 1e-4f);
        }

        [Test]
        public void TryProjectToAnchor_ReturnsFalseForDegenerateOrInvalidInputs()
        {
            var oneSample = CreatureDefinition.CreateEmpty();
            oneSample.Forward = Vector3.forward;
            oneSample.Body.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });

            Assert.IsFalse(BodyPlacementAuthoring.TryProjectToAnchor(
                oneSample, Vector3.zero, Vector3.up, out _), "A single-sample Body has no surface segment.");
            Assert.IsFalse(BodyPlacementAuthoring.TryProjectToAnchor(
                null, Vector3.zero, Vector3.up, out _), "A null definition cannot be projected.");
            Assert.IsFalse(BodyPlacementAuthoring.TryProjectToAnchor(
                StraightBody(), new Vector3(float.NaN, 0f, 0f), Vector3.up, out _),
                "A non-finite hit position must be rejected.");
        }

        [Test]
        public void TryProjectToAnchor_CanonicalizesNonSequentialSampleIds()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 10, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 40, Position = new Vector3(0f, 0f, 1f), Radius = 0.5f });

            bool ok = BodyPlacementAuthoring.TryProjectToAnchor(
                definition, new Vector3(0f, 0.5f, 0f), Vector3.up, out BodySurfaceAnchor anchor);

            Assert.IsTrue(ok);
            // Sample 10 is the first sample; the canonical renumber maps it to 1.
            Assert.AreEqual(1u, anchor.SegmentStartSampleId,
                "A non-sequential authored Body must produce an anchor in the canonical 1..N ID space.");

            // Replicate the editor mutation path (AddPart + RenumberSamplesInOrder)
            // and confirm the anchor stays valid — the review-fix regression.
            var working = definition.Clone();
            working.AddPart(new CreaturePart
            {
                Id = PartIdGenerator.CreateNew(),
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Part,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                ParentAttachment = anchor,
            });
            BodySplineAuthoring.RenumberSamplesInOrder(working.Body);
            Assert.IsTrue(DefinitionValidator.Validate(working).IsValid,
                "An anchor produced by TryProjectToAnchor must survive the mutation path's sample renumbering.");
        }

        [Test]
        public void ResolveSurfaceFrameRotation_MatchesSurfaceFrameConvention()
        {
            bool ok = BodyPlacementAuthoring.TryProjectToAnchor(
                StraightBody(), new Vector3(0f, 0.5f, 0f), Vector3.up, out BodySurfaceAnchor anchor);
            Assert.IsTrue(ok);

            Quaternion rotation = BodyPlacementAuthoring.ResolveSurfaceFrameRotation(StraightBody(), anchor);

            // Frame convention shared with BodyFrameResolver and the runtime
            // resolver: local +Z -> body Tangent (forward), local +Y -> outward Normal.
            Assert.That(Vector3.Distance(rotation * Vector3.forward, Vector3.forward), Is.LessThan(1e-4f),
                "Surface frame +Z must map to the body tangent.");
            Assert.That(Vector3.Distance(rotation * Vector3.up, Vector3.up), Is.LessThan(1e-4f),
                "Surface frame +Y must map to the outward normal.");
        }

        [Test]
        public void TryResolveSurfaceFrame_ReturnsPlacementFrameAtTheHit()
        {
            bool ok = BodyPlacementAuthoring.TryProjectToAnchor(
                StraightBody(), new Vector3(0f, 0.5f, 0f), Vector3.up, out BodySurfaceAnchor anchor);
            Assert.IsTrue(ok);

            bool resolved = BodyPlacementAuthoring.TryResolveSurfaceFrame(
                StraightBody(), anchor, out Vector3 position, out Quaternion rotation);

            Assert.IsTrue(resolved);
            // Straight Body of radius 0.5 along +Z: the anchor's surface frame
            // sits 0.5 above the centerline at the hit.
            Assert.That(Vector3.Distance(position, new Vector3(0f, 0.5f, 0f)), Is.LessThan(1e-4f),
                "The surface frame position must reproduce the hit point on the Body surface.");
            Assert.That(Vector3.Distance(rotation * Vector3.up, Vector3.up), Is.LessThan(1e-4f),
                "Surface frame +Y must map to the outward normal (the ghost's up).");
            Assert.That(Vector3.Distance(rotation * Vector3.forward, Vector3.forward), Is.LessThan(1e-4f),
                "Surface frame +Z must map to the body tangent.");
        }

        [Test]
        public void TryResolveSurfaceFrame_ReturnsFalseForInvalidInput()
        {
            bool ok = BodyPlacementAuthoring.TryProjectToAnchor(
                StraightBody(), new Vector3(0f, 0.5f, 0f), Vector3.up, out BodySurfaceAnchor validAnchor);
            Assert.IsTrue(ok);

            Assert.IsFalse(BodyPlacementAuthoring.TryResolveSurfaceFrame(
                null, validAnchor, out _, out _), "A null definition has no surface frame.");
            Assert.IsFalse(BodyPlacementAuthoring.TryResolveSurfaceFrame(
                StraightBody(), null, out _, out _), "A null anchor has no surface frame.");

            var terminal = new BodySurfaceAnchor
            {
                SegmentStartSampleId = 2u, // terminal sample of the two-sample StraightBody: no outgoing segment
                SegmentT = 0.5f,
                RadialAngle = 0f,
                SurfaceOffset = 0f,
                Roll = 0f,
            };
            Assert.IsFalse(BodyPlacementAuthoring.TryResolveSurfaceFrame(
                StraightBody(), terminal, out _, out _), "A terminal sample id has no surface segment.");
        }

        [Test]
        public void AnchoredBodyChildLocalOffset_RoundTripsThroughResolverWithoutExplosion()
        {
            // CC-007 gizmo-drag fix regression: a placed (anchored, identity)
            // Body child's local position is the anchor SURFACE FRAME's local
            // space. A world->local conversion that treated it as creature space
            // wrote a creature-space offset the resolver misread as
            // surface-frame-local, so a drag along one gizmo axis exploded along
            // the frame's +Y. The fixed conversion inverts the surface frame; the
            // resolver must read the offset back in the same frame.
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.5f });

            bool ok = BodyPlacementAuthoring.TryProjectToAnchor(
                definition, new Vector3(0f, 0.5f, 0f), Vector3.up, out BodySurfaceAnchor anchor);
            Assert.IsTrue(ok);

            var part = new CreaturePart
            {
                Id = "part_anchored",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Part,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                ParentAttachment = anchor,
            };
            definition.AddPart(part);

            // The placed part's resolved frame IS the anchor surface frame.
            Matrix4x4 surfaceFrame =
                CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace(definition, part);
            Vector3 surfacePosition = surfaceFrame.GetColumn(3);
            Quaternion surfaceRotation = surfaceFrame.rotation;

            // Drag along the gizmo's local X (an arbitrary surface-frame axis) by
            // a moderate amount — the CC-007 gizmo-drag scenario.
            Vector3 localOffset = new Vector3(0.3f, 0f, 0f);
            Vector3 targetWorld = surfacePosition + surfaceRotation * localOffset;

            // The fixed conversion (WorldToLocalPosition for an anchored Body child):
            // invert the surface frame instead of returning creature space.
            Vector3 storedLocal = Quaternion.Inverse(surfaceRotation) * (targetWorld - surfacePosition);
            part.Transform = new TransformData
            {
                Position = storedLocal,
                Rotation = Quaternion.identity,
                Scale = Vector3.one,
            };

            Matrix4x4 resolved =
                CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace(definition, part);
            Assert.That(Vector3.Distance(resolved.GetColumn(3), targetWorld), Is.LessThan(1e-4f),
                "An anchored Body child's local offset must round-trip through the resolver " +
                "without exploding along the surface frame's +Y.");
        }
    }
}
