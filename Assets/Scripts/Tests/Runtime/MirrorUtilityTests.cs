using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class MirrorUtilityTests
    {
        [Test]
        public void ReflectPointAcrossX_NegatesOnlyX()
        {
            Assert.AreEqual(new Vector3(-2f, 3f, -4f),
                MirrorUtility.ReflectPointAcrossX(new Vector3(2f, 3f, -4f)));
        }

        [Test]
        public void ReflectTransformAcrossX_ReflectsCreatureSpacePlacement()
        {
            Matrix4x4 placement = Matrix4x4.TRS(
                new Vector3(2f, 3f, -1f), Quaternion.identity, Vector3.one);

            Matrix4x4 mirrored = MirrorUtility.ReflectTransformAcrossX(placement);

            Assert.AreEqual(new Vector3(-2f, 3f, -1f),
                mirrored.MultiplyPoint3x4(Vector3.zero));
        }

        [Test]
        public void MirrorAcrossXPlane_IsAnInvolution()
        {
            Matrix4x4 original = Matrix4x4.TRS(
                new Vector3(2f, 3f, -1f), Quaternion.Euler(15f, 40f, -20f), Vector3.one);

            Matrix4x4 mirroredTwice = MirrorUtility.MirrorAcrossXPlane(MirrorUtility.MirrorAcrossXPlane(original));

            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(original[i], mirroredTwice[i], 1e-4f, $"Matrix element {i} did not round-trip.");
            }
        }

        [Test]
        public void MirrorAcrossXPlane_NegatesPositionX_KeepsYAndZ()
        {
            Matrix4x4 original = Matrix4x4.TRS(new Vector3(5f, 2f, -3f), Quaternion.identity, Vector3.one);
            Matrix4x4 mirrored = MirrorUtility.MirrorAcrossXPlane(original);

            Vector3 position = mirrored.GetColumn(3);
            Assert.AreEqual(new Vector3(-5f, 2f, -3f), position, "MirrorUtility should negate only X.");
        }

        [Test]
        public void MirrorAcrossXPlane_IdentityRotation_StaysIdentity()
        {
            Matrix4x4 original = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
            Matrix4x4 mirrored = MirrorUtility.MirrorAcrossXPlane(original);

            Assert.AreEqual(Quaternion.identity, mirrored.rotation);
        }

        [Test]
        public void MirrorAcrossXPlane_ProducesAProperRotation()
        {
            // Regression guard for the derivation's determinant-preservation claim:
            // a mirrored rotation must still be a valid rotation (orthonormal,
            // determinant +1), not a reflection itself.
            Matrix4x4 original = Matrix4x4.TRS(
                new Vector3(1f, -2f, 3f), Quaternion.Euler(30f, 60f, 10f), Vector3.one);
            Matrix4x4 mirrored = MirrorUtility.MirrorAcrossXPlane(original);

            Quaternion extractedRotation = mirrored.rotation;
            Vector3 right = extractedRotation * Vector3.right;
            Vector3 up = extractedRotation * Vector3.up;
            Vector3 forward = extractedRotation * Vector3.forward;

            float determinant = Vector3.Dot(right, Vector3.Cross(up, forward));
            Assert.AreEqual(1f, determinant, 1e-3f, "Mirrored rotation should remain a proper (right-handed) rotation.");
        }
    }
}
