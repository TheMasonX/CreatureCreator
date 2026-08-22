using System.Linq;
using ProceduralCreature.Common;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Applies the canonical representation rule to a CreatureDefinition: quantized
    /// position/rotation/scale, normalized rotation, sorted parts for stable
    /// serialization. Called explicitly at mutation-commit and serialization
    /// boundaries only — NOT during interactive/temporary editing (§2.3: "Do not
    /// repeatedly quantize internal temporary values during iterative numeric
    /// algorithms").
    ///
    /// This does not validate. Canonicalizing an invalid definition (e.g. one with a
    /// NaN transform) throws DomainException, because calling code is expected to
    /// validate first — canonicalization is not a repair pass (implementation guide
    /// §14: "Never silently clamp or rewrite a persisted definition during load").
    /// </summary>
    public static class DefinitionCanonicalizer
    {
        /// <summary>
        /// Returns a new CreatureDefinition with every part's transform quantized and
        /// parts sorted into a stable order (by Id, ordinal) for deterministic
        /// serialization (Sprint 1.3: "stable property ordering"). The input is not
        /// mutated.
        /// </summary>
        public static CreatureDefinition Canonicalize(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("Cannot canonicalize a null CreatureDefinition.");
            }

            CreatureDefinition result = definition.Clone();

            foreach (CreaturePart part in result.Parts)
            {
                if (!part.Transform.IsFinite())
                {
                    throw new DomainException(
                        $"Part '{part.Id}' has a non-finite transform; validate before canonicalizing.");
                }

                part.Transform = part.Transform.Quantized();
            }

            // Stable ordering independent of authoring/insertion order — this is what
            // makes "definition order independence where semantics are unchanged"
            // (§13.4 determinism tests) hold for serialization output.
            result.Parts = result.Parts
                .OrderBy(p => p.Id, System.StringComparer.Ordinal)
                .ToList();

            return result;
        }
    }
}
