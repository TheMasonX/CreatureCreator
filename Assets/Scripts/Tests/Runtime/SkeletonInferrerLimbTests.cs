using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Phase 6 (CC-018) skeleton inference for limb parts: N joints → N-1 bones,
    /// positions from the shared world resolver, rotations along segments,
    /// terminal-bone child attachment, and mirrored chains. Runtime assembly —
    /// invoke via execute_code, not the MCP runner.
    /// </summary>
    [TestFixture]
    public class SkeletonInferrerLimbTests
    {
        private static LimbChain Chain(params Vector3[] positions)
        {
            var chain = new LimbChain();
            for (int i = 0; i < positions.Length; i++)
            {
                chain.Joints.Add(new LimbJoint { Id = (uint)(i + 1), Position = positions[i] });
            }
            return chain;
        }

        private static CreaturePart MakeLimbPart(string id, PartType type, LimbChain chain,
            string parentId = null, Vector3? localPosition = null, bool mirror = false)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = parentId,
                PartType = type,
                Transform = new TransformData
                {
                    Position = localPosition ?? Vector3.zero,
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = mirror,
                Limb = chain,
            };
        }

        private static CreaturePart MakePlainPart(string id, PartType type, Vector3 localPosition,
            string parentId = null, bool mirror = false)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = parentId,
                PartType = type,
                Transform = new TransformData
                {
                    Position = localPosition,
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = mirror,
            };
        }

        private static CreatureDefinition DefinitionWith(CreaturePart body, params CreaturePart[] parts)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(body);
            foreach (CreaturePart part in parts) definition.AddPart(part);
            return definition;
        }

        [Test]
        public void Infer_LimbWithTwoJoints_ProducesOneBoneAtRootJoint()
        {
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakeLimbPart("part_leg", PartType.Leg,
                    Chain(new Vector3(0f, 0f, 0f), new Vector3(0f, -1f, 0f)),
                    parentId: "part_body"));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(3, skeleton.Bones.Count, "body + 1 leg segment + terminal joint");
            Bone bone = skeleton.FindBone("part_leg_j0");
            Assert.IsNotNull(bone);
            Assert.AreEqual("part_body", bone.ParentBoneId, "Bone 0 attaches through the normal parent rule.");
            Assert.AreEqual(Vector3.zero, bone.Position);
            Assert.AreEqual(PartType.Leg, bone.PartType);
            Assert.IsFalse(bone.IsMirrored);
        }

        [Test]
        public void Infer_LimbWithFourJoints_ProducesThreeBonesParentedInChain()
        {
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakeLimbPart("part_leg", PartType.Leg,
                    Chain(new Vector3(0f, 0f, 0f), new Vector3(0f, -1f, 0f), new Vector3(0.5f, -1f, 0f), new Vector3(0.5f, -2f, 0f)),
                    parentId: "part_body"));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(5, skeleton.Bones.Count, "body + 3 leg segments + terminal joint");
            Bone b0 = skeleton.FindBone("part_leg_j0");
            Bone b1 = skeleton.FindBone("part_leg_j1");
            Bone b2 = skeleton.FindBone("part_leg_j2");
            Bone terminal = skeleton.FindBone("part_leg_j3");
            Assert.IsNotNull(b0);
            Assert.IsNotNull(b1);
            Assert.IsNotNull(b2);
            Assert.IsNotNull(terminal);
            Assert.AreEqual("part_body", b0.ParentBoneId);
            Assert.AreEqual("part_leg_j0", b1.ParentBoneId);
            Assert.AreEqual("part_leg_j1", b2.ParentBoneId);
            Assert.AreEqual("part_leg_j2", terminal.ParentBoneId);
            Assert.AreEqual(new Vector3(0.5f, -2f, 0f), terminal.Position);
        }

        [Test]
        public void Infer_LimbBonePositionsMatchResolverForEachJoint()
        {
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, new Vector3(1f, 0f, 0f)),
                MakeLimbPart("part_arm", PartType.Arm,
                    Chain(new Vector3(0f, 0f, 0f), new Vector3(0f, -1f, 0f), new Vector3(0.8f, -1f, 0f)),
                    parentId: "part_body", localPosition: new Vector3(0.5f, 0f, 0f)));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Bone b0 = skeleton.FindBone("part_arm_j0");
            Bone b1 = skeleton.FindBone("part_arm_j1");
            Assert.IsNotNull(b0);
            Assert.IsNotNull(b1);

            CreaturePart arm = definition.FindPart("part_arm");
            Matrix4x4 world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, arm);

            Assert.Less(Vector3.Distance(world.MultiplyPoint3x4(new Vector3(0f, 0f, 0f)), b0.Position), 1e-4f);
            Assert.Less(Vector3.Distance(world.MultiplyPoint3x4(new Vector3(0f, -1f, 0f)), b1.Position), 1e-4f);
        }

        [Test]
        public void Infer_LimbBoneRotationPointsAlongSegment()
        {
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakeLimbPart("part_arm", PartType.Arm,
                    Chain(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 2f))));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Bone b0 = skeleton.FindBone("part_arm_j0");
            Bone b1 = skeleton.FindBone("part_arm_j1");

            Assert.Greater(Vector3.Dot(b0.Rotation * Vector3.forward, Vector3.forward), 0.999f,
                "Bone 0 points along the first segment (world +Z).");
            Assert.Greater(Vector3.Dot(b1.Rotation * Vector3.forward, new Vector3(0f, 1f, 1f).normalized), 0.999f,
                "Bone 1 points along the second (diagonal) segment.");
        }

        [Test]
        public void Infer_VerticalLimb_ProducesFiniteLookRotation()
        {
            // The default chain is vertical down; segmentDir is anti-parallel to
            // the part's world up, which would degenerate a naive LookRotation.
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakeLimbPart("part_leg", PartType.Leg, Chain(new Vector3(0f, 0f, 0f), new Vector3(0f, -1f, 0f))));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Bone bone = skeleton.FindBone("part_leg_j0");
            Assert.IsNotNull(bone);
            Assert.IsTrue(IsFinite(bone.Rotation), "A vertical limb must not produce NaN/Inf rotation.");
            Assert.Greater(Vector3.Dot(bone.Rotation * Vector3.forward, Vector3.down), 0.999f,
                "The bone must still point down even when the up hint was degenerate.");
        }

        [Test]
        public void Infer_MirroredLimb_EmitsFullMirroredChainAcrossX()
        {
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakeLimbPart("part_leg", PartType.Leg,
                    Chain(new Vector3(0f, 0f, 0f), new Vector3(0.5f, -1f, 0f), new Vector3(0.5f, -1.5f, 0f), new Vector3(0.5f, -2f, 0f)),
                    parentId: "part_body", localPosition: new Vector3(0.5f, 0f, 0f), mirror: true));
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(9, skeleton.Bones.Count, "body + 4 leg segment/joint nodes + 4 mirrored leg segment/joint nodes");
            for (int i = 0; i < 4; i++)
            {
                Bone original = skeleton.FindBone("part_leg_j" + i);
                Bone mirrored = skeleton.FindBone("part_leg_j" + i + SkeletonInferrer.MirrorSuffix);
                Assert.IsNotNull(original, $"Original bone _j{i} missing.");
                Assert.IsNotNull(mirrored, $"Mirrored bone _j{i}_mirror missing.");
                Assert.AreEqual(-original.Position.x, mirrored.Position.x, 1e-4f,
                    $"Mirrored bone {i} must reflect across the creature X plane.");
                Assert.AreEqual(original.Position.y, mirrored.Position.y, 1e-4f);
                Assert.AreEqual(original.Position.z, mirrored.Position.z, 1e-4f);
            }

            // The mirrored chain must be internally parented, not attached to the
            // original chain's bones.
            Assert.AreEqual("part_body", skeleton.FindBone("part_leg_j0_mirror").ParentBoneId);
            Assert.AreEqual("part_leg_j0_mirror", skeleton.FindBone("part_leg_j1_mirror").ParentBoneId);
            Assert.AreEqual("part_leg_j1_mirror", skeleton.FindBone("part_leg_j2_mirror").ParentBoneId);
            Assert.AreEqual("part_leg_j2_mirror", skeleton.FindBone("part_leg_j3_mirror").ParentBoneId);
        }

        [Test]
        public void Infer_MirroredLimbWithRotatedTransform_MirrorsBoneForwardAndUpAxes()
        {
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakeLimbPart("part_leg", PartType.Leg,
                    Chain(new Vector3(0f, 0f, 0f), new Vector3(0.7f, -1f, 0.2f)),
                    parentId: "part_body", localPosition: new Vector3(0.8f, 0.2f, 0.1f), mirror: true));
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.FindPart("part_leg").Transform.Rotation = Quaternion.Euler(20f, 35f, 15f);

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);
            Bone original = skeleton.FindBone("part_leg_j0");
            Bone mirrored = skeleton.FindBone("part_leg_j0_mirror");
            Vector3 reflect = new Vector3(-1f, 1f, 1f);

            Assert.Greater(Vector3.Dot(
                Vector3.Scale(original.Rotation * Vector3.forward, reflect).normalized,
                mirrored.Rotation * Vector3.forward), 0.999f);
            Assert.Greater(Vector3.Dot(
                Vector3.Scale(original.Rotation * Vector3.up, reflect).normalized,
                mirrored.Rotation * Vector3.up), 0.999f);
        }

        [Test]
        public void Infer_ChildOfLimb_AttachesToTerminalBone()
        {
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakeLimbPart("part_leg", PartType.Leg,
                    Chain(new Vector3(0f, 0f, 0f), new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f)),
                    parentId: "part_body"),
                // CC-018 (child-at-tip frame): a child authored at identity sits at
                // the limb's terminal joint — the child's local space IS the tip.
                MakePlainPart("part_foot", PartType.Foot, Vector3.zero, parentId: "part_leg"));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            // 3 joints -> 2 segment bones plus terminal joint _j2; the child
            // attaches to the terminal joint, NOT to the part id, and its bone sits at
            // the terminal joint's creature-space position.
            Bone foot = skeleton.FindBone("part_foot");
            Assert.AreEqual("part_leg_j2", foot.ParentBoneId);

            Matrix4x4 legWorld =
                CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, definition.FindPart("part_leg"));
            Vector3 terminalJointWorld = legWorld.MultiplyPoint3x4(new Vector3(0f, -2f, 0f));
            Assert.Less(Vector3.Distance(terminalJointWorld, foot.Position), 1e-4f,
                "A child at identity under a limb sits at the limb's tip.");
        }

        [Test]
        public void Infer_MirroredChildOfMirroredLimb_AttachesToMirroredTerminalBone()
        {
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakeLimbPart("part_leg", PartType.Leg,
                    Chain(new Vector3(0f, 0f, 0f), new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f)),
                    parentId: "part_body", localPosition: new Vector3(0.5f, 0f, 0f), mirror: true),
                MakePlainPart("part_foot", PartType.Foot, Vector3.zero, parentId: "part_leg", mirror: true));
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Bone mirroredFoot = skeleton.FindBone("part_foot" + SkeletonInferrer.MirrorSuffix);
            Assert.IsNotNull(mirroredFoot);
            Assert.AreEqual("part_leg_j2" + SkeletonInferrer.MirrorSuffix, mirroredFoot.ParentBoneId,
                "A mirrored child of a mirrored limb attaches to the mirrored terminal bone.");
        }

        [Test]
        public void Infer_UnmirroredChildOfMirroredLimb_AttachesToUnmirroredTerminalBone()
        {
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakeLimbPart("part_leg", PartType.Leg,
                    Chain(new Vector3(0f, 0f, 0f), new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f)),
                    parentId: "part_body", localPosition: new Vector3(0.5f, 0f, 0f), mirror: true),
                MakePlainPart("part_foot", PartType.Foot, Vector3.zero, parentId: "part_leg", mirror: false));
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            // Only one foot bone exists (the foot is not mirrored), and it must
            // attach to the UNmirrored terminal bone (there is no mirrored chain
            // to attach to beyond the leg's mirrored bones).
            Assert.AreEqual(8, skeleton.Bones.Count, "body + mirrored leg chains + foot x1");
            Bone foot = skeleton.FindBone("part_foot");
            Assert.AreEqual("part_leg_j2", foot.ParentBoneId);
        }

        [Test]
        public void Infer_LimbUnderLimb_AttachesToParentTerminalBone()
        {
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakeLimbPart("part_arm", PartType.Arm,
                    Chain(new Vector3(0f, 0f, 0f), new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f)),
                    parentId: "part_body"),
                MakeLimbPart("part_hand", PartType.Hand,
                    Chain(new Vector3(0f, 0f, 0f), new Vector3(0f, -0.3f, 0f)),
                    parentId: "part_arm"));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Bone handBone0 = skeleton.FindBone("part_hand_j0");
            Bone wrist = skeleton.FindBone("part_arm_j2");
            Assert.IsNotNull(handBone0);
            Assert.IsNotNull(wrist, "The arm's authored terminal joint must be an explicit wrist node.");
            Assert.AreEqual("part_arm_j2", handBone0.ParentBoneId,
                "A limb child of a limb attaches to the parent's terminal joint node.");
        }

        [Test]
        public void Infer_ExistingNonLimbBehaviorUnchanged()
        {
            // The non-limb path must keep producing exactly one bone per part.
            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakePlainPart("part_leg", PartType.Leg, new Vector3(1f, -1f, 0f), parentId: "part_body"));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(2, skeleton.Bones.Count);
            Assert.IsNotNull(skeleton.FindBone("part_leg"));
            Assert.IsNull(skeleton.FindBone("part_leg_j0"), "A non-limb part must not emit joint bones.");
        }

        [Test]
        public void Infer_LimbWithNullJoint_DoesNotThrowAndEmitsNoBones()
        {
            // CC-056A increment 2: the limb is consumed through ResolvedLimb, so a
            // structurally broken chain (a null joint) resolves to nothing instead
            // of emitting partial bones. Inference stays total for direct calls.
            LimbChain chain = Chain(new Vector3(0f, 0f, 0f), new Vector3(0f, -1f, 0f));
            chain.Joints.Add(null);

            var definition = DefinitionWith(
                MakePlainPart("part_body", PartType.Body, Vector3.zero),
                MakeLimbPart("part_leg", PartType.Leg, chain, parentId: "part_body"));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(1, skeleton.Bones.Count,
                "The broken limb resolves to nothing; only the body bone remains.");
            Assert.IsNull(skeleton.FindBone("part_leg_j0"));
        }

        private static bool IsFinite(Quaternion q)
        {
            return !float.IsNaN(q.x) && !float.IsNaN(q.y) && !float.IsNaN(q.z) && !float.IsNaN(q.w)
                   && !float.IsInfinity(q.x) && !float.IsInfinity(q.y) && !float.IsInfinity(q.z) && !float.IsInfinity(q.w);
        }
    }
}
