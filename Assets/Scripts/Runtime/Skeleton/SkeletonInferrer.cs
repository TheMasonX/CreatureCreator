using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

namespace ProceduralCreature.Skeleton
{
    /// <summary>
    /// Infers the semantic runtime skeleton from resolved creature morphology.
    /// Dense Body samples remain morphology/SDF data; the character skeleton uses
    /// the compact anatomical body layout plus authored limb chains.
    /// </summary>
    public static class SkeletonInferrer
    {
        public const string MirrorSuffix = SemanticBoneResolver.MirrorSuffix;
        public const string LimbJointBoneSeparator = SemanticBoneResolver.LimbJointBoneSeparator;

        public static Skeleton Infer(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot infer a skeleton from a null CreatureDefinition.");
            }

            var skeleton = new Skeleton();
            ResolvedCreatureSnapshot snapshot;
            try
            {
                snapshot = ResolvedCreatureSnapshot.Resolve(definition);
            }
            catch (DomainException)
            {
                // Direct callers may supply malformed DNA before validation. Keep the
                // independently resolvable Body and valid standalone parts available
                // for diagnostics without leaking the resolution exception here.
                bool hasBody = definition.Body != null
                               && definition.Body.Samples != null
                               && definition.Body.Samples.Count > 0;
                AppendBodyBones(
                    skeleton,
                    hasBody ? ResolvedBody.Resolve(definition.Body) : null,
                    definition.Forward,
                    hasBody,
                    resolvedSnapshot: null);

                IReadOnlyList<CreaturePart> parts = definition.CreateHierarchyIndex().Parts;
                for (int i = 0; i < parts.Count; i++)
                {
                    CreaturePart part = parts[i];
                    if (part == null || part.Limb != null) continue;

                    try
                    {
                        Matrix4x4 world = CreaturePartWorldTransformResolver
                            .ResolvePartFrameToCreatureSpace(definition, part);
                        skeleton.Bones.Add(new Bone
                        {
                            Id = SemanticBoneResolver.ResolvePartRootBoneId(part, mirrored: false),
                            ParentBoneId = SemanticBoneResolver.ResolveParentBoneId(
                                definition, part, mirrored: false),
                            SourcePartId = part.Id,
                            PartType = part.PartType,
                            Position = world.GetColumn(3),
                            Rotation = world.rotation,
                        });
                    }
                    catch (DomainException)
                    {
                        // Broken ancestry remains a validation problem; do not invent a
                        // disconnected bone just to keep inference going.
                    }
                }

                return skeleton;
            }

            AppendBodyBones(
                skeleton,
                snapshot.Body,
                snapshot.Forward,
                snapshot.HasBody,
                snapshot);

            List<CreaturePart> orderedParts = definition.Parts
                .OrderBy(p => p.Id, System.StringComparer.Ordinal)
                .ToList();

            foreach (CreaturePart part in orderedParts)
            {
                snapshot.TryGetPart(part.Id, out ResolvedPartSnapshot resolvedPart);
                bool shouldMirror = part.MirrorAcrossSymmetryPlane
                                     && definition.SymmetryMode != SymmetryMode.None;

                if (part.Limb != null)
                {
                    AppendLimbBones(
                        skeleton, snapshot, part, resolvedPart, mirrored: false);
                    if (shouldMirror)
                    {
                        AppendLimbBones(
                            skeleton, snapshot, part, resolvedPart, mirrored: true);
                    }
                }
                else
                {
                    skeleton.Bones.Add(BuildBone(part, resolvedPart, mirrored: false, snapshot));
                    if (shouldMirror)
                    {
                        skeleton.Bones.Add(BuildBone(part, resolvedPart, mirrored: true, snapshot));
                    }
                }
            }

            return skeleton;
        }

        private static Bone BuildBone(
            CreaturePart part,
            ResolvedPartSnapshot resolvedPart,
            bool mirrored,
            ResolvedCreatureSnapshot snapshot)
        {
            Matrix4x4 world = resolvedPart.PartFrameToCreatureSpace;
            if (mirrored)
            {
                world = MirrorUtility.MirrorAcrossXPlane(world);
            }

            return new Bone
            {
                Id = SemanticBoneResolver.ResolvePartRootBoneId(part, mirrored),
                ParentBoneId = SemanticBoneResolver.ResolveParentBoneId(snapshot, resolvedPart, mirrored),
                SourcePartId = part.Id,
                PartType = part.PartType,
                IsMirrored = mirrored,
                Position = world.GetColumn(3),
                Rotation = world.rotation,
            };
        }

        private static void AppendLimbBones(
            Skeleton skeleton,
            ResolvedCreatureSnapshot snapshot,
            CreaturePart part,
            ResolvedPartSnapshot resolvedPart,
            bool mirrored)
        {
            if (!resolvedPart.HasLimb) return;

            ResolvedLimb resolved = resolvedPart.Limb;
            if (resolved.JointPositions == null || resolved.JointPositions.Count < 2)
            {
                return;
            }

            Matrix4x4 partMatrix = resolvedPart.PartFrameToCreatureSpace;
            Vector3 upHint = partMatrix.rotation * Vector3.up;
            if (mirrored)
            {
                partMatrix = MirrorUtility.ReflectTransformAcrossX(partMatrix);
                upHint = Vector3.Scale(upHint, new Vector3(-1f, 1f, 1f));
            }

            string rootParentBoneId = SemanticBoneResolver.ResolveParentBoneId(
                snapshot, resolvedPart, mirrored);
            string previousBoneId = null;
            Quaternion previousRotation = Quaternion.identity;

            for (int i = 0; i < resolved.JointPositions.Count - 1; i++)
            {
                Vector3 fromWorld = partMatrix.MultiplyPoint3x4(resolved.JointPositions[i]);
                Vector3 toWorld = partMatrix.MultiplyPoint3x4(resolved.JointPositions[i + 1]);
                Quaternion rotation = ResolveLimbBoneRotation(toWorld - fromWorld, upHint);
                previousRotation = rotation;

                string boneId = SemanticBoneResolver.ResolveLimbSegmentBoneId(part, i, mirrored);
                skeleton.Bones.Add(new Bone
                {
                    Id = boneId,
                    ParentBoneId = i == 0 ? rootParentBoneId : previousBoneId,
                    SourcePartId = part.Id,
                    PartType = part.PartType,
                    IsMirrored = mirrored,
                    Position = fromWorld,
                    HasSegment = true,
                    EndPosition = toWorld,
                    HasChildAttachmentPosition = i == resolved.JointPositions.Count - 2,
                    ChildAttachmentPosition = toWorld,
                    Rotation = rotation,
                });
                previousBoneId = boneId;
            }

            int terminalIndex = resolved.JointPositions.Count - 1;
            Vector3 terminalPosition = partMatrix.MultiplyPoint3x4(
                resolved.JointPositions[terminalIndex]);
            skeleton.Bones.Add(new Bone
            {
                Id = SemanticBoneResolver.ResolveLimbJointBoneId(part, terminalIndex, mirrored),
                ParentBoneId = previousBoneId,
                SourcePartId = part.Id,
                PartType = part.PartType,
                IsMirrored = mirrored,
                Position = terminalPosition,
                Rotation = previousBoneId == null ? Quaternion.identity : previousRotation,
            });
        }

        private static Quaternion ResolveLimbBoneRotation(Vector3 segmentDirection, Vector3 upHint)
        {
            Vector3 forward = segmentDirection.sqrMagnitude > 1e-8f
                ? segmentDirection.normalized
                : Vector3.down;
            Vector3 up = upHint.sqrMagnitude > 1e-8f
                ? upHint.normalized
                : Vector3.up;

            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.9999f)
            {
                up = Mathf.Abs(Vector3.Dot(forward, Vector3.forward)) > 0.9999f
                    ? Vector3.right
                    : Vector3.forward;
            }

            return Quaternion.LookRotation(forward, up);
        }

        private static void AppendBodyBones(
            Skeleton skeleton,
            ResolvedBody resolved,
            Vector3 forward,
            bool hasBody,
            ResolvedCreatureSnapshot resolvedSnapshot)
        {
            if (!hasBody || resolved == null)
            {
                return;
            }

            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> layout = resolvedSnapshot != null
                ? AnatomicalBodyRigLayout.Build(resolvedSnapshot)
                : AnatomicalBodyRigLayout.Build(resolved, forward);

            for (int i = 0; i < layout.Count; i++)
            {
                AnatomicalBodyRigLayout.BoneSpec spec = layout[i];
                skeleton.Bones.Add(new Bone
                {
                    Id = spec.Id,
                    ParentBoneId = spec.ParentBoneId,
                    SourcePartId = CreatureDefinition.BodyId,
                    PartType = PartType.Body,
                    IsMirrored = false,
                    Position = spec.Position,
                    HasSegment = spec.HasSegment,
                    EndPosition = spec.EndPosition,
                    Rotation = spec.Rotation,
                });
            }
        }
    }
}
