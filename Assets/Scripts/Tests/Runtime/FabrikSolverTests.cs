using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Common;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class FabrikSolverTests
    {
        private static Vector3[] StraightChain(int jointCount, float linkLength)
        {
            var positions = new Vector3[jointCount];
            for (int i = 0; i < jointCount; i++) positions[i] = new Vector3(i * linkLength, 0f, 0f);
            return positions;
        }

        private static float[] UniformLinkLengths(int linkCount, float length)
        {
            var lengths = new float[linkCount];
            for (int i = 0; i < linkCount; i++) lengths[i] = length;
            return lengths;
        }

        [Test]
        public void Solve_ReachableTarget_EndEffectorConvergesWithinTolerance()
        {
            Vector3[] initial = StraightChain(4, 1f); // total reach = 3
            float[] lengths = UniformLinkLengths(3, 1f);
            Vector3 target = new Vector3(1.5f, 1.5f, 0f); // well within reach

            Vector3[] result = FabrikSolver.Solve(initial, lengths, target, maxIterations: 20, tolerance: 0.001f);

            Assert.LessOrEqual(Vector3.Distance(result[^1], target), 0.001f);
        }

        [Test]
        public void Solve_PreservesLinkLengthsThroughoutTheChain()
        {
            Vector3[] initial = StraightChain(4, 1f);
            float[] lengths = UniformLinkLengths(3, 1f);
            Vector3 target = new Vector3(0.8f, 2f, 0.5f);

            Vector3[] result = FabrikSolver.Solve(initial, lengths, target, maxIterations: 20, tolerance: 0.001f);

            for (int i = 0; i < lengths.Length; i++)
            {
                float actualLength = Vector3.Distance(result[i], result[i + 1]);
                Assert.AreEqual(lengths[i], actualLength, 1e-3f, $"Link {i} length was not preserved.");
            }
        }

        [Test]
        public void Solve_RootStaysPinnedAtItsOriginalPosition()
        {
            Vector3[] initial = StraightChain(3, 1f);
            float[] lengths = UniformLinkLengths(2, 1f);
            Vector3 originalRoot = initial[0];

            Vector3[] result = FabrikSolver.Solve(initial, lengths, new Vector3(1f, 1f, 0f), 20, 0.001f);

            Assert.AreEqual(originalRoot, result[0]);
        }

        [Test]
        public void Solve_UnreachableTarget_StretchesChainStraightTowardTarget()
        {
            Vector3[] initial = StraightChain(3, 1f); // total reach = 2
            float[] lengths = UniformLinkLengths(2, 1f);
            Vector3 farTarget = new Vector3(100f, 0f, 0f); // far beyond reach, along +X

            Vector3[] result = FabrikSolver.Solve(initial, lengths, farTarget, 20, 0.001f);

            Vector3 root = result[0];
            Vector3 endEffector = result[^1];

            float totalLength = lengths[0] + lengths[1];
            Assert.AreEqual(totalLength, Vector3.Distance(root, endEffector), 1e-3f,
                "An unreachable chain should stretch to its full length, not reach the target.");

            Vector3 actualDirection = (endEffector - root).normalized;
            Vector3 expectedDirection = (farTarget - root).normalized;
            Assert.LessOrEqual(Vector3.Distance(expectedDirection, actualDirection), 1e-3f,
                "A stretched chain should point straight at the target.");
        }

        [Test]
        public void Solve_UnreachableTarget_StillPreservesLinkLengths()
        {
            Vector3[] initial = StraightChain(3, 1f);
            float[] lengths = UniformLinkLengths(2, 1f);

            Vector3[] result = FabrikSolver.Solve(initial, lengths, new Vector3(50f, 20f, -10f), 20, 0.001f);

            for (int i = 0; i < lengths.Length; i++)
            {
                Assert.AreEqual(lengths[i], Vector3.Distance(result[i], result[i + 1]), 1e-3f);
            }
        }

        [Test]
        public void Solve_TargetAtRoot_DoesNotProduceNaN()
        {
            Vector3[] initial = StraightChain(3, 1f);
            float[] lengths = UniformLinkLengths(2, 1f);

            Vector3[] result = FabrikSolver.Solve(initial, lengths, initial[0], 20, 0.001f);

            foreach (Vector3 p in result)
            {
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z));
            }
        }

        [Test]
        public void Solve_RejectsMismatchedLinkLengthCount()
        {
            Vector3[] initial = StraightChain(3, 1f);
            float[] wrongLengths = UniformLinkLengths(5, 1f);

            Assert.Throws<DomainException>(() => FabrikSolver.Solve(initial, wrongLengths, Vector3.zero, 10, 0.01f));
        }

        [Test]
        public void Solve_RejectsSingleJointChain()
        {
            Assert.Throws<DomainException>(() =>
                FabrikSolver.Solve(new[] { Vector3.zero }, System.Array.Empty<float>(), Vector3.one, 10, 0.01f));
        }

        [Test]
        public void Solve_RejectsNonPositiveLinkLength()
        {
            Vector3[] initial = StraightChain(2, 1f);
            Assert.Throws<DomainException>(() =>
                FabrikSolver.Solve(initial, new[] { 0f }, Vector3.one, 10, 0.01f));
            Assert.Throws<DomainException>(() =>
                FabrikSolver.Solve(initial, new[] { -1f }, Vector3.one, 10, 0.01f));
        }
    }
}
