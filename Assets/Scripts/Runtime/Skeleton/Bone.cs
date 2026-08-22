using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Skeleton
{
    /// <summary>
    /// One inferred bone. Entirely derived state — never written back to DNA, and
    /// (per the design doc's original architectural call, praised in the delta
    /// audit) derived from CreaturePart's semantic metadata and hierarchy, not
    /// from the generated mesh's topology.
    /// </summary>
    public sealed class Bone
    {
        /// <summary>SourcePartId for an unmirrored bone; SourcePartId + MirrorSuffix for a mirrored one.</summary>
        public string Id;

        /// <summary>Null for a root bone (no parent).</summary>
        public string ParentBoneId;

        /// <summary>The CreaturePart this bone was derived from — both a bone and its mirror share the same SourcePartId.</summary>
        public string SourcePartId;

        public PartType PartType;
        public bool IsMirrored;

        /// <summary>Creature-space position in the rest/authored pose.</summary>
        public Vector3 Position;

        /// <summary>
        /// Creature-space rotation in the rest/authored pose. Extracted from the
        /// resolved world matrix via Matrix4x4.rotation — exact for uniform scale
        /// chains, an approximation under non-uniform scale (the same documented
        /// tradeoff TransformNode makes for SDF evaluation; see that class).
        /// </summary>
        public Quaternion Rotation;
    }

    public sealed class Skeleton
    {
        public List<Bone> Bones { get; } = new List<Bone>();

        public Bone FindBone(string id)
        {
            return Bones.FirstOrDefault(b => b.Id == id);
        }

        public IEnumerable<Bone> GetChildren(string parentBoneId)
        {
            return Bones.Where(b => b.ParentBoneId == parentBoneId);
        }

        public IEnumerable<Bone> GetRootBones()
        {
            return Bones.Where(b => b.ParentBoneId == null);
        }
    }
}
