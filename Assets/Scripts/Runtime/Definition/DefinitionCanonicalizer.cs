using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

            if (result.Body == null || result.Body.Samples == null)
            {
                throw new DomainException("Cannot canonicalize a definition without a Body spline.");
            }

            foreach (BodySample sample in result.Body.Samples)
            {
                if (sample == null || !IsFinite(sample.Position) || !IsFinite(sample.Radius))
                {
                    throw new DomainException("Cannot canonicalize a Body spline with non-finite samples.");
                }
                sample.Position = new Vector3(
                    GenerationTolerances.Quantize(sample.Position.x),
                    GenerationTolerances.Quantize(sample.Position.y),
                    GenerationTolerances.Quantize(sample.Position.z));
                sample.Radius = GenerationTolerances.Quantize(sample.Radius);
            }

            CanonicalizeBodyAppearance(result.Body.Appearance);

            if (!IsFinite(result.Forward) || result.Forward.sqrMagnitude <= 0f)
            {
                throw new DomainException("Cannot canonicalize a definition with an invalid Forward vector.");
            }
            Vector3 forward = result.Forward.normalized;
            result.Forward = new Vector3(
                GenerationTolerances.Quantize(forward.x),
                GenerationTolerances.Quantize(forward.y),
                GenerationTolerances.Quantize(forward.z));

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
            var childrenByParent = result.Parts
                .Where(p => p != null)
                .GroupBy(p => p.ParentId ?? string.Empty)
                .ToDictionary(group => group.Key,
                    group => group.OrderBy(p => p.Id, System.StringComparer.Ordinal).ToList());
            var orderedParts = new List<CreaturePart>();
            AppendChildren(CreatureDefinition.BodyId, childrenByParent, orderedParts);
            foreach (CreaturePart part in result.Parts
                .Where(p => p != null && !orderedParts.Contains(p))
                .OrderBy(p => p.Id, System.StringComparer.Ordinal))
            {
                orderedParts.Add(part);
            }
            result.Parts = orderedParts;

            return result;
        }

        /// <summary>
        /// Canonicalizes the Body vertical-gradient appearance (CC-025): quantizes
        /// stop T/color components and the offset, and sorts each gradient's stops
        /// into non-decreasing T order for deterministic serialization. Throws on
        /// a null appearance or empty/non-finite gradients — canonicalization is
        /// not a repair pass, matching the body-spline and transform rules above.
        /// </summary>
        private static void CanonicalizeBodyAppearance(BodyVerticalGradientAppearance appearance)
        {
            if (appearance == null)
            {
                throw new DomainException("Cannot canonicalize a definition without a Body vertical-gradient appearance.");
            }
            CanonicalizeGradient(appearance.TopGradient, "top");
            CanonicalizeGradient(appearance.BottomGradient, "bottom");
            appearance.VerticalOffset = GenerationTolerances.Quantize(appearance.VerticalOffset);
        }

        private static void CanonicalizeGradient(ColorGradient gradient, string name)
        {
            if (gradient == null || gradient.Stops == null || gradient.Stops.Count == 0)
            {
                throw new DomainException($"Cannot canonicalize a Body {name} gradient with no stops.");
            }
            foreach (GradientColorStop stop in gradient.Stops)
            {
                if (!stop.IsFinite())
                {
                    throw new DomainException($"Cannot canonicalize a Body {name} gradient with a non-finite stop.");
                }
            }
            gradient.Stops = gradient.Stops
                .OrderBy(s => s.T)
                .Select(s => new GradientColorStop(
                    GenerationTolerances.Quantize(s.T),
                    new Color(
                        GenerationTolerances.Quantize(s.Color.r),
                        GenerationTolerances.Quantize(s.Color.g),
                        GenerationTolerances.Quantize(s.Color.b),
                        GenerationTolerances.Quantize(s.Color.a))))
                .ToList();
        }

        private static void AppendChildren(string parentId,
            Dictionary<string, List<CreaturePart>> childrenByParent,
            List<CreaturePart> orderedParts)
        {
            if (!childrenByParent.TryGetValue(parentId, out List<CreaturePart> children)) return;
            foreach (CreaturePart child in children)
            {
                orderedParts.Add(child);
                AppendChildren(child.Id, childrenByParent, orderedParts);
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
