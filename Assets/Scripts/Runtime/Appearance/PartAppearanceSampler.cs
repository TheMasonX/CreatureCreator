using UnityEngine;
using Unity.Collections;
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

        /// <summary>
        /// The nearest part's optional submaterial key (CC-028), or null when no
        /// part owns this surface point (Body/default) or the part has no override.
        /// Resolution of the key to an actual material is a render-layer concern
        /// (<see cref="MaterialResolver"/>); the sampler only surfaces which key
        /// applies so editor and runtime previews share the same decision.
        /// </summary>
        public readonly string MaterialKey;

        public ResolvedAppearance(Color baseColor, int noiseSeed, float noiseScale, string materialKey = null)
        {
            BaseColor = baseColor;
            NoiseSeed = noiseSeed;
            NoiseScale = noiseScale;
            MaterialKey = materialKey;
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
            using (Resolver resolver = CreateResolver(definition))
            {
                return resolver.Resolve(position);
            }
        }

        public static Resolver CreateResolver(CreatureDefinition definition)
        {
            if (definition == null) throw new DomainException("definition must not be null.");

            return new Resolver(definition);
        }

        public sealed class Resolver : System.IDisposable
        {
            private readonly CreatureDefinition _definition;
            private readonly System.Collections.Generic.List<(CreaturePart Part, SdfProgram Program)> _compiledParts;
            private readonly SdfProgram _bodyProgram;
            private readonly NativeArray<float> _scratchValues;

            internal Resolver(CreatureDefinition definition)
            {
                _definition = definition;
                _compiledParts = SdfProgramBuilder.CompileIndividualPartsPortable(definition);
                _bodyProgram = SdfProgramBuilder.CompilePortableBodyField(definition);
                int scratchLength = _bodyProgram.Operations.Length;
                foreach ((CreaturePart part, SdfProgram program) in _compiledParts)
                {
                    scratchLength = Mathf.Max(scratchLength, program.Operations.Length);
                }
                _scratchValues = new NativeArray<float>(Mathf.Max(scratchLength, 1), Allocator.Persistent);
            }

            public void Dispose()
            {
                foreach ((CreaturePart part, SdfProgram program) in _compiledParts)
                {
                    program.Dispose();
                }
                _bodyProgram.Dispose();
                if (_scratchValues.IsCreated)
                {
                    _scratchValues.Dispose();
                }
            }

            public ResolvedAppearance Resolve(Vector3 position)
            {
                bool hasBody = _definition.Body != null
                    && _definition.Body.Samples != null
                    && _definition.Body.Samples.Count > 0;

                if (_definition.Parts.Count == 0 && !hasBody)
                {
                    return new ResolvedAppearance(AppearanceDefinition.Default.BaseColor, 0, 1f);
                }

                CreaturePart nearestPart = null;
                float nearestAbsDistance = float.PositiveInfinity;

                foreach ((CreaturePart part, SdfProgram program) in _compiledParts)
                {
                    float distance = Mathf.Abs(SdfProgramEvaluator.Evaluate(program,
                        new Unity.Mathematics.float3(position.x, position.y, position.z), _scratchValues));
                    if (distance < nearestAbsDistance)
                    {
                        nearestAbsDistance = distance;
                        nearestPart = part;
                    }
                }

                float bodyAbsDistance = !_bodyProgram.Operations.IsCreated
                    ? float.PositiveInfinity
                    : Mathf.Abs(SdfProgramEvaluator.Evaluate(_bodyProgram,
                        new Unity.Mathematics.float3(position.x, position.y, position.z), _scratchValues));

                // The Body owns this surface point. Its gradient color (or default
                // flat gray) becomes the base color; noise keeps the same gentle
                // triplanar surface variation the baker applies to parts.
                if (_bodyProgram.Operations.IsCreated && bodyAbsDistance <= nearestAbsDistance)
                {
                    Color bodyColor = BodyVerticalGradientSampler.EvaluateColor(_definition, position);
                    return new ResolvedAppearance(bodyColor, 0, 1f);
                }

                if (nearestPart == null)
                {
                    return new ResolvedAppearance(AppearanceDefinition.Default.BaseColor, 0, 1f);
                }

                AppearanceDefinition appearance = nearestPart.Appearance;
                return new ResolvedAppearance(
                    appearance.BaseColor, appearance.NoiseSeed, appearance.NoiseScale,
                    string.IsNullOrWhiteSpace(appearance.MaterialKey) ? null : appearance.MaterialKey);
            }
        }
    }
}
