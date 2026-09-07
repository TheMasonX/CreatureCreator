using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Animation.Ik
{
    /// <summary>
    /// Forward And Backward Reaching Inverse Kinematics (Aristidou &amp; Lasenby,
    /// 2011). Operates purely on Vector3[] joint positions and float[] link
    /// lengths — no knowledge of Bone, Skeleton, Transform, or GameObject exists
    /// anywhere in this class.
    /// </summary>
    public static class FabrikSolver
    {
        private const float DegenerateDirectionEpsilonSqr = 1e-8f;

        public static Vector3[] Solve(
            Vector3[] initialPositions, float[] linkLengths, Vector3 target,
            int maxIterations, float tolerance)
        {
            ValidateInputs(initialPositions, linkLengths, target, maxIterations, tolerance);

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
        /// Direction from <paramref name="from"/> to <paramref name="to"/>, or a
        /// deterministic fallback when the points coincide. Input validation rejects
        /// non-finite vectors before the solver starts, preventing NaN/Infinity from
        /// entering the iterative passes.
        /// </summary>
        private static Vector3 SafeDirection(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            return delta.sqrMagnitude < DegenerateDirectionEpsilonSqr ? Vector3.up : delta.normalized;
        }

        private static void ValidateInputs(
            Vector3[] positions,
            float[] linkLengths,
            Vector3 target,
            int maxIterations,
            float tolerance)
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

            for (int i = 0; i < positions.Length; i++)
            {
                if (!NumericValidity.IsFinite(positions[i]))
                {
                    throw new DomainException($"Initial joint position {i} must be finite.");
                }
            }
            if (!NumericValidity.IsFinite(target))
            {
                throw new DomainException("target must be finite.");
            }

            foreach (float length in linkLengths)
            {
                if (length <= 0f || float.IsNaN(length) || float.IsInfinity(length))
                {
                    throw new DomainException($"Every link length must be finite and positive; got {length}.");
                }
            }
            if (maxIterations <= 0) throw new DomainException("maxIterations must be positive.");
            if (tolerance < 0f || float.IsNaN(tolerance) || float.IsInfinity(tolerance))
            {
                throw new DomainException("tolerance must be finite and non-negative.");
            }
        }
    }
}
