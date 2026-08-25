using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

namespace ProceduralCreature.Skeleton
{
    /// <summary>
    /// Infers a Skeleton from a validated CreatureDefinition. Every bone's
    /// position/rotation comes from CreaturePartWorldTransformResolver — the same
    /// utility the SDF compiler uses — so skeleton and geometry are always
    /// consistent with each other by construction, never independently derived
    /// and potentially disagreeing (the delta audit's original praise for this
    /// architecture: "sidesteps the hardest part of Spore-style rigging" by never
    /// touching the mesh at all).
    ///
    /// LIMB PARTS (CC-018 Phase 6): a part with a non-null <see cref="CreaturePart.Limb"/>
    /// emits N-1 bones, one per consecutive joint pair (N joints → N-1 bones),
    /// NOT one bone per part. Bone i spans Joints[i] → Joints[i+1]; its position
    /// is the resolved creature-space joint position, its rotation points along
    /// the segment, and its id is <c>part.Id + "_j" + i</c>. The skeleton is
    /// derived from the AUTHORED joints, never from the derived metaball samples,
    /// so render-geometry density can change without changing the rig.
    ///
    /// LIMB PARENT CHAIN: bone 0 of a limb attaches through ResolveParentBoneId.
    /// A child of a limb part (Foot, Hand, Claw, Decoration, another limb)
    /// attaches to that limb's TERMINAL bone (index N-2) — the terminal joint is
    /// the stable semantic child-attachment point (ADR-001 §3). A child's LOCAL
    /// SPACE is the limb's terminal joint (CC-018 child-at-tip frame in
    /// CreaturePartWorldTransformResolver), so a child authored at local (0,0,0)
    /// physically sits at the limb's tip and its bone lands on the terminal
    /// joint's creature-space position.
    ///
    /// MIRRORING RULE — READ BEFORE AUTHORING SYMMETRIC CREATURES: mirroring does
    /// NOT cascade to children automatically, matching the SDF compiler's
    /// identical per-part interpretation of CreaturePart.MirrorAcrossSymmetryPlane
    /// (see SdfProgramBuilder.CompilePart). To mirror an entire limb chain (e.g. a
    /// leg AND its foot), flag every part in that chain individually. Flagging
    /// only the leg produces a mirrored leg bone with the foot attached ONLY to
    /// the original (unmirrored) leg — geometrically the foot would only exist on
    /// one side, which is very likely not what an author mirroring a leg
    /// intended. This is deliberate (no implicit cascading, matching the "one
    /// mutation path" / no-hidden-magic principle elsewhere in this codebase) but
    /// is exactly the kind of thing worth a content-authoring warning in the
    /// editor once Phase 5 exists — flagged here rather than silently working
    /// around it in inference.
    ///
    /// A mirrored bone's parent link resolves to the mirrored copy of its DNA
    /// parent IF that parent is also flagged for mirroring, or to the single
    /// (unmirrored) parent bone otherwise — see ResolveParentBoneId. A mirrored
    /// limb emits a full mirrored chain (N-1 bones with MirrorSuffix ids), each
    /// joint mirrored by the creature-space X reflection of the part matrix — the
    /// SAME mirror the SDF compiler applies to the limb's metaball chain, so the
    /// mirrored rig and the mirrored geometry always coincide.
    /// </summary>
    public static class SkeletonInferrer
    {
        public const string MirrorSuffix = "_mirror";

        /// <summary>Bone-id separator for a limb's per-segment bones: part.Id + LimbJointBoneSeparator + i.</summary>
        public const string LimbJointBoneSeparator = "_j";

        /// <summary>
        /// The creature-space reflection across the X = 0 plane. A mirrored limb
        /// joint's position must be <c>S · (world · joint)</c>, i.e. the part
        /// matrix LEFT-multiplied by this reflection — the same result the SDF
        /// compiler's mirrored limb chain produces. (The conjugate
        /// <see cref="MirrorUtility.MirrorAcrossXPlane"/> is correct for a
        /// single bone sitting at the part origin, but not for a joint offset
        /// from that origin.)
        /// </summary>
        private static readonly Matrix4x4 ReflectAcrossX = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

        public static Skeleton Infer(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot infer a skeleton from a null CreatureDefinition.");
            }

            var skeleton = new Skeleton();

            AppendBodyBones(skeleton, definition);

            List<CreaturePart> orderedParts = definition.Parts
                .OrderBy(p => p.Id, System.StringComparer.Ordinal)
                .ToList();

            foreach (CreaturePart part in orderedParts)
            {
                bool shouldMirror = part.MirrorAcrossSymmetryPlane
                                     && definition.SymmetryMode != SymmetryMode.None;

                if (part.Limb != null)
                {
                    // A limb emits one bone per consecutive joint pair, plus a
                    // full mirrored chain when flagged.
                    AppendLimbBones(skeleton, definition, part, mirrored: false);
                    if (shouldMirror)
                    {
                        AppendLimbBones(skeleton, definition, part, mirrored: true);
                    }
                }
                else
                {
                    skeleton.Bones.Add(BuildBone(definition, part, mirrored: false));
                    if (shouldMirror)
                    {
                        skeleton.Bones.Add(BuildBone(definition, part, mirrored: true));
                    }
                }
            }

            return skeleton;
        }

        /// <summary>
        /// Builds the single origin bone for a non-limb part (the part's resolved
        /// creature-space placement). Mirrored parts use the conjugate
        /// <see cref="MirrorUtility.MirrorAcrossXPlane"/> — correct for a bone at
        /// the part origin, and it reflects the part's rotation so the mirrored
        /// bone points the right way.
        /// </summary>
        private static Bone BuildBone(CreatureDefinition definition, CreaturePart part, bool mirrored)
        {
            Matrix4x4 world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, part);
            if (mirrored)
            {
                world = MirrorUtility.MirrorAcrossXPlane(world);
            }

            return new Bone
            {
                Id = mirrored ? part.Id + MirrorSuffix : part.Id,
                ParentBoneId = ResolveParentBoneId(definition, part, mirrored),
                SourcePartId = part.Id,
                PartType = part.PartType,
                IsMirrored = mirrored,
                Position = world.GetColumn(3),
                Rotation = world.rotation,
            };
        }

        /// <summary>
        /// Builds the N-1 bones for a limb part (CC-018 Phase 6). Bone i spans
        /// Joints[i] → Joints[i+1]. Positions are the resolved creature-space
        /// joint positions through the part's matrix; when mirrored, the matrix is
        /// the creature-space X reflection LEFT-multiplied (not conjugated), so
        /// each mirrored joint lands at S · (unmirrored world position) — exactly
        /// where the SDF compiler's mirrored limb chain places its metaballs.
        /// Rotations look along each segment with the part's (reflected) world up
        /// as the up hint, with a fallback for the vertical-segment case where a
        /// look rotation would otherwise degenerate.
        ///
        /// CC-056A increment 2: the chain is consumed through the shared
        /// <see cref="ResolvedLimb"/> derivation — the same joint positions and
        /// structure the metaball sampler uses — never re-derived here. A
        /// structurally broken chain (empty, or containing a null joint) resolves
        /// to no bones; the validator rejects those before inference, so this only
        /// guards direct calls.
        /// </summary>
        private static void AppendLimbBones(Skeleton skeleton, CreatureDefinition definition, CreaturePart part, bool mirrored)
        {
            LimbChain limb = part.Limb;
            if (limb == null)
            {
                // Defensive: no chain, no bones.
                return;
            }

            ResolvedLimb resolved;
            try
            {
                resolved = ResolvedLimb.Resolve(limb);
            }
            catch (DomainException)
            {
                // Defensive: an empty or null-joint chain resolves to nothing.
                // The validator enforces MinLimbJointCount and rejects null
                // joints, so valid definitions never reach here.
                return;
            }

            if (resolved.JointPositions.Length < 2)
            {
                // Defensive: a single-joint chain emits no bones.
                return;
            }

            Matrix4x4 partMatrix = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, part);
            Vector3 upHint = partMatrix.rotation * Vector3.up;
            if (mirrored)
            {
                partMatrix = ReflectAcrossX * partMatrix;
                upHint = Vector3.Scale(upHint, new Vector3(-1f, 1f, 1f));
            }

            string suffix = mirrored ? MirrorSuffix : string.Empty;
            string rootParentBoneId = ResolveParentBoneId(definition, part, mirrored);
            string previousBoneId = null;

            for (int i = 0; i < resolved.JointPositions.Length - 1; i++)
            {
                Vector3 from = resolved.JointPositions[i];
                Vector3 to = resolved.JointPositions[i + 1];

                Vector3 fromWorld = partMatrix.MultiplyPoint3x4(from);
                Vector3 toWorld = partMatrix.MultiplyPoint3x4(to);
                Vector3 segmentDir = toWorld - fromWorld;
                Quaternion rotation = LimbBoneRotation(segmentDir, upHint);

                string boneId = part.Id + LimbJointBoneSeparator + i + suffix;
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
                    HasChildAttachmentPosition = i == resolved.JointPositions.Length - 2,
                    ChildAttachmentPosition = toWorld,
                    Rotation = rotation,
                });
                previousBoneId = boneId;
            }
        }

        /// <summary>
        /// A bone rotation that looks along <paramref name="segmentDir"/> with
        /// <paramref name="upHint"/> as the up direction. When the segment is
        /// (anti)parallel to the up hint the look rotation's cross product is zero
        /// (the default limb chain is a vertical leg, so this is the NORMAL case),
        /// so a non-parallel world axis is substituted deterministically before
        /// building the rotation.
        /// </summary>
        private static Quaternion LimbBoneRotation(Vector3 segmentDir, Vector3 upHint)
        {
            Vector3 forward = segmentDir.sqrMagnitude > 1e-8f ? segmentDir.normalized : Vector3.down;
            Vector3 up = upHint.sqrMagnitude > 1e-8f ? upHint.normalized : Vector3.up;

            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.9999f)
            {
                up = Mathf.Abs(Vector3.Dot(forward, Vector3.forward)) > 0.9999f
                    ? Vector3.right
                    : Vector3.forward;
            }

            return Quaternion.LookRotation(forward, up);
        }

        private static string ResolveParentBoneId(CreatureDefinition definition, CreaturePart part, bool mirrored)
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
                // The child of a limb attaches to the limb's TERMINAL bone (N
                // joints -> N-1 bones, so the last bone is index N-2). The
                // terminal joint is the stable child-attachment point.
                parentBoneBaseId = parentPart.Id + LimbJointBoneSeparator + (parentPart.Limb.Joints.Count - 2);
            }
            else
            {
                // Existing rule: an unmirrored part's bone id is exactly the
                // source part id.
                parentBoneBaseId = part.ParentId;
            }

            return mirrored && parentIsAlsoMirrored
                ? parentBoneBaseId + MirrorSuffix
                : parentBoneBaseId;
        }

        private static void AppendBodyBones(Skeleton skeleton, CreatureDefinition definition)
        {
            if (definition.Body == null || definition.Body.Samples == null
                || definition.Body.Samples.Count == 0)
            {
                return;
            }

            BodyFrame[] frames = BodyFrameResolver.ComputeSampleFrames(
                definition.Body.Samples, definition.Forward);
            for (int i = 0; i < definition.Body.Samples.Count; i++)
            {
                Vector3 position = definition.Body.Samples[i].Position;
                bool hasSegment = i < definition.Body.Samples.Count - 1;
                Vector3 endPosition = hasSegment
                    ? definition.Body.Samples[i + 1].Position
                    : position;
                string boneId = CreatureDefinition.BodyId + LimbJointBoneSeparator
                    + definition.Body.Samples[i].Id;
                string parentBoneId = i == 0
                    ? null
                    : CreatureDefinition.BodyId + LimbJointBoneSeparator
                        + definition.Body.Samples[i - 1].Id;

                skeleton.Bones.Add(new Bone
                {
                    Id = boneId,
                    ParentBoneId = parentBoneId,
                    SourcePartId = CreatureDefinition.BodyId,
                    PartType = PartType.Body,
                    Position = position,
                    HasSegment = hasSegment,
                    EndPosition = endPosition,
                    Rotation = Quaternion.LookRotation(frames[i].Tangent, frames[i].Normal),
                });
            }
        }

        private static string ResolveBodyParentBoneId(
            CreatureDefinition definition, CreaturePart part, bool mirrored)
        {
            if (definition.Body == null || definition.Body.Samples == null
                || definition.Body.Samples.Count == 0)
            {
                return null;
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

            return CreatureDefinition.BodyId + LimbJointBoneSeparator
                + definition.Body.Samples[nearestIndex].Id;
        }
    }
}
