using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;

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
            if (!NumericValidity.IsFinite(targetPosition)) throw new DomainException("targetPosition must be finite.");

            SkeletonSnapshot restSnapshot = SkeletonSnapshot.Capture(restSkeleton);
            if (!restSnapshot.HasSameBoneOrder(currentPose.Skeleton))
            {
                throw new DomainException(
                    "currentPose must use the same bone structure and rest-pose data as restSkeleton.");
            }

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

            // Keep this adapter allocation-predictable and avoid LINQ in the
            // per-solve path; the solver itself still owns its working clone.
            Vector3[] seedPositions = new Vector3[chainIds.Count];
            for (int i = 0; i < chainIds.Count; i++)
            {
                seedPositions[i] = currentPose.GetPosition(chainIds[i]);
            }

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
