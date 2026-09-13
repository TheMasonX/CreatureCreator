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

        public static Dictionary<string, Quaternion> Resolve(Skeleton.Skeleton restSkeleton, PosedSkeleton pose)
        {
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");
            return Resolve(SkeletonSnapshot.Capture(restSkeleton), pose);
        }

        public static Dictionary<string, Quaternion> Resolve(SkeletonSnapshot restSkeleton, PosedSkeleton pose)
        {
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");
            if (pose == null) throw new DomainException("pose must not be null.");
            ValidateCompatibility(restSkeleton, pose);

            var indexedRotations = new Quaternion[restSkeleton.Count];
            ResolveIntoCompatible(restSkeleton, pose, indexedRotations);
            var rotations = new Dictionary<string, Quaternion>(restSkeleton.Count);
            for (int i = 0; i < restSkeleton.Count; i++) rotations.Add(restSkeleton[i].Id, indexedRotations[i]);
            return rotations;
        }

        public static void ResolveInto(SkeletonSnapshot restSkeleton, PosedSkeleton pose, Quaternion[] rotations)
        {
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");
            if (pose == null) throw new DomainException("pose must not be null.");
            if (rotations == null || rotations.Length < restSkeleton.Count)
                throw new DomainException("rotations must contain one entry per rest-skeleton bone.");
            ValidateCompatibility(restSkeleton, pose);
            ResolveIntoCompatible(restSkeleton, pose, rotations);
        }

        internal static void ResolveIntoCompatible(SkeletonSnapshot restSkeleton, PosedSkeleton pose, Quaternion[] rotations)
        {
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");
            if (pose == null) throw new DomainException("pose must not be null.");
            if (rotations == null || rotations.Length < restSkeleton.Count)
                throw new DomainException("rotations must contain one entry per rest-skeleton bone.");

            for (int i = 0; i < restSkeleton.Count; i++)
            {
                BoneSnapshot bone = restSkeleton[i];
                IReadOnlyList<int> children = restSkeleton.GetChildren(i);
                if (children.Count == 0)
                {
                    rotations[i] = bone.Rotation;
                    continue;
                }

                Vector3 position = pose.GetPosition(i);
                if (!TryResolveAimDirections(restSkeleton, pose, bone, children, position,
                        out Vector3 restDirection, out Vector3 posedDirection))
                {
                    // No usable reference direction: keep the bind rotation rather than
                    // inventing one. This is also the rest-pose identity.
                    rotations[i] = bone.Rotation;
                    continue;
                }

                rotations[i] = ResolveAimRotation(restDirection, posedDirection, bone.Rotation);
            }
        }

        /// <summary>
        /// The world-space direction from the bone to its reference child, measured in
        /// the REST snapshot and in the POSED snapshot. Using the same child for both
        /// makes the rest pose an exact identity (the two directions are equal) while
        /// posed bones still aim at the child they are bound to.
        /// </summary>
        private static bool TryResolveAimDirections(
            SkeletonSnapshot restSkeleton,
            PosedSkeleton pose,
            BoneSnapshot bone,
            IReadOnlyList<int> children,
            Vector3 posedPosition,
            out Vector3 restDirection,
            out Vector3 posedDirection)
        {
            restDirection = Vector3.zero;
            posedDirection = Vector3.zero;

            int referenceChild;
            if (bone.HasSegment)
            {
                referenceChild = FindSegmentContinuationChild(restSkeleton, bone, children);
                if (referenceChild < 0)
                {
                    // No child sits on the segment endpoint, so the segment itself is
                    // the reference direction; a rigid segment cannot re-aim itself.
                    restDirection = bone.EndPosition - bone.Position;
                    posedDirection = restDirection;
                    return true;
                }
            }
            else
            {
                referenceChild = FindPrimaryChild(restSkeleton, children);
            }

            if (referenceChild < 0 || referenceChild >= restSkeleton.Count) return false;

            restDirection = restSkeleton[referenceChild].Position - bone.Position;
            posedDirection = pose.GetPosition(referenceChild) - posedPosition;
            return true;
        }

        private static Quaternion ResolveAimRotation(Vector3 restDirection, Vector3 posedDirection, Quaternion restRotation)
        {
            // Guard against subtraction overflow: two large but individually finite
            // coordinates can produce a non-finite delta even though every input is
            // finite.
            if (!NumericValidity.IsFinite(restDirection) || !NumericValidity.IsFinite(posedDirection))
                return restRotation;

            float restLengthSqr = restDirection.sqrMagnitude;
            float posedLengthSqr = posedDirection.sqrMagnitude;
            if (restLengthSqr <= DirectionEpsilonSqr || posedLengthSqr <= DirectionEpsilonSqr)
                return restRotation;

            Vector3 restForward = restDirection / Mathf.Sqrt(restLengthSqr);
            Vector3 posedForward = posedDirection / Mathf.Sqrt(posedLengthSqr);

            // Swing the bone from its REST direction to the posed direction and apply
            // that delta to the bind rotation. At rest the directions are identical,
            // so the delta is identity and the bone keeps its exact bind rotation (the
            // bound mesh must not deform with no animation). Posed, the bone still aims
            // at the same reference child and keeps its authored roll; a raw
            // LookRotation would re-derive the roll and discard it.
            return Quaternion.FromToRotation(restForward, posedForward) * restRotation;
        }

        private static void ValidateCompatibility(SkeletonSnapshot restSkeleton, PosedSkeleton pose)
        {
            if (!restSkeleton.HasSameBoneOrder(pose.Skeleton))
                throw new DomainException("pose must use the same bone structure as restSkeleton.");
        }

        private static int FindSegmentContinuationChild(SkeletonSnapshot skeleton, BoneSnapshot bone, IReadOnlyList<int> children)
        {
            int samePartChild = -1;
            int endpointChild = -1;
            for (int i = 0; i < children.Count; i++)
            {
                int candidate = children[i];
                BoneSnapshot child = skeleton[candidate];
                Vector3 endpointDelta = child.Position - bone.EndPosition;
                if (!NumericValidity.IsFinite(endpointDelta) || endpointDelta.sqrMagnitude > DirectionEpsilonSqr) continue;

                endpointChild = SelectDeterministicChild(skeleton, endpointChild, candidate);
                if (string.Equals(child.SourcePartId, bone.SourcePartId, System.StringComparison.Ordinal))
                    samePartChild = SelectDeterministicChild(skeleton, samePartChild, candidate);
            }
            return samePartChild >= 0 ? samePartChild : endpointChild;
        }

        private static int SelectDeterministicChild(SkeletonSnapshot skeleton, int current, int candidate)
        {
            return current < 0 || string.CompareOrdinal(skeleton[candidate].Id, skeleton[current].Id) < 0 ? candidate : current;
        }

        private static int FindPrimaryChild(SkeletonSnapshot skeleton, IReadOnlyList<int> children)
        {
            int primaryChild = children[0];
            for (int i = 1; i < children.Count; i++)
            {
                int candidate = children[i];
                if (string.CompareOrdinal(skeleton[candidate].Id, skeleton[primaryChild].Id) < 0) primaryChild = candidate;
            }
            return primaryChild;
        }
    }
}
