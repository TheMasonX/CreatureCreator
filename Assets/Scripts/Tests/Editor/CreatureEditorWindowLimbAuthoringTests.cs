using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// CC-018 Phase 7 editor authoring helpers: the pure LimbAuthoring math
    /// (default-chain seeding, chain resize, joint ids, bounds clamping, world↔local
    /// handle geometry) and the ThicknessCurveAdapter (profile ↔ linear curve).
    /// The window's SceneView drag itself is a manual residual check — the MCP
    /// bridge cannot simulate SceneView interaction — so these tests cover every
    /// pure helper the gesture and inspector call into. Editor assembly (runs via
    /// the MCP test runner).
    /// </summary>
    [TestFixture]
    public class CreatureEditorWindowLimbAuthoringTests
    {
        [Test]
        public void IsLimbChainType_TrueForLimbLegArm()
        {
            Assert.IsTrue(LimbAuthoring.IsLimbChainType(PartType.Limb));
            Assert.IsTrue(LimbAuthoring.IsLimbChainType(PartType.Leg));
            Assert.IsTrue(LimbAuthoring.IsLimbChainType(PartType.Arm));
        }

        [Test]
        public void IsLimbChainType_FalseForAttachmentAndGenericTypes()
        {
            Assert.IsFalse(LimbAuthoring.IsLimbChainType(PartType.Foot));
            Assert.IsFalse(LimbAuthoring.IsLimbChainType(PartType.Hand));
            Assert.IsFalse(LimbAuthoring.IsLimbChainType(PartType.Part));
            Assert.IsFalse(LimbAuthoring.IsLimbChainType(PartType.Eye));
            Assert.IsFalse(LimbAuthoring.IsLimbChainType(PartType.Body));
        }

        [Test]
        public void ApplyLimbStateForTypeChange_ClearsChainWhenSwitchingAwayFromLimbType()
        {
            var part = new CreaturePart
            {
                PartType = PartType.Leg,
                Limb = LimbAuthoring.DefaultLimbChainForType(PartType.Leg),
            };

            LimbAuthoring.ApplyLimbStateForTypeChange(part, PartType.Part);

            Assert.IsNull(part.Limb, "A non-limb part must drop its stale chain when the type changes away.");
        }

        [Test]
        public void DefaultLimbChainForType_SeedsOnlyLimbChainTypes()
        {
            Assert.IsNotNull(LimbAuthoring.DefaultLimbChainForType(PartType.Leg));
            Assert.IsNotNull(LimbAuthoring.DefaultLimbChainForType(PartType.Arm));
            Assert.IsNull(LimbAuthoring.DefaultLimbChainForType(PartType.Hand),
                "A Hand is an attachment part, not a standalone chain.");
            Assert.IsNull(LimbAuthoring.DefaultLimbChainForType(PartType.Foot));
            Assert.IsNull(LimbAuthoring.DefaultLimbChainForType(PartType.Part));
        }

        [Test]
        public void DefaultLimbChainForType_RootAtOriginTerminalDownward()
        {
            LimbChain chain = LimbAuthoring.DefaultLimbChainForType(PartType.Arm);
            Assert.AreEqual(2, chain.Joints.Count);
            Assert.AreEqual(Vector3.zero, chain.Joints[0].Position, "Root must sit at the local origin.");
            Assert.AreEqual(new Vector3(0f, -1f, 0f), chain.Joints[1].Position, "Default limb extends down local -Y.");
            Assert.IsNotNull(chain.Thickness);
            Assert.AreEqual(2, chain.Thickness.Keys.Count, "Default thickness is a two-key taper.");
        }

        [Test]
        public void NextLimbJointId_IsMaxPlusOne()
        {
            LimbChain chain = LimbAuthoring.DefaultLimbChainForType(PartType.Leg);
            Assert.AreEqual(3u, LimbAuthoring.NextLimbJointId(chain), "Two joints (ids 1, 2) -> next is 3.");
        }

        [Test]
        public void ResizeLimbChain_Growing_AppendsIncreasingIdsAndExtendsDownward()
        {
            LimbChain chain = LimbAuthoring.DefaultLimbChainForType(PartType.Leg);
            LimbAuthoring.ResizeLimbChain(chain, 4);

            Assert.AreEqual(4, chain.Joints.Count);
            Assert.AreEqual(new Vector3(0f, -1.25f, 0f), chain.Joints[2].Position,
                "The first appended joint extends down from the original tail (0,-1,0).");
            Assert.AreEqual(new Vector3(0f, -1.5f, 0f), chain.Joints[3].Position,
                "Each appended joint continues downward.");
            Assert.Greater(chain.Joints[2].Id, chain.Joints[1].Id);
            Assert.Greater(chain.Joints[3].Id, chain.Joints[2].Id);
        }

        [Test]
        public void ResizeLimbChain_Shrinking_RemovesTailOnly_NeverRoot()
        {
            LimbChain chain = LimbAuthoring.DefaultLimbChainForType(PartType.Leg);
            LimbAuthoring.ResizeLimbChain(chain, 4);
            LimbAuthoring.ResizeLimbChain(chain, 2);

            Assert.AreEqual(2, chain.Joints.Count);
            Assert.AreEqual(Vector3.zero, chain.Joints[0].Position, "The root joint is never removed.");
        }

        [Test]
        public void ResizeLimbChain_ClampsToValidatorRange()
        {
            LimbChain chain = LimbAuthoring.DefaultLimbChainForType(PartType.Leg);
            LimbAuthoring.ResizeLimbChain(chain, 1); // below the validator minimum
            Assert.AreEqual(GenerationTolerances.MinLimbJointCount, chain.Joints.Count);

            LimbAuthoring.ResizeLimbChain(chain, 10000); // above the validator maximum
            Assert.AreEqual(GenerationTolerances.MaxLimbJointCount, chain.Joints.Count);
        }

        [Test]
        public void ClampJointToBounds_RootLocksToOrigin()
        {
            var bounds = BoundsDefinition.Default;
            Vector3 result = LimbAuthoring.ClampJointToBounds(new Vector3(9f, 9f, 9f), 0, bounds);
            Assert.AreEqual(Vector3.zero, result, "The root joint must stay at the local origin.");
        }

        [Test]
        public void ClampJointToBounds_InteriorClampsToBounds()
        {
            var bounds = BoundsDefinition.Default;
            Vector3 result = LimbAuthoring.ClampJointToBounds(
                new Vector3(999f, 999f, 999f), 1, bounds);
            Assert.AreEqual(new Vector3(bounds.MaxX, bounds.MaxY, bounds.MaxZ), result);

            Vector3 inside = LimbAuthoring.ClampJointToBounds(new Vector3(0.1f, -0.5f, 0.2f), 1, bounds);
            Assert.AreEqual(new Vector3(0.1f, -0.5f, 0.2f), inside);
        }

        [Test]
        public void WorldAndLocalJointPosition_RoundTripThroughPartMatrix()
        {
            var matrix = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), Quaternion.Euler(20f, 0f, 0f), Vector3.one);
            Vector3 local = new Vector3(0.3f, -0.7f, 0.1f);

            Vector3 world = LimbAuthoring.WorldJointPosition(matrix, local);
            Vector3 back = LimbAuthoring.LocalJointPosition(matrix, world);

            Assert.Less(Vector3.Distance(local, back), 1e-4f);
        }

        [Test]
        public void ThicknessAdapter_DefaultCurve_IsLinearTaper()
        {
            AnimationCurve curve = ThicknessCurveAdapter.DefaultCurve();
            Assert.AreEqual(2, curve.keys.Length);
            Assert.AreEqual(0f, curve.keys[0].time);
            Assert.AreEqual(0.30f, curve.keys[0].value, 1e-4f);
            Assert.AreEqual(1f, curve.keys[1].time);
            Assert.AreEqual(0.12f, curve.keys[1].value, 1e-4f);
            // Piecewise-linear: the curve evaluates linearly between the keys.
            Assert.AreEqual(0.21f, curve.Evaluate(0.5f), 1e-4f);
        }

        [Test]
        public void ThicknessAdapter_ToProfile_ReadsTimeAndValue()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(0.5f, 0.2f),
                new Keyframe(1f, 0.12f));

            ThicknessProfile profile = ThicknessCurveAdapter.ToProfile(curve);

            Assert.AreEqual(3, profile.Keys.Count);
            Assert.AreEqual(0.5f, profile.Keys[1].T);
            Assert.AreEqual(0.2f, profile.Keys[1].Value);
        }

        [Test]
        public void ThicknessAdapter_ProfileToCurveToProfile_RoundTrips()
        {
            var profile = new ThicknessProfile();
            profile.Keys.Add(new ThicknessKey { T = 0f, Value = 0.3f });
            profile.Keys.Add(new ThicknessKey { T = 1f, Value = 0.12f });

            AnimationCurve curve = ThicknessCurveAdapter.ToCurve(profile);
            ThicknessProfile back = ThicknessCurveAdapter.ToProfile(curve);

            Assert.AreEqual(2, back.Keys.Count);
            Assert.AreEqual(0f, back.Keys[0].T);
            Assert.AreEqual(0.3f, back.Keys[0].Value, 1e-4f);
            Assert.AreEqual(1f, back.Keys[1].T);
            Assert.AreEqual(0.12f, back.Keys[1].Value, 1e-4f);
        }

        [Test]
        public void ThicknessAdapter_ProfileEvaluate_MatchesCurveEvaluate()
        {
            var profile = new ThicknessProfile();
            profile.Keys.Add(new ThicknessKey { T = 0f, Value = 0.3f });
            profile.Keys.Add(new ThicknessKey { T = 1f, Value = 0.12f });
            AnimationCurve curve = ThicknessCurveAdapter.ToCurve(profile);

            Assert.AreEqual(profile.Evaluate(0.25f), curve.Evaluate(0.25f), 1e-4f);
            Assert.AreEqual(profile.Evaluate(0.75f), curve.Evaluate(0.75f), 1e-4f);
        }
    }
}
