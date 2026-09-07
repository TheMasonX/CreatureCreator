using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using SkeletonModel = ProceduralCreature.Skeleton.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Fixture matrix and proofs for TSK-0131 — deterministic, skeleton-aware,
    /// build-time per-vertex weights for the implicit welded surface. Every
    /// fixture is synthetic resolved-part data (hand-authored segment bones,
    /// exactly like the LinearBlendSkinning contract fixtures) so it is pure and
    /// headless. The validation oracle is <see cref="LinearBlendSkinning.Deform"/>:
    /// authored weights must be valid LBS bindings that reproduce the rest mesh at
    /// rest and move the intended region under a known pose.
    ///
    /// Matrix: straight limb, single bend, branch point, a vertex at a joint seam,
    /// a mirrored limb, and the generated Body/welded-surface shape. Invariants
    /// asserted throughout: <= MaxInfluencesPerVertex influences per vertex,
    /// normalized, deterministic under repeated generation and under segment /
    /// vertex ordering, and bone indices in the SkeletonSnapshot.Capture order
    /// (the shared bind-index contract consumed by TSK-0132 / TSK-0130).
    /// </summary>
    [TestFixture]
    public sealed class ImplicitSurfaceWeightAuthoringTests
    {
        private const float T = 1e-3f;

        // ---- Skeleton helpers ---------------------------------------------------

        private static Bone SegmentBone(string id, Vector3 position, Vector3 end, bool mirrored = false)
        {
            return new Bone
            {
                Id = id,
                SourcePartId = "part",
                IsMirrored = mirrored,
                Position = position,
                HasSegment = true,
                EndPosition = end,
                Rotation = Quaternion.identity,
            };
        }

        private static SkeletonSnapshot Capture(params Bone[] bones)
        {
            var skeleton = new SkeletonModel();
            skeleton.Bones.AddRange(bones);
            return SkeletonSnapshot.Capture(skeleton);
        }

        private static BonePose[] RestFrames(SkeletonSnapshot snapshot)
        {
            var frames = new BonePose[snapshot.Count];
            for (int i = 0; i < snapshot.Count; i++)
            {
                frames[i] = new BonePose(snapshot[i].Position, snapshot[i].Rotation);
            }
            return frames;
        }

        // ---- Weight-structure assertions ----------------------------------------

        private static void AssertStructural(SkeletonSnapshot snapshot, VertexInfluence[][] weights, int vertexCount)
        {
            Assert.AreEqual(vertexCount, weights.Length, "one influence list per vertex");
            for (int v = 0; v < weights.Length; v++)
            {
                VertexInfluence[] influences = weights[v];
                Assert.IsNotNull(influences, $"vertex {v} influences must not be null");
                Assert.GreaterOrEqual(influences.Length, 1, $"vertex {v} must have >=1 influence");
                Assert.LessOrEqual(influences.Length, ImplicitSurfaceWeightAuthoring.MaxInfluencesPerVertex,
                    $"vertex {v} must have <=4 influences");

                float sum = 0f;
                for (int i = 0; i < influences.Length; i++)
                {
                    Assert.GreaterOrEqual(influences[i].Weight, 0f, $"vertex {v} influence {i} weight");
                    Assert.IsTrue(influences[i].BoneIndex >= 0 && influences[i].BoneIndex < snapshot.Count,
                        $"vertex {v} influence {i} bone index in range");
                    sum += influences[i].Weight;
                }
                Assert.AreEqual(1f, sum, T, $"vertex {v} weights are normalized");
            }
        }

        private static float WeightSum(IEnumerable<VertexInfluence> influences)
        {
            float sum = 0f;
            foreach (VertexInfluence influence in influences) sum += influence.Weight;
            return sum;
        }

        private static bool HasBone(IReadOnlyList<VertexInfluence> influences, int boneIndex)
        {
            foreach (VertexInfluence influence in influences)
            {
                if (influence.BoneIndex == boneIndex && influence.Weight > 0f) return true;
            }
            return false;
        }

        // ---- Fixtures -----------------------------------------------------------

        [Test]
        public void Author_StraightLimb_SingleInfluenceRigid_AndRestPoseRoundTrip()
        {
            // Straight two-segment limb along +X: bone straight_0 [0..1], bone straight_1 [1..2].
            SkeletonSnapshot snapshot = Capture(
                SegmentBone("straight_0", new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f)),
                SegmentBone("straight_1", new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f)));
            int bone0 = snapshot.GetIndex("straight_0");
            int bone1 = snapshot.GetIndex("straight_1");

            List<BoneSegmentInfluence> segments = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(
                snapshot, new float[] { 0.12f, 0.12f });
            Assert.AreEqual(2, segments.Count, "both straight segments are eligible");

            // On-axis vertices; radius 0.12 keeps a vertex more than one influence
            // radius (x0.12*3=0.36) from the far segment single-influence.
            Vector3[] restVertices =
            {
                new Vector3(0.5f, 0f, 0f),   // proximal -> bone0 (rigid)
                new Vector3(1.0f, 0f, 0f),   // the shared joint -> blends both
                new Vector3(1.5f, 0f, 0f),   // distal -> bone1 (rigid)
            };

            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(segments, restVertices);
            AssertStructural(snapshot, weights, restVertices.Length);

            Assert.AreEqual(1, weights[0].Length);
            Assert.AreEqual(bone0, weights[0][0].BoneIndex);
            Assert.AreEqual(1f, weights[0][0].Weight, T);

            // The joint-seam vertex is equidistant to both segments -> smooth 50/50 blend.
            Assert.AreEqual(2, weights[1].Length, "seam vertex blends two segments");
            Assert.AreEqual(bone0, weights[1][0].BoneIndex);
            Assert.AreEqual(bone1, weights[1][1].BoneIndex);
            Assert.AreEqual(0.5f, weights[1][0].Weight, T);
            Assert.AreEqual(0.5f, weights[1][1].Weight, T);

            Assert.AreEqual(1, weights[2].Length);
            Assert.AreEqual(bone1, weights[2][0].BoneIndex);

            // Rest-pose equivalence against the LinearBlendSkinning oracle.
            BonePose[] rest = RestFrames(snapshot);
            Vector3[] roundTripped = LinearBlendSkinning.Deform(rest, rest, restVertices, weights);
            for (int v = 0; v < restVertices.Length; v++)
            {
                Assert.AreEqual(restVertices[v].x, roundTripped[v].x, T);
                Assert.AreEqual(restVertices[v].y, roundTripped[v].y, T);
            }
        }

        [Test]
        public void Author_SingleBend_KnownPose_OnlyDistalRegionMoves()
        {
            // Straight limb with the distal bone posed +90 about Z at the shared joint.
            SkeletonSnapshot snapshot = Capture(
                SegmentBone("bend_0", new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f)),
                SegmentBone("bend_1", new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f)));
            int bone0 = snapshot.GetIndex("bend_0");
            int bone1 = snapshot.GetIndex("bend_1");

            List<BoneSegmentInfluence> segments = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(
                snapshot, new float[] { 0.12f, 0.12f });
            Vector3[] restVertices =
            {
                new Vector3(0.5f, 0f, 0f), // rigid to bone0 -> stays under the bend
                new Vector3(1.5f, 0f, 0f), // rigid to bone1 -> swings up
            };
            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(segments, restVertices);
            AssertStructural(snapshot, weights, restVertices.Length);
            Assert.AreEqual(bone0, weights[0][0].BoneIndex);
            Assert.AreEqual(bone1, weights[1][0].BoneIndex);

            BonePose[] rest = RestFrames(snapshot);
            var posed = new BonePose[snapshot.Count];
            for (int i = 0; i < snapshot.Count; i++) posed[i] = rest[i];
            posed[bone1] = new BonePose(new Vector3(1f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f));

            Vector3[] deformed = LinearBlendSkinning.Deform(rest, posed, restVertices, weights);
            // Proximal (bone0-rigid) stays; distal (bone1-rigid) moves to (1,0.5,0).
            Assert.AreEqual(0.5f, deformed[0].x, T);
            Assert.AreEqual(0f, deformed[0].y, T);
            Assert.AreEqual(1f, deformed[1].x, T);
            Assert.AreEqual(0.5f, deformed[1].y, T);
            Assert.That(deformed[0].y, Is.EqualTo(0f).Within(T),
                "proximal must not move under a distal bend");
            Assert.That(deformed[1].y, Is.EqualTo(0.5f).Within(T));
            Assert.That(deformed[1].y, Is.Not.EqualTo(restVertices[1].y).Within(T),
                "distal vertex must visibly move under the bend");
        }

        [Test]
        public void Author_BentChain_VertexInsideBendBindsToCorrectSegment_NotBoneCenter()
        {
            // Bent two-segment chain: [0,0,0]->[1,0,0]->[1.5,1,0]. A vertex on the
            // inside of the bend near the distal segment must follow the DISTAL bone,
            // which is only guaranteed by closest-point-on-segment distance (a
            // nearest-bone-CENTER rule would pull it toward the proximal joint).
            SkeletonSnapshot snapshot = Capture(
                SegmentBone("b_0", new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f)),
                SegmentBone("b_1", new Vector3(1f, 0f, 0f), new Vector3(1.5f, 1f, 0f)));
            int bone1 = snapshot.GetIndex("b_1");

            List<BoneSegmentInfluence> segments = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(
                snapshot, new float[] { 0.3f, 0.3f });

            // Surface vertex on the distal segment, on the inside of the 90-degree turn.
            Vector3 vertex = new Vector3(1.25f, 0.6f, 0f);
            // Nearest-point on distal segment [1,0,0]->[1.5,1,0]: param to (1.25,0.5,0) = 0.5
            // -> distance 0.1. Distance to proximal segment [0,0,0]->[1,0,0] is to endpoint
            // (1,0,0) = 0.6. The distal segment is clearly closer -> its bone dominates.
            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(segments, new[] { vertex });
            AssertStructural(snapshot, weights, 1);
            Assert.AreEqual(bone1, weights[0][0].BoneIndex,
                "inside-of-bend vertex must bind to the distal segment, not the nearest bone center");
            Assert.Greater(weights[0][0].Weight, 0.5f, "distal bone should dominate");
        }

        [Test]
        public void Author_BranchPoint_BlendsBodyAndTwoLimbChains()
        {
            // A branch origin shared by the body spine and two limbs. A vertex at the
            // branch is equidistant to all three segments and must blend across the
            // multiple chains (proving a junction does not snap to one bone).
            SkeletonSnapshot snapshot = Capture(
                SegmentBone("body_j0", new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f)),
                SegmentBone("legL_j0", new Vector3(0f, 0f, 0f), new Vector3(-1f, -1f, 0f)),
                SegmentBone("legR_j0", new Vector3(0f, 0f, 0f), new Vector3(1f, -1f, 0f)));
            int body = snapshot.GetIndex("body_j0");
            int left = snapshot.GetIndex("legL_j0");
            int right = snapshot.GetIndex("legR_j0");

            List<BoneSegmentInfluence> segments = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(
                snapshot, new float[] { 0.5f, 0.5f, 0.5f });
            Vector3 branchVertex = Vector3.zero;
            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(segments, new[] { branchVertex });
            AssertStructural(snapshot, weights, 1);

            Assert.AreEqual(3, weights[0].Length, "branch vertex blends three chains");
            Assert.AreEqual(1f, WeightSum(weights[0]), T);
            // All three bones present, none dominating.
            Assert.IsTrue(HasBone(weights[0], body), "body bone present at the branch");
            Assert.IsTrue(HasBone(weights[0], left), "left limb bone present at the branch");
            Assert.IsTrue(HasBone(weights[0], right), "right limb bone present at the branch");
            Assert.Less(weights[0][0].Weight, 0.5f, "no single chain may dominate a branch seam");
        }

        [Test]
        public void Author_MirroredLimb_MirroredSurfaceBindsToMirroredBone_NoIdentityRediscovery()
        {
            // Authored limb on the +X side and its reflected copy on the -X side
            // (positions placed with MirrorUtility reflection). A vertex on the
            // mirrored surface binds to the MIRRORED bone index; no mirror identity
            // is re-discovered here — the skeleton already holds both chains.
            Vector3 joint0 = new Vector3(0.5f, 0f, 0f);
            Vector3 joint1 = new Vector3(0.5f, -1f, 0f);
            Vector3 joint2 = new Vector3(0.5f, -2f, 0f);
            Vector3 mJoint0 = MirrorUtility.ReflectPointAcrossX(joint0);
            Vector3 mJoint1 = MirrorUtility.ReflectPointAcrossX(joint1);
            Vector3 mJoint2 = MirrorUtility.ReflectPointAcrossX(joint2);

            SkeletonSnapshot snapshot = Capture(
                SegmentBone("leg_j0", joint0, joint1),
                SegmentBone("leg_j1", joint1, joint2),
                SegmentBone("leg_j0_mirror", mJoint0, mJoint1, mirrored: true),
                SegmentBone("leg_j1_mirror", mJoint1, mJoint2, mirrored: true));
            int mirroredBone0 = snapshot.GetIndex("leg_j0_mirror");

            List<BoneSegmentInfluence> segments = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(
                snapshot, new float[] { 0.12f, 0.12f, 0.12f, 0.12f });

            // A surface vertex on the mirrored (negative-X) thigh.
            Vector3[] mirroredSurface = { new Vector3(-0.5f, -0.5f, 0f) };
            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(segments, mirroredSurface);
            AssertStructural(snapshot, weights, 1);
            Assert.AreEqual(1, weights[0].Length);
            Assert.AreEqual(mirroredBone0, weights[0][0].BoneIndex,
                "mirrored-surface vertex must bind to the mirrored bone index");
            Assert.AreEqual(1f, weights[0][0].Weight, T);

            // The chosen segment is genuinely the mirrored copy.
            bool foundMirrored = false;
            for (int s = 0; s < segments.Count; s++)
            {
                if (segments[s].BoneIndex == mirroredBone0) foundMirrored = segments[s].IsMirrored;
            }
            Assert.IsTrue(foundMirrored, "the influence primitive must be flagged mirrored");

            // Rest-pose equivalence for the mirrored surface binding.
            BonePose[] rest = RestFrames(snapshot);
            Vector3[] roundTripped = LinearBlendSkinning.Deform(rest, rest, mirroredSurface, weights);
            Assert.AreEqual(mirroredSurface[0].x, roundTripped[0].x, T);
            Assert.AreEqual(mirroredSurface[0].y, roundTripped[0].y, T);
        }

        [Test]
        public void Author_GeneratedBodySurface_AllVerticesValidAndDeterministic()
        {
            // A Body-like spine of chained sample bones with a surface vertex per
            // segment, off-axis as a real welded surface vertex would be.
            SkeletonSnapshot snapshot = Capture(
                SegmentBone("body_j0", new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f)),
                SegmentBone("body_j1", new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f)),
                SegmentBone("body_j2", new Vector3(2f, 0f, 0f), new Vector3(3f, 0f, 0f)),
                SegmentBone("body_j3", new Vector3(3f, 0f, 0f), new Vector3(4f, 0f, 0f)));

            List<BoneSegmentInfluence> segments = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(
                snapshot, new float[] { 0.5f, 0.5f, 0.5f, 0.5f });
            Assert.AreEqual(4, segments.Count, "all four spine bones carry segments");

            // Off-axis "surface" shell points: ~0.5 above each segment axis (radius 0.5).
            var restVertices = new List<Vector3>();
            for (float x = 0.25f; x <= 3.75f; x += 0.5f)
            {
                restVertices.Add(new Vector3(x, 0.55f, 0f));
                restVertices.Add(new Vector3(x, -0.55f, 0f));
            }

            VertexInfluence[][] first = ImplicitSurfaceWeightAuthoring.Author(segments, restVertices);
            VertexInfluence[][] second = ImplicitSurfaceWeightAuthoring.Author(segments, restVertices);
            AssertStructural(snapshot, first, restVertices.Count);
            AssertStructural(snapshot, second, restVertices.Count);

            // Deterministic under repeated generation.
            for (int v = 0; v < first.Length; v++)
            {
                Assert.AreEqual(first[v].Length, second[v].Length, $"vertex {v} influence count");
                for (int i = 0; i < first[v].Length; i++)
                {
                    Assert.AreEqual(first[v][i].BoneIndex, second[v][i].BoneIndex, $"vertex {v} influence {i} bone");
                    Assert.AreEqual(first[v][i].Weight, second[v][i].Weight, T, $"vertex {v} influence {i} weight");
                }
            }

            // Rest-pose equivalence for every surface vertex.
            BonePose[] rest = RestFrames(snapshot);
            Vector3[] roundTripped = LinearBlendSkinning.Deform(rest, rest, restVertices, first);
            for (int v = 0; v < restVertices.Count; v++)
            {
                Assert.AreEqual(restVertices[v].x, roundTripped[v].x, T);
                Assert.AreEqual(restVertices[v].y, roundTripped[v].y, T);
            }
        }

        [Test]
        public void Author_ResultIndependentOfSegmentOrdering_AndPerVertexLocality()
        {
            SkeletonSnapshot snapshot = Capture(
                SegmentBone("straight_0", new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f)),
                SegmentBone("straight_1", new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f)));
            float[] radii = { 0.12f, 0.12f };

            List<BoneSegmentInfluence> forward = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(snapshot, radii);
            var reversed = new List<BoneSegmentInfluence>();
            for (int s = forward.Count - 1; s >= 0; s--) reversed.Add(forward[s]);

            Vector3[] restVertices =
            {
                new Vector3(0.5f, 0f, 0f),
                new Vector3(1.0f, 0f, 0f),
                new Vector3(1.5f, 0f, 0f),
            };
            VertexInfluence[][] a = ImplicitSurfaceWeightAuthoring.Author(forward, restVertices);
            VertexInfluence[][] b = ImplicitSurfaceWeightAuthoring.Author(reversed, restVertices);
            for (int v = 0; v < restVertices.Length; v++)
            {
                Assert.AreEqual(a[v].Length, b[v].Length, $"vertex {v} count independent of segment order");
                for (int i = 0; i < a[v].Length; i++)
                {
                    Assert.AreEqual(a[v][i].BoneIndex, b[v][i].BoneIndex, $"vertex {v} order-independent bone");
                    Assert.AreEqual(a[v][i].Weight, b[v][i].Weight, T, $"vertex {v} order-independent weight");
                }
            }

            // Per-vertex locality: authoring one vertex at a time equals the bulk row,
            // so the result never depends on Unity/scene vertex iteration order.
            VertexInfluence[][] bulk = ImplicitSurfaceWeightAuthoring.Author(forward, restVertices);
            for (int v = 0; v < restVertices.Length; v++)
            {
                VertexInfluence[][] single = ImplicitSurfaceWeightAuthoring.Author(forward, new[] { restVertices[v] });
                Assert.AreEqual(bulk[v].Length, single[0].Length, $"vertex {v} locality count");
                for (int i = 0; i < bulk[v].Length; i++)
                {
                    Assert.AreEqual(bulk[v][i].BoneIndex, single[0][i].BoneIndex, $"vertex {v} locality bone");
                    Assert.AreEqual(bulk[v][i].Weight, single[0][i].Weight, T, $"vertex {v} locality weight");
                }
            }
        }

        [Test]
        public void BuildSegmentInfluences_BoneOrderingContract_StableAcrossInsertionOrder()
        {
            // The shared bind-index contract: SkeletonSnapshot.Capture orders bones by
            // BFS id-sort regardless of insertion order, and BuildSegmentInfluences maps
            // each bone to that same index. Two skeletons with the same bones inserted
            // differently must produce the same bone-index order (HasSameBoneOrder) and
            // identical influence geometry.
            Bone first = SegmentBone("straight_0", Vector3.zero, new Vector3(1f, 0f, 0f));
            Bone second = SegmentBone("straight_1", new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f));

            SkeletonSnapshot ordered = Capture(first, second);
            SkeletonSnapshot shuffled = Capture(second, first);
            Assert.IsTrue(ordered.HasSameBoneOrder(shuffled), "capture order is insertion-independent");

            Assert.AreEqual(ordered.GetIndex("straight_0"), shuffled.GetIndex("straight_0"));
            Assert.AreEqual(ordered.GetIndex("straight_1"), shuffled.GetIndex("straight_1"));

            List<BoneSegmentInfluence> a = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(
                ordered, new float[] { 0.12f, 0.12f });
            List<BoneSegmentInfluence> b = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(
                shuffled, new float[] { 0.12f, 0.12f });
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].BoneIndex, b[i].BoneIndex);
                Assert.AreEqual(a[i].Start, b[i].Start);
                Assert.AreEqual(a[i].End, b[i].End);
            }
        }

        // ---- Total / defensive behavior -----------------------------------------

        [Test]
        public void Author_NoEligibleInfluence_ThrowsDomainException()
        {
            SkeletonSnapshot snapshot = Capture(
                SegmentBone("straight_0", Vector3.zero, new Vector3(1f, 0f, 0f)));
            List<BoneSegmentInfluence> segments = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(
                snapshot, new float[] { 0.1f });
            // A vertex far beyond every segment's influence range.
            Assert.Throws<DomainException>(() =>
                ImplicitSurfaceWeightAuthoring.Author(segments, new[] { new Vector3(100f, 0f, 0f) }));
        }

        [Test]
        public void Author_NonFiniteVertexOrNonPositiveRadius_ThrowsDomainException()
        {
            SkeletonSnapshot snapshot = Capture(
                SegmentBone("straight_0", Vector3.zero, new Vector3(1f, 0f, 0f)));
            List<BoneSegmentInfluence> segments = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(
                snapshot, new float[] { 0.1f });

            Assert.Throws<DomainException>(() =>
                ImplicitSurfaceWeightAuthoring.Author(segments,
                    new[] { new Vector3(float.NaN, 0f, 0f) }));

            var bad = new List<BoneSegmentInfluence> { new BoneSegmentInfluence(0, false, Vector3.zero, Vector3.one, 0f) };
            Assert.Throws<DomainException>(() =>
                ImplicitSurfaceWeightAuthoring.Author(bad, new[] { new Vector3(0.5f, 0f, 0f) }));
        }
    }
}
