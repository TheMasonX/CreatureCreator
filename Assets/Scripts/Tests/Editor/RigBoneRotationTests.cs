using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// TSK-0215: pure handle-frame composition for the rig bone rotation tool.
    ///
    /// The reported regression was that the first drag snapped every bone to the
    /// handle's default orientation. The cause was a coordinate-space error:
    /// Handles.RotationHandle returns <c>worldDelta * handleFrame</c>, so assigning
    /// that value as an absolute rotation discards the bone's rotation whenever the
    /// handle frame is not the bone's own rotation (Global pivot mode). These tests
    /// pin the contract that keeps the SceneView interaction correct; the SceneView
    /// event loop itself remains a manual residual check.
    /// </summary>
    [TestFixture]
    public class RigBoneRotationTests
    {
        private static readonly Quaternion BoneRotation = Quaternion.Euler(31f, 47f, -12f);

        [Test]
        public void ResolveHandleFrame_GlobalPivot_ReturnsIdentity()
        {
            Assert.AreEqual(
                Quaternion.identity,
                RigBoneRotation.ResolveHandleFrame(BoneRotation, localPivot: false));
        }

        [Test]
        public void ResolveHandleFrame_LocalPivot_ReturnsBoneRotation()
        {
            Assert.AreEqual(
                BoneRotation,
                RigBoneRotation.ResolveHandleFrame(BoneRotation, localPivot: true));
        }

        [Test]
        public void ApplyHandleDelta_ZeroDelta_PreservesBoneRotationInGlobalPivot()
        {
            // A drag that has not produced rotation returns the passed frame
            // unchanged. This is the first event of every drag and must be a no-op.
            Quaternion frame = RigBoneRotation.ResolveHandleFrame(BoneRotation, localPivot: false);
            Quaternion result = RigBoneRotation.ApplyHandleDelta(BoneRotation, frame, frame);
            AssertRotationEqual(BoneRotation, result);
        }

        [Test]
        public void ApplyHandleDelta_ZeroDelta_PreservesBoneRotationInLocalPivot()
        {
            Quaternion frame = RigBoneRotation.ResolveHandleFrame(BoneRotation, localPivot: true);
            Quaternion result = RigBoneRotation.ApplyHandleDelta(BoneRotation, frame, frame);
            AssertRotationEqual(BoneRotation, result);
        }

        [Test]
        public void ApplyHandleDelta_GlobalFrame_AppliesWorldDeltaToCurrentRotation()
        {
            // Global pivot: the frame is identity, so the handle result is the
            // world-space drag delta and must compose with the existing rotation.
            Quaternion worldDelta = Quaternion.AngleAxis(75f, new Vector3(0.2f, 1f, 0.3f).normalized);
            Quaternion result = RigBoneRotation.ApplyHandleDelta(BoneRotation, Quaternion.identity, worldDelta);
            AssertRotationEqual(worldDelta * BoneRotation, result);
        }

        [Test]
        public void ApplyHandleDelta_LocalFrame_AppliesDeltaAroundBoneAxes()
        {
            // Local pivot: the drag rotates around the bone's own axes. Unity
            // composes result = worldDelta * frame, so the recovered delta must
            // reproduce a local-axis rotation (rotation * localDelta).
            Quaternion localDelta = Quaternion.AngleAxis(40f, Vector3.forward);
            Quaternion worldDelta = BoneRotation * localDelta * Quaternion.Inverse(BoneRotation);
            Quaternion handleResult = worldDelta * BoneRotation;
            Quaternion result = RigBoneRotation.ApplyHandleDelta(BoneRotation, BoneRotation, handleResult);
            AssertRotationEqual(BoneRotation * localDelta, result);
        }

        [Test]
        public void ApplyHandleDelta_LaterFrame_UsesDragStartRotationNotCurrent()
        {
            // Handles.RotationHandle reports the delta relative to the drag start, so
            // every frame must be applied to the SAME start rotation. Feeding the
            // previous frame's output back in multiplies the accumulated delta again
            // and spins the bone ("moves too fast, independent of drag amount").
            Quaternion frame = Quaternion.identity; // Global pivot
            Quaternion firstDelta = Quaternion.AngleAxis(20f, Vector3.up);
            Quaternion secondDelta = Quaternion.AngleAxis(25f, Vector3.up);

            Quaternion afterFirst = RigBoneRotation.ApplyHandleDelta(
                BoneRotation, frame, firstDelta * frame);
            Quaternion afterSecond = RigBoneRotation.ApplyHandleDelta(
                BoneRotation, frame, secondDelta * frame);

            AssertRotationEqual(secondDelta * BoneRotation, afterSecond);

            // The compounding path (previous result used as the start rotation) must
            // produce a materially different, runaway rotation.
            Quaternion compounded = RigBoneRotation.ApplyHandleDelta(
                afterFirst, frame, secondDelta * frame);
            Assert.That(Quaternion.Angle(compounded, afterSecond), Is.GreaterThan(1f),
                "using the running rotation as the start rotation must be observably wrong");
        }

        private static void AssertRotationEqual(Quaternion expected, Quaternion actual)
        {
            // Quaternion equality is sign-ambiguous (q and -q are the same rotation).
            Assert.That(
                Quaternion.Angle(expected, actual),
                Is.LessThan(1e-3f),
                $"Expected {expected.eulerAngles} but was {actual.eulerAngles}.");
        }
    }
}
