using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;

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
        private readonly Dictionary<string, Vector3> _positions;

        private PosedSkeleton(Dictionary<string, Vector3> positions)
        {
            _positions = positions;
        }

        public static PosedSkeleton FromRestPose(Skeleton.Skeleton skeleton)
        {
            if (skeleton == null) throw new DomainException("skeleton must not be null.");
            return new PosedSkeleton(skeleton.Bones.ToDictionary(b => b.Id, b => b.Position));
        }

        public Vector3 GetPosition(string boneId)
        {
            if (!_positions.TryGetValue(boneId, out Vector3 position))
            {
                throw new DomainException($"Bone '{boneId}' has no position in this pose.");
            }
            return position;
        }

        public bool TryGetPosition(string boneId, out Vector3 position)
        {
            return _positions.TryGetValue(boneId, out position);
        }

        /// <summary>Returns a new PosedSkeleton with the given bones' positions replaced; all other bones keep their current position.</summary>
        public PosedSkeleton WithUpdatedPositions(IReadOnlyDictionary<string, Vector3> updates)
        {
            if (updates == null) throw new DomainException("updates must not be null.");

            var merged = new Dictionary<string, Vector3>(_positions);
            foreach (KeyValuePair<string, Vector3> update in updates)
            {
                merged[update.Key] = update.Value;
            }
            return new PosedSkeleton(merged);
        }
    }
}
