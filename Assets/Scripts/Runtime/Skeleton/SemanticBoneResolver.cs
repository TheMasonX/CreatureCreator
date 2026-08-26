using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Skeleton
{
    /// <summary>
    /// The single source of part-to-bone mapping (CC-076). Skeleton inference,
    /// mesh binding (CC-052/CC-073), and animation queries (CC-010) must all
    /// resolve the same semantic bone id for the same part instead of each
    /// re-deriving the mapping. Depends only on authoritative DNA and the
    /// resolved morphology contract (CC-056B); never on generated mesh state.
    /// </summary>
    public static class SemanticBoneResolver
    {
        /// <summary>Suffix on a mirrored bone id.</summary>
        public const string MirrorSuffix = "_mirror";

        /// <summary>Bone-id separator for a limb's per-segment bones: part.Id + LimbJointBoneSeparator + i.</summary>
        public const string LimbJointBoneSeparator = "_j";

        /// <summary>
        /// The creature-space reflection across the X = 0 plane (point form). A
        /// mirrored body socket lands at S · (unmirrored world position) — the
        /// same point reflection the SDF compiler applies to a mirrored limb
        /// chain.
        /// </summary>
        public static readonly Matrix4x4 ReflectAcrossX = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

        /// <summary>Appends the mirror suffix when mirrored; returns the id unchanged otherwise.</summary>
        public static string ResolveMirroredBoneId(string boneId, bool mirrored)
        {
            return mirrored ? boneId + MirrorSuffix : boneId;
        }

        /// <summary>A non-limb part's root bone id: part.Id [+ mirror].</summary>
        public static string ResolvePartRootBoneId(CreaturePart part, bool mirrored)
        {
            return ResolveMirroredBoneId(part.Id, mirrored);
        }

        /// <summary>A limb's per-segment bone id: part.Id + "_j" + segmentIndex [+ mirror].</summary>
        public static string ResolveLimbSegmentBoneId(CreaturePart part, int segmentIndex, bool mirrored)
        {
            return ResolveMirroredBoneId(part.Id + LimbJointBoneSeparator + segmentIndex, mirrored);
        }

        /// <summary>
        /// A limb's TERMINAL bone id — the last segment bone. A chain of N joints
        /// produces N-1 bones (indices 0..N-2); the terminal joint at index N-2 is
        /// the stable semantic child-attachment point (ADR-001 §3).
        /// </summary>
        public static string ResolveLimbTerminalBoneId(CreaturePart limb, bool mirrored)
        {
            return ResolveLimbSegmentBoneId(limb, limb.Limb.Joints.Count - 2, mirrored);
        }

        /// <summary>The Body bone id for a Body sample: body_j&lt;sampleId&gt;.</summary>
        public static string ResolveBodySocketBoneId(uint bodySampleId)
        {
            return CreatureDefinition.BodyId + LimbJointBoneSeparator + bodySampleId;
        }

        /// <summary>
        /// The bone a part's ROOT bone attaches to. A Body-rooted part (ParentId is
        /// null or the Body) binds to the nearest Body sample bone; a child binds to
        /// its parent's TERMINAL bone (limb parent) or root bone (non-limb parent).
        /// A mirrored part binds to the mirrored copy of its DNA parent when that
        /// parent is also mirrored, or to the single unmirrored parent bone
        /// otherwise.
        /// </summary>
        public static string ResolveParentBoneId(CreatureDefinition definition, CreaturePart part, bool mirrored)
        {
            if (part.ParentId == null || part.ParentId == CreatureDefinition.BodyId)
            {
                return ResolveBodyParentBoneId(definition, part, mirrored);
            }

            CreaturePart parentPart = definition.FindPart(part.ParentId);
            bool parentIsAlsoMirrored = parentPart != null
                                         && parentPart.MirrorAcrossSymmetryPlane
                                         && definition.SymmetryMode != SymmetryMode.None;

            string parentBoneBaseId;
            if (parentPart != null
                && parentPart.Limb != null
                && parentPart.Limb.Joints != null
                && parentPart.Limb.Joints.Count >= 2)
            {
                // The child of a limb attaches to the limb's TERMINAL bone
                // (N joints -> N-1 bones, so the last bone is index N-2).
                parentBoneBaseId = ResolveLimbTerminalBoneId(parentPart, mirrored: false);
            }
            else
            {
                // Existing rule: an unmirrored part's bone id is exactly the
                // source part id.
                parentBoneBaseId = part.ParentId;
            }

            return ResolveMirroredBoneId(parentBoneBaseId, mirrored && parentIsAlsoMirrored);
        }

        /// <summary>
        /// The Body socket bone for a Body-rooted part. A direct Body child that
        /// carries a <see cref="BodySurfaceAnchor"/> (ParentAttachment) binds to
        /// the socket of the anchor's segment-start sample — the SAME sample
        /// identity the resolved morphology layer (CC-056B) uses to place its
        /// geometry — replacing the legacy nearest-sample search at this single
        /// seam (CC-007). Otherwise, the nearest Body sample to the part's
        /// resolved creature-space origin (the limb's root joint, or the part
        /// origin for a non-limb) is used.
        /// </summary>
        public static string ResolveBodyParentBoneId(
            CreatureDefinition definition, CreaturePart part, bool mirrored)
        {
            if (definition.Body == null || definition.Body.Samples == null
                || definition.Body.Samples.Count == 0)
            {
                return null;
            }

            // CC-007: anchor-based binding for direct Body children. The anchor
            // drives geometry placement only for ParentId == BodyId, so binding
            // follows the same rule. Falls back to nearest-sample when the anchor
            // does not reference a valid segment start (defensive; the validator
            // rejects those before inference).
            BodySurfaceAnchor anchor = part.ParentAttachment;
            if (part.ParentId == CreatureDefinition.BodyId && anchor != null)
            {
                for (int i = 0; i < definition.Body.Samples.Count - 1; i++)
                {
                    if (definition.Body.Samples[i].Id == anchor.SegmentStartSampleId)
                    {
                        return ResolveBodySocketBoneId(anchor.SegmentStartSampleId);
                    }
                }
            }

            Matrix4x4 world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(
                definition, part);
            Vector3 position = part.Limb != null && part.Limb.Joints != null
                && part.Limb.Joints.Count > 0
                ? world.MultiplyPoint3x4(part.Limb.Joints[0].Position)
                : world.GetColumn(3);
            if (mirrored) position = ReflectAcrossX.MultiplyPoint3x4(position);

            int nearestIndex = 0;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < definition.Body.Samples.Count; i++)
            {
                float distance = (definition.Body.Samples[i].Position - position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return ResolveBodySocketBoneId(definition.Body.Samples[nearestIndex].Id);
        }
    }
}
