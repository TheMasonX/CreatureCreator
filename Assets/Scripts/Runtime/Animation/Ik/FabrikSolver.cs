using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Animation.Ik
{
    /// <summary>
    /// Forward And Backward Reaching Inverse Kinematics (Aristidou &amp; Lasenby,
    /// 2011). Operates purely on Vector3[] joint positions and float[] link
    /// lengths — no knowledge of Bone, Skeleton, Transform, or GameObject exists
    /// anywhere in this class, matching the design doc's explicit requirement
    /// that the solver stay ignorant of bone ownership so it's testable in
    /// complete isolation (see IkChainSolver for the adapter that actually
    /// connects this to a Skeleton).
    ///
    /// ALGORITHM: given the chain is UNREACHABLE (root-to-target distance exceeds
    /// the sum of link lengths), the chain simply stretches straight toward the
    /// target — no iteration needed, this is the exact solution in that case.
    /// Otherwise, alternate BACKWARD passes (pull the end effector to the target,
    /// then walk back toward the root re-fixing each link length) and FORWARD
    /// passes (re-pin the root at its original position, then walk out toward the
    /// end effector re-fixing each link length) until the end effector is within
    /// tolerance of the target or maxIterations is reached.
    /// </summary>
    public static class FabrikSolver
    {
        private const float DegenerateDirectionEpsilonSqr = 1e-8f;

        public static Vector3[] Solve(
            Vector3[] initialPositions, float[] linkLengths, Vector3 target,
            int maxIterations, float tolerance)
        {
            ValidateInputs(initialPositions, linkLengths, maxIterations, tolerance);

            var positions = (Vector3[])initialPositions.Clone();
            Vector3 root = positions[0];
            int last = positions.Length - 1;

            float totalLength = 0f;
            foreach (float length in linkLengths) totalLength += length;

            float rootToTargetDistance = Vector3.Distance(root, target);
            if (rootToTargetDistance >= totalLength)
            {
                StretchTowardTarget(positions, linkLengths, root, target);
                return positions;
            }

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                if (Vector3.Distance(positions[last], target) <= tolerance) break;
                BackwardPass(positions, linkLengths, target);
                ForwardPass(positions, linkLengths, root);
            }

            return positions;
        }

        private static void BackwardPass(Vector3[] positions, float[] linkLengths, Vector3 target)
        {
            int last = positions.Length - 1;
            positions[last] = target;
            for (int i = last - 1; i >= 0; i--)
            {
                Vector3 direction = SafeDirection(positions[i + 1], positions[i]);
                positions[i] = positions[i + 1] + direction * linkLengths[i];
            }
        }

        private static void ForwardPass(Vector3[] positions, float[] linkLengths, Vector3 root)
        {
            positions[0] = root;
            for (int i = 1; i < positions.Length; i++)
            {
                Vector3 direction = SafeDirection(positions[i - 1], positions[i]);
                positions[i] = positions[i - 1] + direction * linkLengths[i - 1];
            }
        }

        private static void StretchTowardTarget(Vector3[] positions, float[] linkLengths, Vector3 root, Vector3 target)
        {
            Vector3 direction = SafeDirection(root, target);
            positions[0] = root;
            for (int i = 1; i < positions.Length; i++)
            {
                positions[i] = positions[i - 1] + direction * linkLengths[i - 1];
            }
        }

        /// <summary>
        /// Direction from 'from' to 'to', or Vector3.up if the two points
        /// coincide (a genuine but rare degenerate case — e.g. a chain whose
        /// current pose has two joints at the same position). Vector3.up is an
        /// arbitrary but fixed, deterministic fallback; it never produces NaN.
        /// </summary>
        private static Vector3 SafeDirection(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            return delta.sqrMagnitude < DegenerateDirectionEpsilonSqr ? Vector3.up : delta.normalized;
        }

        private static void ValidateInputs(Vector3[] positions, float[] linkLengths, int maxIterations, float tolerance)
        {
            if (positions == null) throw new DomainException("initialPositions must not be null.");
            if (linkLengths == null) throw new DomainException("linkLengths must not be null.");
            if (positions.Length < 2)
            {
                throw new DomainException("A FABRIK chain needs at least 2 joints (1 link) to solve.");
            }
            if (linkLengths.Length != positions.Length - 1)
            {
                throw new DomainException(
                    $"linkLengths.Length ({linkLengths.Length}) must equal positions.Length - 1 ({positions.Length - 1}).");
            }
            foreach (float length in linkLengths)
            {
                if (length <= 0f || float.IsNaN(length) || float.IsInfinity(length))
                {
                    throw new DomainException($"Every link length must be finite and positive; got {length}.");
                }
            }
            if (maxIterations <= 0) throw new DomainException("maxIterations must be positive.");
            if (tolerance < 0f || float.IsNaN(tolerance)) throw new DomainException("tolerance must be non-negative.");
        }
    }
}
