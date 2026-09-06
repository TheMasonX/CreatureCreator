using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

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

        /// <summary>A limb's explicit terminal joint node id.</summary>
        public static string ResolveLimbTerminalBoneId(CreaturePart limb, bool mirrored)
        {
            return ResolveLimbTerminalBoneId(limb, ResolvedLimb.Resolve(limb.Limb), mirrored);
        }

        /// <summary>Resolves a limb terminal bone id from the canonical limb snapshot.</summary>
        public static string ResolveLimbTerminalBoneId(
            CreaturePart limb, ResolvedLimb resolvedLimb, bool mirrored)
        {
            return ResolveLimbJointBoneId(
                limb, resolvedLimb.JointPositions.Count - 1, mirrored);
        }

        /// <summary>Resolves a limb joint node, including the terminal joint.</summary>
        public static string ResolveLimbJointBoneId(CreaturePart limb, int jointIndex, bool mirrored)
        {
            return ResolveMirroredBoneId(limb.Id + LimbJointBoneSeparator + jointIndex, mirrored);
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
            // Compatibility entry point over the shared decision core. Kept for
            // the SkeletonInferrer defensive fallback, which resolves parts from
            // authored DNA that cannot be canonicalized into a snapshot. For any
            // valid definition it produces the identical id as the snapshot
            // overload (they funnel through the same core below).
            if (part.ParentId == null || part.ParentId == CreatureDefinition.BodyId)
            {
                return ResolveBodyParentBoneId(definition, part, mirrored);
            }

            CreaturePart parentPart = definition.FindPart(part.ParentId);
            bool parentFound = parentPart != null;
            bool parentMirrorFlagged = parentFound
                && parentPart.MirrorAcrossSymmetryPlane;
            bool symmetryEnabled = definition.SymmetryMode != SymmetryMode.None;
            bool parentIsRealLimb = parentFound
                && parentPart.Limb != null
                && parentPart.Limb.Joints != null
                && parentPart.Limb.Joints.Count >= 2;
            string parentLimbTerminalId = parentIsRealLimb
                ? ResolveLimbTerminalBoneId(
                    parentPart, ResolvedLimb.Resolve(parentPart.Limb), mirrored: false)
                : null;

            return ResolveParentBoneIdCore(
                part.ParentId,
                parentFound,
                parentMirrorFlagged,
                symmetryEnabled,
                parentIsRealLimb,
                parentLimbTerminalId,
                mirrored);
        }

        /// <summary>
        /// Resolves a parent bone from the immutable generation snapshot. Normal
        /// generation uses this overload so parent lookup and limb terminal data
        /// cannot observe later authored mutations.
        /// </summary>
        public static string ResolveParentBoneId(
            ResolvedCreatureSnapshot snapshot, ResolvedPartSnapshot part, bool mirrored)
        {
            if (snapshot == null) throw new DomainException("snapshot must not be null.");

            if (part.ParentId == null || part.ParentId == CreatureDefinition.BodyId)
            {
                return ResolveBodyParentBoneId(snapshot, part, mirrored);
            }

            bool parentFound = snapshot.TryGetPart(part.ParentId, out ResolvedPartSnapshot parent);
            bool parentMirrorFlagged = parentFound
                && parent.MirrorAcrossSymmetryPlane;
            bool symmetryEnabled = snapshot.SymmetryMode != SymmetryMode.None;
            // CC-091: a resolved limb parent is only a real limb parent when its
            // chain has at least two joints (N joints -> N-1 bones, so the
            // terminal bone index N-2 must be >= 0). ResolvedLimb permits a
            // single-joint degenerate chain, and validation
            // (MinLimbJointCount == 2) is not guaranteed for a direct call, so we
            // guard on the resolved joint count exactly like the definition
            // overload guards on the authored Joints.Count. This keeps the two
            // overloads consistent and never fabricates a "_j-1" bone id.
            bool parentIsRealLimb = parentFound
                && parent.HasLimb
                && parent.Limb.JointPositions.Count >= 2;
            string parentLimbTerminalId = parentIsRealLimb
                ? ResolveLimbTerminalBoneId(
                    new CreaturePart { Id = parent.Id }, parent.Limb, mirrored: false)
                : null;

            return ResolveParentBoneIdCore(
                part.ParentId,
                parentFound,
                parentMirrorFlagged,
                symmetryEnabled,
                parentIsRealLimb,
                parentLimbTerminalId,
                mirrored);
        }

        /// <summary>
        /// The single implementation of the parent-bone decision for a
        /// non-Body-rooted part, shared by the definition and snapshot overloads
        /// so the raw/snapshot paths cannot drift. A child binds to its parent's
        /// TERMINAL bone (a real limb parent with at least two joints) or to the
        /// bare parent id (a non-limb parent, or an absent parent); a mirrored
        /// child binds to the mirrored copy of its DNA parent when that parent is
        /// also mirrored, or to the single unmirrored parent bone otherwise.
        /// </summary>
        private static string ResolveParentBoneIdCore(
            string partParentId,
            bool parentFound,
            bool parentMirrorFlagged,
            bool symmetryEnabled,
            bool parentIsRealLimb,
            string parentLimbTerminalId,
            bool mirrorChild)
        {
            if (!parentFound)
            {
                // Existing rule: a part whose DNA parent is absent binds to the
                // bare parent id.
                return partParentId;
            }

            bool parentIsAlsoMirrored = parentMirrorFlagged && symmetryEnabled;
            string parentBoneBaseId = parentIsRealLimb ? parentLimbTerminalId : partParentId;
            return ResolveMirroredBoneId(parentBoneBaseId, mirrorChild && parentIsAlsoMirrored);
        }

        /// <summary>
        /// The Body socket bone for a Body-rooted part. A direct Body child that
        /// carries a <see cref="BodySurfaceAnchor"/> (ParentAttachment) binds to
        /// the socket of the anchor's segment-start sample — the SAME sample
        /// identity the resolved morphology layer (CC-056B) uses to place its
        /// geometry — replacing the legacy nearest-sample search at this single
        /// seam (CC-007). Otherwise, the nearest Body sample to the part's
        /// resolved creature-space origin (the limb's root joint, or the part
        /// origin for a non-limb) is used. This definition overload is a thin
        /// compatibility entry over the shared body core for the SkeletonInferrer
        /// defensive fallback.
        /// </summary>
        public static string ResolveBodyParentBoneId(
            CreatureDefinition definition, CreaturePart part, bool mirrored)
        {
            if (definition.Body == null || definition.Body.Samples == null
                || definition.Body.Samples.Count == 0)
            {
                return null;
            }

            ResolvedBody resolvedBody = ResolvedBody.Resolve(definition.Body);
            Matrix4x4 world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(
                definition, part);
            Vector3 position = part.Limb != null
                ? world.MultiplyPoint3x4(ResolvedLimb.Resolve(part.Limb).RootSocket)
                : world.GetColumn(3);

            BodySurfaceAnchor anchor = part.ParentAttachment;
            return ResolveBodyParentBoneIdCore(
                resolvedBody.SamplePositions,
                resolvedBody.SampleIds,
                part.ParentId,
                partHasAnchor: anchor != null,
                anchorSampleId: anchor != null ? anchor.SegmentStartSampleId : 0u,
                position,
                mirrored);
        }

        private static string ResolveBodyParentBoneId(
            ResolvedCreatureSnapshot snapshot, ResolvedPartSnapshot part, bool mirrored)
        {
            if (!snapshot.HasBody || snapshot.Body.SamplePositions.Count == 0)
            {
                return null;
            }

            Vector3 position = part.HasLimb
                ? part.PartFrameToCreatureSpace.MultiplyPoint3x4(part.Limb.RootSocket)
                : part.PartFrameToCreatureSpace.GetColumn(3);

            return ResolveBodyParentBoneIdCore(
                snapshot.Body.SamplePositions,
                snapshot.Body.SampleIds,
                part.ParentId,
                partHasAnchor: part.HasBodySurfaceAnchor,
                anchorSampleId: part.BodySurfaceAnchorSegmentStartSampleId,
                position,
                mirrored);
        }

        /// <summary>
        /// The single implementation of the Body-socket decision, shared by the
        /// definition and snapshot overloads so the raw/snapshot paths cannot
        /// drift: anchor-based binding for a direct Body child, else the nearest
        /// Body sample to the given creature-space position. Callers supply the
        /// resolved sample arrays and the part's resolved position from their own
        /// source (authored DNA vs the immutable snapshot).
        /// </summary>
        private static string ResolveBodyParentBoneIdCore(
            IReadOnlyList<Vector3> samplePositions,
            IReadOnlyList<uint> sampleIds,
            string partParentId,
            bool partHasAnchor,
            uint anchorSampleId,
            Vector3 position,
            bool mirrored)
        {
            if (samplePositions == null || samplePositions.Count == 0)
            {
                return null;
            }

            // CC-007: anchor-based binding for direct Body children. The anchor
            // drives geometry placement only for ParentId == BodyId, so binding
            // follows the same rule. Falls back to nearest-sample when the anchor
            // does not reference a valid segment start (defensive; the validator
            // rejects those before inference).
            if (partParentId == CreatureDefinition.BodyId && partHasAnchor)
            {
                for (int i = 0; i < sampleIds.Count - 1; i++)
                {
                    if (sampleIds[i] == anchorSampleId)
                    {
                        return ResolveBodySocketBoneId(anchorSampleId);
                    }
                }
            }

            if (mirrored) position = MirrorUtility.ReflectPointAcrossX(position);

            int nearestIndex = 0;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < samplePositions.Count; i++)
            {
                float distance = (samplePositions[i] - position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return ResolveBodySocketBoneId(sampleIds[nearestIndex]);
        }
    }
}
