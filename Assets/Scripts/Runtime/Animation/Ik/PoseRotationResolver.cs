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
            if (pose == null) throw new DomainException("pose must not be null.");

            var rotations = new Dictionary<string, Quaternion>(restSkeleton.Bones.Count);
            foreach (Bone bone in restSkeleton.Bones)
            {
                if (bone == null) continue;
                Vector3 position = pose.GetPosition(bone.Id);
                Bone child = FindFirstChild(restSkeleton, bone.Id);
                if (child == null)
                {
                    rotations[bone.Id] = bone.Rotation;
                    continue;
                }

                Vector3 childPosition = pose.GetPosition(child.Id);
                Vector3 direction = childPosition - position;
                rotations[bone.Id] = ResolveLookRotation(direction, bone.Rotation);
            }
            return rotations;
        }

        private static Bone FindFirstChild(Skeleton.Skeleton skeleton, string parentId)
        {
            foreach (Bone bone in skeleton.Bones)
            {
                if (bone != null && bone.ParentBoneId == parentId) return bone;
            }
            return null;
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
