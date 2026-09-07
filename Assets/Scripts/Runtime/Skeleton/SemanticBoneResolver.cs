using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

namespace ProceduralCreature.Skeleton
{
    /// <summary>
    /// The single source of semantic part-to-bone mapping. Runtime generation uses
    /// resolved morphology for parent placement; compact Body anatomy is delegated to
    /// <see cref="AnatomicalBodyRigLayout"/> so Body sampling density never leaks into
    /// rig identity.
    /// </summary>
    public static class SemanticBoneResolver
    {
        public const string MirrorSuffix = "_mirror";
        public const string LimbJointBoneSeparator = "_j";

        public static string ResolveMirroredBoneId(string boneId, bool mirrored)
            => mirrored ? boneId + MirrorSuffix : boneId;

        public static string ResolvePartRootBoneId(CreaturePart part, bool mirrored)
            => ResolvePartRootBoneId(part.Id, mirrored);

        public static string ResolvePartRootBoneId(string partId, bool mirrored)
            => ResolveMirroredBoneId(partId, mirrored);

        public static string ResolveLimbSegmentBoneId(CreaturePart part, int segmentIndex, bool mirrored)
            => ResolveLimbSegmentBoneId(part.Id, segmentIndex, mirrored);

        public static string ResolveLimbSegmentBoneId(string partId, int segmentIndex, bool mirrored)
            => ResolveMirroredBoneId(partId + LimbJointBoneSeparator + segmentIndex, mirrored);

        public static string ResolveLimbTerminalBoneId(CreaturePart limb, bool mirrored)
            => ResolveLimbTerminalBoneId(limb, ResolvedLimb.Resolve(limb.Limb), mirrored);

        public static string ResolveLimbTerminalBoneId(
            CreaturePart limb, ResolvedLimb resolvedLimb, bool mirrored)
            => ResolveLimbJointBoneId(limb.Id, resolvedLimb.JointPositions.Count - 1, mirrored);

        public static string ResolveLimbTerminalBoneId(
            string limbId, ResolvedLimb resolvedLimb, bool mirrored)
            => ResolveLimbJointBoneId(limbId, resolvedLimb.JointPositions.Count - 1, mirrored);

        public static string ResolveLimbJointBoneId(CreaturePart limb, int jointIndex, bool mirrored)
            => ResolveLimbJointBoneId(limb.Id, jointIndex, mirrored);

        public static string ResolveLimbJointBoneId(string limbId, int jointIndex, bool mirrored)
            => ResolveMirroredBoneId(limbId + LimbJointBoneSeparator + jointIndex, mirrored);

        /// <summary>Legacy Body sample ID formatter retained for old tools/fixtures.</summary>
        public static string ResolveBodySocketBoneId(uint bodySampleId)
            => CreatureDefinition.BodyId + LimbJointBoneSeparator + bodySampleId;

        public static string ResolveParentBoneId(
            CreatureDefinition definition, CreaturePart part, bool mirrored)
        {
            if (part.ParentId == null || part.ParentId == CreatureDefinition.BodyId)
            {
                return ResolveBodyParentBoneId(definition, part, mirrored);
            }

            CreaturePart parentPart = definition.FindPart(part.ParentId);
            bool parentFound = parentPart != null;
            bool parentMirrorFlagged = parentFound && parentPart.MirrorAcrossSymmetryPlane;
            bool symmetryEnabled = definition.SymmetryMode != SymmetryMode.None;
            bool parentIsRealLimb = parentFound
                && parentPart.Limb != null
                && parentPart.Limb.Joints != null
                && parentPart.Limb.Joints.Count >= 2;
            string parentLimbTerminalId = parentIsRealLimb
                ? ResolveLimbTerminalBoneId(parentPart, ResolvedLimb.Resolve(parentPart.Limb), mirrored: false)
                : null;

            return ResolveParentBoneIdCore(
                part.ParentId, parentFound, parentMirrorFlagged, symmetryEnabled,
                parentIsRealLimb, parentLimbTerminalId, mirrored);
        }

        public static string ResolveParentBoneId(
            ResolvedCreatureSnapshot snapshot, ResolvedPartSnapshot part, bool mirrored)
        {
            if (snapshot == null) throw new DomainException("snapshot must not be null.");

            if (part.ParentId == null || part.ParentId == CreatureDefinition.BodyId)
            {
                return ResolveBodyParentBoneId(snapshot, part, mirrored);
            }

            bool parentFound = snapshot.TryGetPart(part.ParentId, out ResolvedPartSnapshot parent);
            bool parentMirrorFlagged = parentFound && parent.MirrorAcrossSymmetryPlane;
            bool symmetryEnabled = snapshot.SymmetryMode != SymmetryMode.None;
            bool parentIsRealLimb = parentFound && parent.HasLimb
                && parent.Limb.JointPositions.Count >= 2;
            string parentLimbTerminalId = parentIsRealLimb
                ? ResolveLimbTerminalBoneId(parent.Id, parent.Limb, mirrored: false)
                : null;

            return ResolveParentBoneIdCore(
                part.ParentId, parentFound, parentMirrorFlagged, symmetryEnabled,
                parentIsRealLimb, parentLimbTerminalId, mirrored);
        }

        private static string ResolveParentBoneIdCore(
            string partParentId, bool parentFound, bool parentMirrorFlagged,
            bool symmetryEnabled, bool parentIsRealLimb,
            string parentLimbTerminalId, bool mirrorChild)
        {
            if (!parentFound) return partParentId;

            bool parentIsAlsoMirrored = parentMirrorFlagged && symmetryEnabled;
            string parentBoneBaseId = parentIsRealLimb ? parentLimbTerminalId : partParentId;
            return ResolveMirroredBoneId(parentBoneBaseId, mirrorChild && parentIsAlsoMirrored);
        }

        public static string ResolveBodyParentBoneId(
            CreatureDefinition definition, CreaturePart part, bool mirrored)
        {
            if (definition.Body == null || definition.Body.Samples == null
                || definition.Body.Samples.Count == 0)
            {
                return null;
            }

            ResolvedBody body = ResolvedBody.Resolve(definition.Body);
            Matrix4x4 world = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, part);
            Vector3 position = part.Limb != null
                ? world.MultiplyPoint3x4(ResolvedLimb.Resolve(part.Limb).RootSocket)
                : world.GetColumn(3);

            uint anchorSampleId = part.ParentId == CreatureDefinition.BodyId
                && part.ParentAttachment != null
                ? part.ParentAttachment.SegmentStartSampleId
                : 0u;

            return AnatomicalBodyRigLayout.ResolveAttachmentBoneId(
                body, definition.Forward, position, mirrored, anchorSampleId);
        }

        private static string ResolveBodyParentBoneId(
            ResolvedCreatureSnapshot snapshot, ResolvedPartSnapshot part, bool mirrored)
        {
            if (!snapshot.HasBody || snapshot.Body.SamplePositions.Count == 0) return null;

            Vector3 position = part.HasLimb
                ? part.PartFrameToCreatureSpace.MultiplyPoint3x4(part.Limb.RootSocket)
                : part.PartFrameToCreatureSpace.GetColumn(3);
            uint anchorSampleId = part.ParentId == CreatureDefinition.BodyId
                && part.HasBodySurfaceAnchor
                ? part.BodySurfaceAnchorSegmentStartSampleId
                : 0u;

            return AnatomicalBodyRigLayout.ResolveAttachmentBoneId(
                snapshot, position, mirrored, anchorSampleId);
        }
    }
}
