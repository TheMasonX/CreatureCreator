using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Animation.Ik
{
    /// <summary>
    /// Solves a single IK chain (root bone down to a named leaf/effector bone)
    /// toward a target position, returning a new PosedSkeleton. This is the only
    /// place FabrikSolver, BoneChain, Skeleton, and PosedSkeleton all meet — each
    /// of those stays independently testable (see their respective test files)
    /// specifically so this adapter is the sole integration point, matching the
    /// design doc's explicit call to keep the solver itself ignorant of bone
    /// ownership.
    /// </summary>
    public static class IkChainSolver
    {
        public const int DefaultMaxIterations = 10;
        public const float DefaultTolerance = 0.01f;

        public static PosedSkeleton SolveChainTarget(
            Skeleton.Skeleton restSkeleton,
            PosedSkeleton currentPose,
            string leafBoneId,
            Vector3 targetPosition,
            int maxIterations = DefaultMaxIterations,
            float tolerance = DefaultTolerance)
        {
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");
            if (currentPose == null) throw new DomainException("currentPose must not be null.");

            List<string> chainIds = BoneChain.ExtractChain(restSkeleton, leafBoneId);
            if (chainIds.Count < 2)
            {
                throw new DomainException(
                    $"Bone '{leafBoneId}' has no ancestors — an IK chain needs at least 2 bones (1 link). " +
                    "This bone is a root with nothing to solve against.");
            }

            // Link lengths always come from the REST pose, not the current pose —
            // bones are treated as rigid (fixed length); only their positions
            // change under IK. Seeding FABRIK's initial guess from the CURRENT
            // pose (not the rest pose) makes repeated per-frame solves converge
            // faster and pose continuously rather than snapping back to rest each
            // time.
            Vector3[] restPositions = BoneChain.ExtractRestPositions(restSkeleton, chainIds);
            float[] linkLengths = BoneChain.ComputeLinkLengths(restPositions);

            Vector3[] seedPositions = chainIds.Select(id => currentPose.GetPosition(id)).ToArray();

            Vector3[] solved = FabrikSolver.Solve(seedPositions, linkLengths, targetPosition, maxIterations, tolerance);

            var updates = new Dictionary<string, Vector3>(chainIds.Count);
            for (int i = 0; i < chainIds.Count; i++)
            {
                updates[chainIds[i]] = solved[i];
            }

            return currentPose.WithUpdatedPositions(updates);
        }
    }
}
