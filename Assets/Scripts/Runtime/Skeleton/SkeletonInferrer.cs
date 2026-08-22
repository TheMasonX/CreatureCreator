using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

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
    /// (unmirrored) parent bone otherwise — see ResolveParentBoneId.
    /// </summary>
    public static class SkeletonInferrer
    {
        public const string MirrorSuffix = "_mirror";

        public static Skeleton Infer(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot infer a skeleton from a null CreatureDefinition.");
            }

            var skeleton = new Skeleton();

            List<CreaturePart> orderedParts = definition.Parts
                .OrderBy(p => p.Id, System.StringComparer.Ordinal)
                .ToList();

            foreach (CreaturePart part in orderedParts)
            {
                skeleton.Bones.Add(BuildBone(definition, part, mirrored: false));

                bool shouldMirror = part.MirrorAcrossSymmetryPlane
                                     && definition.SymmetryMode != SymmetryMode.None;
                if (shouldMirror)
                {
                    skeleton.Bones.Add(BuildBone(definition, part, mirrored: true));
                }
            }

            return skeleton;
        }

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

        private static string ResolveParentBoneId(CreatureDefinition definition, CreaturePart part, bool mirrored)
        {
            if (part.ParentId == null)
            {
                return null; // root bone, mirrored or not
            }

            if (!mirrored)
            {
                return part.ParentId; // unmirrored bone ids are exactly the source part id
            }

            CreaturePart parentPart = definition.FindPart(part.ParentId);
            bool parentIsAlsoMirrored = parentPart != null
                                         && parentPart.MirrorAcrossSymmetryPlane
                                         && definition.SymmetryMode != SymmetryMode.None;

            return parentIsAlsoMirrored ? parentPart.Id + MirrorSuffix : part.ParentId;
        }
    }
}
