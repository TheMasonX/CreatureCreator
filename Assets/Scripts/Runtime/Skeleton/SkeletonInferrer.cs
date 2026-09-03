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
        /// <summary>Suffix on a mirrored bone id (owned by <see cref="SemanticBoneResolver"/>).</summary>
        public const string MirrorSuffix = SemanticBoneResolver.MirrorSuffix;

        /// <summary>Bone-id separator for a limb's per-segment bones: part.Id + LimbJointBoneSeparator + i.</summary>
        public const string LimbJointBoneSeparator = SemanticBoneResolver.LimbJointBoneSeparator;

        /// <summary>
        /// The creature-space reflection across the X = 0 plane (point form).
        /// A mirrored limb joint's position must be <c>S · (world · joint)</c>,
        /// i.e. the part matrix LEFT-multiplied by this reflection — the same
        /// result the SDF compiler's mirrored limb chain produces. (The conjugate
        /// <see cref="MirrorUtility.MirrorAcrossXPlane"/> is correct for a single
        /// bone sitting at the part origin, but not for a joint offset from that
        /// origin.) Owned by <see cref="SemanticBoneResolver"/>.
        /// </summary>
        private static readonly Matrix4x4 ReflectAcrossX = SemanticBoneResolver.ReflectAcrossX;

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
                // Direct inference calls can receive malformed DNA before validation.
                // Preserve the independently resolvable Body rather than leaking a
                // morphology exception from the defensive adapter.
                bool hasBody = definition.Body != null
                               && definition.Body.Samples != null
                               && definition.Body.Samples.Count > 0;
                AppendBodyBones(skeleton, definition,
                    hasBody ? ResolvedBody.Resolve(definition.Body) : default,
                    hasBody);
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
                        // Skip entries whose ancestor chain is also malformed.
                    }
                }
                return skeleton;
            }

            AppendBodyBones(skeleton, definition, snapshot.Body, snapshot.HasBody);

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
                    // A limb emits one bone per consecutive joint pair, plus a
                    // full mirrored chain when flagged.
                    AppendLimbBones(skeleton, definition, part, resolvedPart, mirrored: false);
                    if (shouldMirror)
                    {
                        AppendLimbBones(skeleton, definition, part, resolvedPart, mirrored: true);
                    }
                }
                else
                {
                    skeleton.Bones.Add(BuildBone(definition, part, resolvedPart, mirrored: false));
                    if (shouldMirror)
                    {
                        skeleton.Bones.Add(BuildBone(definition, part, resolvedPart, mirrored: true));
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
        private static Bone BuildBone(CreatureDefinition definition, CreaturePart part,
            ResolvedPartSnapshot resolvedPart, bool mirrored)
        {
            Matrix4x4 world = resolvedPart.PartFrameToCreatureSpace;
            if (mirrored)
            {
                world = MirrorUtility.MirrorAcrossXPlane(world);
            }

            return new Bone
            {
                Id = SemanticBoneResolver.ResolvePartRootBoneId(part, mirrored),
                ParentBoneId = SemanticBoneResolver.ResolveParentBoneId(definition, part, mirrored),
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
        private static void AppendLimbBones(Skeleton skeleton, CreatureDefinition definition,
            CreaturePart part, ResolvedPartSnapshot resolvedPart, bool mirrored)
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
                resolved = resolvedPart.Limb;
            }
            catch (DomainException)
            {
                return;
            }

            if (resolved.JointPositions.Count < 2)
            {
                // Defensive: a single-joint chain emits no bones.
                return;
            }

            Matrix4x4 partMatrix = resolvedPart.PartFrameToCreatureSpace;
            Vector3 upHint = partMatrix.rotation * Vector3.up;
            if (mirrored)
            {
                partMatrix = ReflectAcrossX * partMatrix;
                upHint = Vector3.Scale(upHint, new Vector3(-1f, 1f, 1f));
            }

            string rootParentBoneId = SemanticBoneResolver.ResolveParentBoneId(definition, part, mirrored);
            string previousBoneId = null;

            for (int i = 0; i < resolved.JointPositions.Count - 1; i++)
            {
                Vector3 from = resolved.JointPositions[i];
                Vector3 to = resolved.JointPositions[i + 1];

                Vector3 fromWorld = partMatrix.MultiplyPoint3x4(from);
                Vector3 toWorld = partMatrix.MultiplyPoint3x4(to);
                Vector3 segmentDir = toWorld - fromWorld;
                Quaternion rotation = LimbBoneRotation(segmentDir, upHint);

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

        private static void AppendBodyBones(Skeleton skeleton, CreatureDefinition definition,
            ResolvedBody resolved, bool hasBody)
        {
            if (!hasBody)
            {
                return;
            }

            BodyFrame[] frames = BodyFrameResolver.ComputeSampleFrames(
                resolved, definition.Forward);
            for (int i = 0; i < resolved.SamplePositions.Count; i++)
            {
                Vector3 position = resolved.SamplePositions[i];
                bool hasSegment = i < resolved.SamplePositions.Count - 1;
                Vector3 endPosition = hasSegment
                    ? resolved.SamplePositions[i + 1]
                    : position;
                string boneId = SemanticBoneResolver.ResolveBodySocketBoneId(
                    resolved.SampleIds[i]);
                string parentBoneId = i == 0
                    ? null
                    : SemanticBoneResolver.ResolveBodySocketBoneId(
                        resolved.SampleIds[i - 1]);

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
    }
}
