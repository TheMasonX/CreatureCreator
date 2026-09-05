using System.Collections.Generic;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Animation.Ik
{
    /// <summary>
    /// Derives runtime bone rotations from a position-only pose. Child directions
    /// drive non-terminal bones. Terminal bones retain their rest rotation because
    /// the position-only pose has no separate endpoint for them.
    /// </summary>
    public static class PoseRotationResolver
    {
        private const float DirectionEpsilonSqr = 1e-8f;

        public static Dictionary<string, Quaternion> Resolve(
            Skeleton.Skeleton restSkeleton, PosedSkeleton pose)
        {
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");
            return Resolve(SkeletonSnapshot.Capture(restSkeleton), pose);
        }

        public static Dictionary<string, Quaternion> Resolve(
            SkeletonSnapshot restSkeleton, PosedSkeleton pose)
        {
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");
            if (pose == null) throw new DomainException("pose must not be null.");
            if (!restSkeleton.HasSameBoneOrder(pose.Skeleton))
            {
                throw new DomainException("pose must use the same bone order as restSkeleton.");
            }

            var rotations = new Dictionary<string, Quaternion>(restSkeleton.Count);
            for (int i = 0; i < restSkeleton.Count; i++)
            {
                BoneSnapshot bone = restSkeleton[i];
                Vector3 position = pose.GetPosition(i);
                IReadOnlyList<int> children = restSkeleton.GetChildren(i);
                if (children.Count == 0)
                {
                    rotations[bone.Id] = bone.Rotation;
                    continue;
                }

                Vector3 targetPosition;
                if (bone.HasSegment)
                {
                    targetPosition = position + (bone.EndPosition - bone.Position);
                }
                else
                {
                    int primaryChild = FindPrimaryChild(restSkeleton, children);
                    targetPosition = pose.GetPosition(primaryChild);
                }

                Vector3 direction = targetPosition - position;
                rotations[bone.Id] = ResolveLookRotation(direction, bone.Rotation);
            }
            return rotations;
        }

        private static int FindPrimaryChild(SkeletonSnapshot skeleton, IReadOnlyList<int> children)
        {
            int primaryChild = children[0];
            for (int i = 1; i < children.Count; i++)
            {
                int candidate = children[i];
                if (string.CompareOrdinal(skeleton[candidate].Id, skeleton[primaryChild].Id) < 0)
                {
                    primaryChild = candidate;
                }
            }
            return primaryChild;
        }

        private static Quaternion ResolveLookRotation(Vector3 direction, Quaternion restRotation)
        {
            Vector3 forward = direction.sqrMagnitude > DirectionEpsilonSqr
                ? direction.normalized
                : restRotation * Vector3.forward;
            if (forward.sqrMagnitude <= DirectionEpsilonSqr) forward = Vector3.forward;

            Vector3 up = restRotation * Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, up.normalized)) > 0.9999f)
            {
                up = restRotation * Vector3.right;
            }
            if (up.sqrMagnitude <= DirectionEpsilonSqr) up = Vector3.up;

            return Quaternion.LookRotation(forward, up);
        }
    }
}
