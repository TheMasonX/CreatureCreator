using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Appearance
{
    public readonly struct ResolvedAppearance
    {
        public readonly Color BaseColor;
        public readonly int NoiseSeed;
        public readonly float NoiseScale;

        public ResolvedAppearance(Color baseColor, int noiseSeed, float noiseScale)
        {
            BaseColor = baseColor;
            NoiseSeed = noiseSeed;
            NoiseScale = noiseScale;
        }
    }

    /// <summary>
    /// SdfProgramBuilder.Compile folds every part into a single unioned SDF — by
    /// the time a mesh exists, there is no per-vertex record of which
    /// CreaturePart it "belongs to." This resolver answers that question after
    /// the fact: for a given surface point, it evaluates every part's own
    /// individually-compiled node (via SdfProgramBuilder.CompileIndividualParts,
    /// which already handles each part's transform and symmetry mirror) and
    /// picks whichever part's surface is closest to that point.
    ///
    /// The Body spline's field is part of the same nearest-surface decision: a
    /// point whose closest surface is the Body resolves to the Body's
    /// vertical-gradient appearance (CC-025) instead of any part's flat color.
    /// That gradient color is computed and carried as <see cref="ResolvedAppearance.BaseColor"/>
    /// so the baker needs no knowledge of the gradient model.
    ///
    /// KNOWN SIMPLIFICATION: this picks a single nearest part rather than
    /// blending appearance between the nearest two — meaning color can change
    /// abruptly right at a smooth-blended geometric seam between two parts with
    /// different BaseColor. Smooth appearance blending at seams (matching the
    /// geometric smooth-min blending) is a reasonable hardening target, not
    /// implemented here — flagged rather than silently approximated as "good
    /// enough."
    /// </summary>
    public static class PartAppearanceSampler
    {
        public static ResolvedAppearance Resolve(CreatureDefinition definition, Vector3 position)
        {
            if (definition == null) throw new DomainException("definition must not be null.");

            bool hasBody = definition.Body != null
                && definition.Body.Samples != null
                && definition.Body.Samples.Count > 0;

            if (definition.Parts.Count == 0 && !hasBody)
            {
                return new ResolvedAppearance(AppearanceDefinition.Default.BaseColor, 0, 1f);
            }

            var compiledParts = SdfProgramBuilder.CompileIndividualParts(definition);
            ISdfNode bodyNode = SdfProgramBuilder.CompileBodyField(definition);

            CreaturePart nearestPart = null;
            float nearestAbsDistance = float.PositiveInfinity;

            foreach ((CreaturePart part, ISdfNode node) in compiledParts)
            {
                float distance = Mathf.Abs(node.Evaluate(position));
                if (distance < nearestAbsDistance)
                {
                    nearestAbsDistance = distance;
                    nearestPart = part;
                }
            }

            float bodyAbsDistance = bodyNode == null
                ? float.PositiveInfinity
                : Mathf.Abs(bodyNode.Evaluate(position));

            // The Body owns this surface point. Its gradient color (or default
            // flat gray) becomes the base color; noise keeps the same gentle
            // triplanar surface variation the baker applies to parts.
            if (bodyNode != null && bodyAbsDistance <= nearestAbsDistance)
            {
                Color bodyColor = BodyVerticalGradientSampler.EvaluateColor(definition, position);
                return new ResolvedAppearance(bodyColor, 0, 1f);
            }

            if (nearestPart == null)
            {
                return new ResolvedAppearance(AppearanceDefinition.Default.BaseColor, 0, 1f);
            }

            AppearanceDefinition appearance = nearestPart.Appearance;
            return new ResolvedAppearance(appearance.BaseColor, appearance.NoiseSeed, appearance.NoiseScale);
        }
    }
}
