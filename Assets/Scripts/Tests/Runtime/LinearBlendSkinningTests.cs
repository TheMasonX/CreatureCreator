using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Two-segment binding fixture and proofs for the geometry binding contract
    /// (TSK-0077 / CC-073, C4). Chain: bone 0 -> bone 1 -> terminal, all explicit
    /// numeric data — no procedural noise. The rest mesh lies along +X with the
    /// proximal joint at the creature origin:
    ///
    ///   bone0: origin, rest identity   (segment [0,1])
    ///   bone1: (1,0,0), rest identity  (segment [1,2], terminal at (2,0,0))
    ///
    /// Rest vertices (creature space) and bind weights:
    ///   A (0.5,0,0)  -> bone0 w=1.0            (proximal, rigid to bone0)
    ///   D (0.8,0,0)  -> bone0 w=0.6 / bone1 0.4 (soft blend across the joint)
    ///   B (1.0,0,0)  -> bone0 w=0.5 / bone1 0.5 (the shared joint, 50/50)
    ///   C (1.5,0,0)  -> bone1 w=1.0            (distal)
    ///   T (2.0,0,0)  -> bone1 w=1.0            (the terminal tip)
    ///
    /// Posed bend: bone1 keeps its joint at (1,0,0) and rotates +90° about Z, so the
    /// distal segment and terminal point +Y. Expected posed vertices (hand-computed):
    ///   A (0.5,0,0), D (0.88,-0.08,0), B (1,0,0), C (1,0.5,0), T (1,1,0).
    /// </summary>
    [TestFixture]
    public class LinearBlendSkinningTests
    {
        private const float T = 1e-3f;

        private static readonly BonePose Bone0Rest = new BonePose(Vector3.zero, Quaternion.identity);
        private static readonly BonePose Bone1Rest = new BonePose(new Vector3(1f, 0f, 0f), Quaternion.identity);
        private static readonly BonePose[] RestAligned = { Bone0Rest, Bone1Rest };

        // A, D, B, C, T rest positions.
        private static readonly Vector3[] RestVertices =
        {
            new Vector3(0.5f, 0f, 0f),
            new Vector3(0.8f, 0f, 0f),
            new Vector3(1.0f, 0f, 0f),
            new Vector3(1.5f, 0f, 0f),
            new Vector3(2.0f, 0f, 0f),
        };

        private static readonly IReadOnlyList<IReadOnlyList<VertexInfluence>> RestBindings = new[]
        {
            new[] { new VertexInfluence(0, 1.0f) },
            new[] { new VertexInfluence(0, 0.6f), new VertexInfluence(1, 0.4f) },
            new[] { new VertexInfluence(0, 0.5f), new VertexInfluence(1, 0.5f) },
            new[] { new VertexInfluence(1, 1.0f) },
            new[] { new VertexInfluence(1, 1.0f) },
        };

        // bone1 rotated +90 about Z at its joint -> distal + terminal point +Y.
        private static readonly BonePose[] PosedBend =
        {
            new BonePose(Vector3.zero, Quaternion.identity),
            new BonePose(new Vector3(1f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f)),
        };

        private static readonly Vector3[] ExpectedBend =
        {
            new Vector3(0.5f, 0f, 0f),
            new Vector3(0.88f, -0.08f, 0f),
            new Vector3(1.0f, 0f, 0f),
            new Vector3(1.0f, 0.5f, 0f),
            new Vector3(1.0f, 1.0f, 0f),
        };

        [Test]
        public void Deform_RestPoseRoundTrip_ReproducesRestVertices()
        {
            // Rest mesh -> binding -> apply REST pose must reproduce the mesh.
            Vector3[] posed = LinearBlendSkinning.Deform(RestAligned, RestAligned, RestVertices, RestBindings);
            AssertVectorArraysEqual(RestVertices, posed, T, "rest round trip");
        }

        [Test]
        public void Deform_RotatedRestRoundTrip_ReproducesRestVertices()
        {
            // Non-identity rest rotations prove the inverse-rest bind path is exact,
            // not just the identity-rotation special case of the aligned fixture.
            var rest = new[]
            {
                new BonePose(new Vector3(0f, 0f, 0f), Quaternion.Euler(15f, 20f, 0f)),
                new BonePose(new Vector3(1f, 0f, 0f), Quaternion.Euler(0f, 0f, -30f)),
            };
            Vector3[] posed = LinearBlendSkinning.Deform(rest, rest, RestVertices, RestBindings);
            AssertVectorArraysEqual(
                RestVertices, posed, LinearBlendSkinning.RestRoundTripTolerance, "rotated rest round trip");
        }

        [Test]
        public void Deform_PosedBend_MovesVerticesToExpectedPositions()
        {
            Vector3[] posed = LinearBlendSkinning.Deform(RestAligned, PosedBend, RestVertices, RestBindings);
            AssertVectorArraysEqual(ExpectedBend, posed, T, "posed bend");

            // Acceptance guard: numerical movement must be real, not a no-op pass.
            Assert.That(posed[4].y, Is.EqualTo(1.0f).Within(T),
                "the terminal tip must visibly move up under the bend");
            Assert.That(posed[4].y, Is.Not.EqualTo(RestVertices[4].y).Within(T),
                "posed vertex must differ from the rest vertex, not merely stay put");
        }

        [Test]
        public void Deform_ReapplyRestAfterPose_RestoresOriginalVerticesDeterministically()
        {
            // Bend, then return to rest: vertices come back to the authored mesh.
            Vector3[] posed = LinearBlendSkinning.Deform(RestAligned, PosedBend, RestVertices, RestBindings);
            AssertVectorArraysEqual(ExpectedBend, posed, T, "posed before return");

            Vector3[] restored = LinearBlendSkinning.Deform(RestAligned, RestAligned, RestVertices, RestBindings);
            AssertVectorArraysEqual(RestVertices, restored, T, "returned to rest");
        }

        [Test]
        public void Deform_PureTranslation_TranslatesWholeLimbUniformly()
        {
            Vector3 offset = new Vector3(0f, 0f, 5f);
            var posed = new[]
            {
                new BonePose(Bone0Rest.Position + offset, Bone0Rest.Rotation),
                new BonePose(Bone1Rest.Position + offset, Bone1Rest.Rotation),
            };
            Vector3[] result = LinearBlendSkinning.Deform(RestAligned, posed, RestVertices, RestBindings);
            for (int i = 0; i < RestVertices.Length; i++)
            {
                AssertVectorEqual(RestVertices[i] + offset, result[i], $"vertex {i}");
            }
        }

        [Test]
        public void Deform_UnnormalizedWeights_AreNormalizedToTheSameBlend()
        {
            // Doubling every weight must not change the blend (defensive normalization).
            var scaledBindings = new[]
            {
                new[] { new VertexInfluence(0, 2.0f) },
                new[] { new VertexInfluence(0, 1.2f), new VertexInfluence(1, 0.8f) },
                new[] { new VertexInfluence(0, 1.0f), new VertexInfluence(1, 1.0f) },
                new[] { new VertexInfluence(1, 2.0f) },
                new[] { new VertexInfluence(1, 2.0f) },
            };
            Vector3[] normalized = LinearBlendSkinning.Deform(RestAligned, PosedBend, RestVertices, RestBindings);
            Vector3[] scaled = LinearBlendSkinning.Deform(RestAligned, PosedBend, RestVertices, scaledBindings);
            AssertVectorArraysEqual(normalized, scaled, T, "scaled weights normalize to the unit blend");
        }

        [Test]
        public void Deform_MirroredConfiguration_EqualsReflectionOfUnmirroredResult()
        {
            // Mirror convention: mirrored geometry flows through MirrorUtility (existing
            // shared reflection math). Deformation must commute with that reflection.
            // (Full mirrored-morphology proof over a whole CreatureDefinition is deferred.)
            Vector3[] unmirrored = LinearBlendSkinning.Deform(RestAligned, PosedBend, RestVertices, RestBindings);

            BonePose[] mirroredRest = ReflectBones(RestAligned);
            BonePose[] mirroredPosed = ReflectBones(PosedBend);
            Vector3[] mirroredVertices = ReflectPoints(RestVertices);

            Vector3[] mirrored = LinearBlendSkinning.Deform(
                mirroredRest, mirroredPosed, mirroredVertices, RestBindings);

            AssertVectorArraysEqual(
                ReflectPoints(unmirrored), mirrored, T, "deform commutes with X-plane reflection");
        }

        [Test]
        public void Deform_MirroredRestRoundTrip_ReproducesMirroredRestVertices()
        {
            BonePose[] mirroredRest = ReflectBones(RestAligned);
            Vector3[] mirroredVertices = ReflectPoints(RestVertices);
            Vector3[] posed = LinearBlendSkinning.Deform(
                mirroredRest, mirroredRest, mirroredVertices, RestBindings);
            AssertVectorArraysEqual(mirroredVertices, posed, T, "mirrored rest round trip");
        }

        [Test]
        public void Deform_ResolvedMirroredLimb_RestAndPosedResultsReflectBySemanticIdentity()
        {
            CreatureDefinition definition = CreateResolvedMirroredLimbDefinition();
            ResolvedCreatureSnapshot resolved = ResolvedCreatureSnapshot.Resolve(definition);
            Assert.IsTrue(resolved.TryGetPart("part_leg", out ResolvedPartSnapshot limb));

            ProceduralCreature.Skeleton.Skeleton inferred = SkeletonInferrer.Infer(definition);
            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(inferred);
            CreaturePart sourcePart = definition.FindPart("part_leg");
            string[] sourceIds =
            {
                SemanticBoneResolver.ResolveLimbSegmentBoneId(sourcePart, 0, false),
                SemanticBoneResolver.ResolveLimbSegmentBoneId(sourcePart, 1, false),
            };
            string[] mirroredIds =
            {
                SemanticBoneResolver.ResolveLimbSegmentBoneId(sourcePart, 0, true),
                SemanticBoneResolver.ResolveLimbSegmentBoneId(sourcePart, 1, true),
            };

            int[] sourceIndices = ResolveSemanticIndices(snapshot, inferred, sourceIds, mirrored: false);
            int[] mirroredIndices = ResolveSemanticIndices(snapshot, inferred, mirroredIds, mirrored: true);
            Assert.That(sourceIndices[0], Is.Not.EqualTo(mirroredIndices[0]));
            Assert.That(sourceIndices[1], Is.Not.EqualTo(mirroredIndices[1]));

            Vector3[] sourceVertices = ResolvedLimbVertices(limb);
            Vector3[] mirroredVertices = ReflectPoints(sourceVertices);
            IReadOnlyList<IReadOnlyList<VertexInfluence>> sourceBindings =
                SegmentBindings(sourceIndices);
            IReadOnlyList<IReadOnlyList<VertexInfluence>> mirroredBindings =
                SegmentBindings(mirroredIndices);

            BonePose[] rest = SnapshotPoses(snapshot);
            BonePose[] mirroredRest = SnapshotPoses(snapshot);
            Vector3[] sourceAtRest = LinearBlendSkinning.Deform(
                rest, rest, sourceVertices, sourceBindings);
            Vector3[] mirroredAtRest = LinearBlendSkinning.Deform(
                mirroredRest, mirroredRest, mirroredVertices, mirroredBindings);
            AssertVectorArraysEqual(sourceVertices, sourceAtRest, T, "resolved source rest round trip");
            AssertVectorArraysEqual(mirroredVertices, mirroredAtRest, T, "resolved mirrored rest round trip");

            BonePose[] sourcePosed = (BonePose[])rest.Clone();
            Quaternion sourcePoseDelta = Quaternion.AngleAxis(47f, Vector3.right);
            sourcePosed[sourceIndices[1]] = new BonePose(
                rest[sourceIndices[1]].Position,
                rest[sourceIndices[1]].Rotation * sourcePoseDelta);
            BonePose[] mirroredPosed = SnapshotPoses(snapshot);
            mirroredPosed[mirroredIndices[1]] = ReflectPose(sourcePosed[sourceIndices[1]]);

            Vector3[] sourceResult = LinearBlendSkinning.Deform(
                rest, sourcePosed, sourceVertices, sourceBindings);
            Vector3[] mirroredResult = LinearBlendSkinning.Deform(
                mirroredRest, mirroredPosed, mirroredVertices, mirroredBindings);
            AssertVectorArraysEqual(
                ReflectPoints(sourceResult), mirroredResult, T,
                "resolved mirrored posed result reflects the source result");
            Assert.That(sourceResult[2].y, Is.Not.EqualTo(sourceVertices[2].y).Within(T),
                "resolved source terminal must move under the posed segment rotation");
        }

        [Test]
        public void Deform_BoneCountMismatch_Throws()
        {
            var posed = new[] { new BonePose(Vector3.zero, Quaternion.identity) };
            Assert.Throws<ProceduralCreature.Common.DomainException>(() =>
                LinearBlendSkinning.Deform(RestAligned, posed, RestVertices, RestBindings));
        }

        [Test]
        public void Deform_EmptyBindingsForAVertex_Throws()
        {
            var empty = new IReadOnlyList<VertexInfluence>[]
            {
                new[] { new VertexInfluence(0, 1f) },
                Array.Empty<VertexInfluence>(),
                new[] { new VertexInfluence(0, 1f) },
                new[] { new VertexInfluence(1, 1f) },
                new[] { new VertexInfluence(1, 1f) },
            };
            Assert.Throws<ProceduralCreature.Common.DomainException>(() =>
                LinearBlendSkinning.Deform(RestAligned, RestAligned, RestVertices, empty));
        }

        [Test]
        public void Deform_BoneIndexOutOfRange_Throws()
        {
            var bindings = new[]
            {
                new[] { new VertexInfluence(0, 1f) },
                new[] { new VertexInfluence(0, 1f) },
                new[] { new VertexInfluence(0, 1f) },
                new[] { new VertexInfluence(99, 1f) },
                new[] { new VertexInfluence(0, 1f) },
            };
            Assert.Throws<ProceduralCreature.Common.DomainException>(() =>
                LinearBlendSkinning.Deform(RestAligned, RestAligned, RestVertices, bindings));
        }

        [Test]
        public void Deform_NegativeWeight_Throws()
        {
            var bindings = new[]
            {
                new[] { new VertexInfluence(0, 1f) },
                new[] { new VertexInfluence(0, -0.5f), new VertexInfluence(1, 1f) },
                new[] { new VertexInfluence(0, 1f) },
                new[] { new VertexInfluence(0, 1f) },
                new[] { new VertexInfluence(0, 1f) },
            };
            Assert.Throws<ProceduralCreature.Common.DomainException>(() =>
                LinearBlendSkinning.Deform(RestAligned, RestAligned, RestVertices, bindings));
        }

        [Test]
        public void Deform_ZeroTotalWeight_Throws()
        {
            var bindings = new[]
            {
                new[] { new VertexInfluence(0, 1f) },
                new[] { new VertexInfluence(0, 0f), new VertexInfluence(1, 0f) },
                new[] { new VertexInfluence(0, 1f) },
                new[] { new VertexInfluence(0, 1f) },
                new[] { new VertexInfluence(0, 1f) },
            };
            Assert.Throws<ProceduralCreature.Common.DomainException>(() =>
                LinearBlendSkinning.Deform(RestAligned, RestAligned, RestVertices, bindings));
        }

        private static BonePose[] ReflectBones(IReadOnlyList<BonePose> bones)
        {
            var reflected = new BonePose[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                reflected[i] = new BonePose(
                    MirrorUtility.ReflectPointAcrossX(bones[i].Position),
                    MirrorUtility.MirrorAcrossXPlane(
                        Matrix4x4.TRS(Vector3.zero, bones[i].Rotation, Vector3.one)).rotation);
            }
            return reflected;
        }

        private static CreatureDefinition CreateResolvedMirroredLimbDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = Vector3.zero,
                Radius = 1f,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_leg",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = new TransformData
                {
                    Position = new Vector3(0.7f, -0.2f, 0.15f),
                    Rotation = Quaternion.Euler(8f, 17f, 11f),
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
                Limb = new LimbChain
                {
                    Joints =
                    {
                        new LimbJoint { Id = 1, Position = Vector3.zero },
                        new LimbJoint { Id = 2, Position = new Vector3(0.35f, 0.3f, 0.1f) },
                        new LimbJoint { Id = 3, Position = new Vector3(0.75f, 0.45f, 0.25f) },
                    },
                },
            });
            return definition;
        }

        private static int[] ResolveSemanticIndices(
            SkeletonSnapshot snapshot, ProceduralCreature.Skeleton.Skeleton skeleton,
            IReadOnlyList<string> ids, bool mirrored)
        {
            var indices = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                indices[i] = snapshot.GetIndex(ids[i]);
                Bone bone = skeleton.FindBone(ids[i]);
                Assert.IsNotNull(bone, $"resolved semantic bone '{ids[i]}' must exist");
                Assert.AreEqual("part_leg", bone.SourcePartId);
                Assert.AreEqual(mirrored, bone.IsMirrored);
            }
            return indices;
        }

        private static Vector3[] ResolvedLimbVertices(ResolvedPartSnapshot limb)
        {
            Matrix4x4 frame = limb.PartFrameToCreatureSpace;
            Vector3 root = limb.Limb.JointPositions[0];
            Vector3 midpoint = Vector3.Lerp(limb.Limb.JointPositions[0], limb.Limb.JointPositions[1], 0.55f);
            Vector3 terminal = limb.Limb.JointPositions[limb.Limb.JointPositions.Count - 1];
            return new[]
            {
                frame.MultiplyPoint3x4(root),
                frame.MultiplyPoint3x4(midpoint),
                frame.MultiplyPoint3x4(terminal),
            };
        }

        private static IReadOnlyList<IReadOnlyList<VertexInfluence>> SegmentBindings(int[] indices)
        {
            return new IReadOnlyList<VertexInfluence>[]
            {
                new[] { new VertexInfluence(indices[0], 1f) },
                new[] { new VertexInfluence(indices[0], 0.35f), new VertexInfluence(indices[1], 0.65f) },
                new[] { new VertexInfluence(indices[1], 1f) },
            };
        }

        private static BonePose[] SnapshotPoses(SkeletonSnapshot snapshot)
        {
            var poses = new BonePose[snapshot.Count];
            for (int i = 0; i < snapshot.Count; i++)
            {
                poses[i] = new BonePose(snapshot[i].Position, snapshot[i].Rotation);
            }
            return poses;
        }

        private static BonePose ReflectPose(BonePose pose)
        {
            return new BonePose(
                MirrorUtility.ReflectPointAcrossX(pose.Position),
                MirrorUtility.MirrorAcrossXPlane(
                    Matrix4x4.TRS(Vector3.zero, pose.Rotation, Vector3.one)).rotation);
        }

        private static Vector3[] ReflectPoints(IReadOnlyList<Vector3> points)
        {
            var reflected = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                reflected[i] = MirrorUtility.ReflectPointAcrossX(points[i]);
            }
            return reflected;
        }

        private static void AssertVectorArraysEqual(
            IReadOnlyList<Vector3> expected, IReadOnlyList<Vector3> actual, float tolerance, string context)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Count, Is.EqualTo(expected.Count), $"{context}: vertex count");
            for (int i = 0; i < expected.Count; i++)
            {
                AssertVectorEqual(expected[i], actual[i], $"{context}: vertex {i}");
            }
        }

        private static void AssertVectorEqual(Vector3 expected, Vector3 actual, string context)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(T), $"{context} x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(T), $"{context} y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(T), $"{context} z");
        }
    }
}
