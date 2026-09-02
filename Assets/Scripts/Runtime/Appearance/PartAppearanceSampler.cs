using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
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

        internal static Resolver CreateResolver(
            CreatureDefinition definition,
            System.Collections.Generic.List<(CreaturePart Part, SdfProgram Program)> compiledParts,
            SdfProgram bodyProgram)
        {
            if (definition == null) throw new DomainException("definition must not be null.");
            return new Resolver(definition, compiledParts, bodyProgram);
        }

        public sealed class Resolver : System.IDisposable
        {
            private readonly CreatureDefinition _definition;
            private readonly System.Collections.Generic.List<(CreaturePart Part, SdfProgram Program)> _compiledParts;
            private readonly PartBounds[] _partBounds;
            private readonly SdfProgram _bodyProgram;
            private readonly NativeArray<float> _scratchValues;
            private readonly bool _ownsPrograms;

            /// <summary>
            /// Test hook: when false, <see cref="Resolve"/> evaluates every part
            /// unconditionally, bypassing the AABB broad phase. Tests compare this
            /// reference path against the broad-phase path to prove the
            /// optimization is behavior-preserving. Production code never toggles
            /// this; it defaults to true.
            /// </summary>
            internal bool EnableBroadPhase = true;

            internal Resolver(CreatureDefinition definition)
                : this(
                    definition,
                    SdfProgramBuilder.CompileIndividualPartsPortable(definition),
                    SdfProgramBuilder.CompilePortableBodyField(definition),
                    ownsPrograms: true)
            {
            }

            internal Resolver(
                CreatureDefinition definition,
                System.Collections.Generic.List<(CreaturePart Part, SdfProgram Program)> compiledParts,
                SdfProgram bodyProgram,
                bool ownsPrograms = false)
            {
                _definition = definition;
                _compiledParts = compiledParts ?? throw new DomainException("compiledParts must not be null.");
                _ownsPrograms = ownsPrograms;
                _partBounds = new PartBounds[_compiledParts.Count];
                for (int i = 0; i < _compiledParts.Count; i++)
                {
                    _partBounds[i] = PartBounds.FromProgram(_compiledParts[i].Program);
                }
                _bodyProgram = bodyProgram ?? throw new DomainException("bodyProgram must not be null.");
                int scratchLength = _bodyProgram.Operations.Length;
                foreach ((CreaturePart part, SdfProgram program) in _compiledParts)
                {
                    scratchLength = Mathf.Max(scratchLength, program.Operations.Length);
                }
                _scratchValues = new NativeArray<float>(Mathf.Max(scratchLength, 1), Allocator.Persistent);
            }

            public void Dispose()
            {
                if (_ownsPrograms)
                {
                    foreach ((CreaturePart part, SdfProgram program) in _compiledParts)
                    {
                        program.Dispose();
                    }
                    _bodyProgram.Dispose();
                }
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
                float nearestAbsDistanceSq = float.PositiveInfinity;
                var point3 = new float3(position.x, position.y, position.z);

                for (int i = 0; i < _compiledParts.Count; i++)
                {
                    // AABB broad phase: when every op in a part's program is
                    // Cullable (no ellipsoid), the part's SDF is bounded below by
                    // the distance to its world AABB. A part whose AABB distance is
                    // not smaller than the closest part found so far can never win,
                    // so skipping it is bit-identical to evaluating it — it saves
                    // the full per-operation walk over a far part's program.
                    PartBounds bounds = _partBounds[i];
                    if (EnableBroadPhase
                        && bounds.CanBroadPhase
                        && DistanceToAabbSquared(point3, bounds.Min, bounds.Max) >= nearestAbsDistanceSq)
                    {
                        continue;
                    }

                    CreaturePart part = _compiledParts[i].Part;
                    SdfProgram program = _compiledParts[i].Program;
                    // CC-064 non-finite contract: a culled/outside sample reads +inf,
                    // which means "no candidate here" — never a giant valid distance.
                    // Skip it so it cannot win (or poison) the nearest-part decision.
                    float rawDistance = SdfProgramEvaluator.Evaluate(program, point3, _scratchValues);
                    if (float.IsPositiveInfinity(rawDistance)) continue;
                    float distance = Mathf.Abs(rawDistance);
                    if (distance < nearestAbsDistance)
                    {
                        nearestAbsDistance = distance;
                        nearestAbsDistanceSq = distance * distance;
                        nearestPart = part;
                    }
                }

                float bodyAbsDistance = !_bodyProgram.Operations.IsCreated
                    ? float.PositiveInfinity
                    : Mathf.Abs(SdfProgramEvaluator.Evaluate(_bodyProgram, point3, _scratchValues));

                // The Body owns this surface point only when it is a real (finite)
                // candidate — +inf is "outside", not "the Body is nearest". Without
                // this guard, a point where every candidate is culled (+inf everywhere)
                // would incorrectly fall through to the Body's gradient color (CC-064).
                if (_bodyProgram.Operations.IsCreated
                    && !float.IsPositiveInfinity(bodyAbsDistance)
                    && bodyAbsDistance <= nearestAbsDistance)
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

            /// <summary>
            /// Precomputed world AABB and cullability of a part's compiled SDF
            /// program, used by <see cref="Resolve"/> for its broad phase. When
            /// every op in the program is Cullable (no ellipsoid), the part's SDF
            /// is bounded below by the Euclidean distance to its world AABB, so a
            /// part whose AABB distance is not smaller than the closest part found
            /// so far can be skipped without changing the nearest-part decision.
            /// </summary>
            private readonly struct PartBounds
            {
                public readonly float3 Min;
                public readonly float3 Max;
                public readonly bool CanBroadPhase;

                public PartBounds(float3 min, float3 max, bool canBroadPhase)
                {
                    Min = min;
                    Max = max;
                    CanBroadPhase = canBroadPhase;
                }

                public static PartBounds FromProgram(SdfProgram program)
                {
                    if (program == null || !program.Operations.IsCreated)
                    {
                        return new PartBounds(default, default, false);
                    }

                    SdfOperation root = program.Operations[program.RootIndex];
                    bool hasBounds = root.MinBound.x <= root.MaxBound.x
                        && root.MinBound.y <= root.MaxBound.y
                        && root.MinBound.z <= root.MaxBound.z;
                    return new PartBounds(root.MinBound, root.MaxBound, hasBounds && root.Cullable);
                }
            }

            /// <summary>
            /// Squared Euclidean distance from <paramref name="point"/> to the AABB
            /// [min, max]. Zero when the point is inside the box.
            /// </summary>
            private static float DistanceToAabbSquared(float3 point, float3 min, float3 max)
            {
                float3 closest = math.clamp(point, min, max);
                float3 delta = point - closest;
                return math.dot(delta, delta);
            }
        }
    }
}
