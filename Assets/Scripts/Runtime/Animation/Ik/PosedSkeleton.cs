using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Animation.Ik
{
    /// <summary>
    /// An immutable snapshot of per-bone world positions — the runtime "current
    /// pose," distinct from Skeleton's rest pose the same way CreatureDefinition's
    /// canonical DNA is distinct from any in-progress edit (design doc §16's
    /// single-mutation-path principle, applied here as "one posing path": callers
    /// get a new PosedSkeleton back from WithUpdatedPositions rather than mutating
    /// one in place). A rest Skeleton never changes; a PosedSkeleton is created
    /// fresh each time a solve happens and replaces the previous one.
    /// </summary>
    public sealed class PosedSkeleton
    {
        private readonly SkeletonSnapshot _skeleton;
        private readonly Vector3[] _positions;

        private PosedSkeleton(SkeletonSnapshot skeleton, Vector3[] positions)
        {
            _skeleton = skeleton;
            _positions = positions;
        }

        internal SkeletonSnapshot Skeleton => _skeleton;

        public static PosedSkeleton FromRestPose(Skeleton.Skeleton skeleton)
        {
            if (skeleton == null) throw new DomainException("skeleton must not be null.");
            return FromRestPose(SkeletonSnapshot.Capture(skeleton));
        }

        public static PosedSkeleton FromRestPose(SkeletonSnapshot skeleton)
        {
            if (skeleton == null) throw new DomainException("skeleton must not be null.");
            var positions = new Vector3[skeleton.Count];
            for (int i = 0; i < positions.Length; i++) positions[i] = skeleton[i].Position;
            return new PosedSkeleton(skeleton, positions);
        }

        public Vector3 GetPosition(string boneId)
        {
            return GetPosition(_skeleton.GetIndex(boneId));
        }

        public bool TryGetPosition(string boneId, out Vector3 position)
        {
            if (!_skeleton.TryGetIndex(boneId, out int index))
            {
                position = default;
                return false;
            }
            position = _positions[index];
            return true;
        }

        public Vector3 GetPosition(int boneIndex)
        {
            if (boneIndex < 0 || boneIndex >= _positions.Length)
            {
                throw new DomainException("boneIndex must identify a bone in the pose.");
            }
            return _positions[boneIndex];
        }

        /// <summary>Returns a new PosedSkeleton with the given bones' positions replaced; all other bones keep their current position.</summary>
        public PosedSkeleton WithUpdatedPositions(IReadOnlyDictionary<string, Vector3> updates)
        {
            if (updates == null) throw new DomainException("updates must not be null.");

            var merged = (Vector3[])_positions.Clone();
            foreach (KeyValuePair<string, Vector3> update in updates)
            {
                if (!_skeleton.TryGetIndex(update.Key, out int index))
                {
                    throw new DomainException($"Bone '{update.Key}' has no position in the rest skeleton.");
                }
                merged[index] = update.Value;
            }
            return new PosedSkeleton(_skeleton, merged);
        }
    }
}
