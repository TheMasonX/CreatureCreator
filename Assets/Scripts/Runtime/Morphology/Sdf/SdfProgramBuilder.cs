using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Morphology.Sdf
{
    /// <summary>
    /// Compiles a validated CreatureDefinition into a single composed ISdfNode
    /// (implementation guide §11 "Definition-to-SDF compiler"). Read-only over its
    /// input — never mutates or writes back to the definition (§16 "the compiler
    /// never modifies DNA").
    ///
    /// Callers must run DefinitionValidator first; this class assumes valid input
    /// and throws DomainException (a programmer-error signal, not a data-error
    /// signal) if that assumption is violated, per the project's exception policy.
    ///
    /// DETERMINISTIC ORDERING RULE (§2.3 "Establish deterministic ordering rules"):
    /// parts are compiled and folded into the union chain in ascending Id order
    /// (ordinal string comparison) — the same rule DefinitionCanonicalizer uses for
    /// serialization — regardless of Parts list authoring/insertion order. Parent
    /// hierarchy (used only for computing each part's creature-space transform via
    /// CreaturePartWorldTransformResolver) is kept entirely separate from this fold
    /// order, per §2.3's "keep assembly parent relationships separate from SDF
    /// composition order."
    /// </summary>
    public static class SdfProgramBuilder
    {
        public static ISdfNode Compile(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot compile a null CreatureDefinition.");
            }

            if (definition.Parts.Count == 0)
            {
                return new EmptySdfNode();
            }

            List<(CreaturePart Part, ISdfNode Node)> compiled = CompileIndividualParts(definition);

            ISdfNode accumulated = compiled[0].Node;
            for (int i = 1; i < compiled.Count; i++)
            {
                float blendRadius = compiled[i].Part.Shape.SmoothBlendRadius;
                accumulated = new SmoothUnionNode(accumulated, compiled[i].Node, blendRadius);
            }

            return accumulated;
        }

        /// <summary>
        /// Compiles each part into its own standalone ISdfNode (including its own
        /// transform and, if flagged, its own symmetry mirror) WITHOUT folding them
        /// into a single unioned body. Exposed for consumers that need to reason
        /// about individual parts rather than the composed whole — Phase 4's
        /// appearance baker uses this to determine which part's appearance
        /// parameters apply at a given surface point (see
        /// Appearance/PartAppearanceSampler.cs), and Phase 6's skeleton inferer
        /// will have the same need for per-part identity. Ordered the same way
        /// Compile's fold order is (ascending Id), though callers needing per-part
        /// data generally don't care about fold order — it's just a stable,
        /// deterministic order to hand back.
        /// </summary>
        public static List<(CreaturePart Part, ISdfNode Node)> CompileIndividualParts(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot compile a null CreatureDefinition.");
            }

            List<CreaturePart> orderedParts = definition.Parts
                .OrderBy(p => p.Id, System.StringComparer.Ordinal)
                .ToList();

            return orderedParts
                .Select(part => (part, CompilePart(definition, part)))
                .ToList();
        }

        private static ISdfNode CompilePart(CreatureDefinition definition, CreaturePart part)
        {
            ISdfNode primitive = CompilePrimitive(part.Shape);

            Matrix4x4 localToCreatureSpace =
                CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, part);
            ISdfNode transformed = new TransformNode(primitive, localToCreatureSpace);

            bool shouldMirror = part.MirrorAcrossSymmetryPlane
                                 && definition.SymmetryMode != SymmetryMode.None;

            return shouldMirror ? new SymmetryNode(transformed) : transformed;
        }

        private static ISdfNode CompilePrimitive(ShapeDefinition shape)
        {
            switch (shape.Type)
            {
                case ShapeType.Sphere:
                    return new SphereSdfNode(shape.PrimarySize);
                case ShapeType.Box:
                    return new BoxSdfNode(new Vector3(shape.PrimarySize, shape.PrimarySize, shape.PrimarySize));
                case ShapeType.Capsule:
                    return new CapsuleSdfNode(shape.PrimarySize);
                case ShapeType.Ellipsoid:
                    return new EllipsoidSdfNode(shape.PrimarySize);
                default:
                    // Validated definitions never reach here (DefinitionValidator's
                    // UnsupportedPartType-adjacent checks run on PartType, and
                    // ShapeType is a closed enum with no "unknown" value today) —
                    // this is a genuine future-proofing guard for whoever adds a
                    // ShapeType case without adding it here too.
                    throw new DomainException($"No SDF primitive mapping exists for ShapeType.{shape.Type}.");
            }
        }
    }
}
