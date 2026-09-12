using ProceduralCreature.Common;
using UnityEngine;

namespace ProceduralCreature.Animation.Binding
{
    /// <summary>
    /// Build-time tuning for implicit-surface weight authoring. These values decide
    /// how far a bone's influence reaches; they never change DNA and never touch the
    /// per-frame animation path.
    ///
    /// The values are exposed through the shared <see cref="ProceduralCreature.Generation.CreatureGenerationConfig"/>
    /// so the editor can tune deformation without a recompile.
    /// </summary>
    public readonly struct InfluenceWeightingPolicy
    {
        /// <summary>Lowest accepted falloff tube multiplier; below 1 a bone barely blends.</summary>
        public const float MinRadiusScale = 1f;

        /// <summary>Highest accepted falloff tube multiplier.</summary>
        public const float MaxRadiusScale = 8f;

        /// <summary>Lowest accepted falloff exponent; 1 is linear, higher is tighter.</summary>
        public const float MinFalloffPower = 0.5f;

        /// <summary>Highest accepted falloff exponent.</summary>
        public const float MaxFalloffPower = 6f;

        /// <summary>Lowest accepted default radius for bones with no resolved morphology radius.</summary>
        public const float MinDefaultBoneRadius = 0.01f;

        /// <summary>Highest accepted default radius for bones with no resolved morphology radius.</summary>
        public const float MaxDefaultBoneRadius = 4f;

        /// <summary>Highest accepted longitudinal blend band, in bone radii.</summary>
        public const float MaxLongitudinalBlendMarginRadii = 6f;

        /// <summary>Default falloff tube radius multiplier (TSK-0131 value).</summary>
        public const float DefaultRadiusScale = 3f;

        /// <summary>Default falloff exponent (TSK-0131 value).</summary>
        public const float DefaultFalloffPower = 2f;

        /// <summary>Default radius for bones with no resolved morphology radius (TSK-0131 value).</summary>
        public const float DefaultBoneRadius = 0.5f;

        /// <summary>Default chain-aware blend band past each endpoint, in bone radii.</summary>
        public const float DefaultLongitudinalBlendMarginRadii = 1f;

        /// <summary>Falloff tube radius as a multiple of the bone's morphology radius.</summary>
        public float RadiusScale { get; }

        /// <summary>Exponent applied to the normalized falloff; higher values tighten the blend.</summary>
        public float FalloffPower { get; }

        /// <summary>Radius used when a bone has no finite positive resolved morphology radius.</summary>
        public float FallbackBoneRadius { get; }

        /// <summary>
        /// When true, a bone cannot influence geometry that lies behind its own start
        /// or far beyond its own end along its own axis. This is what keeps a forearm
        /// bone from dragging the upper arm.
        /// </summary>
        public bool ChainAwareLocality { get; }

        /// <summary>Blend band past each bone endpoint, in bone radii, when chain-aware locality is on.</summary>
        public float LongitudinalBlendMarginRadii { get; }

        public InfluenceWeightingPolicy(
            float radiusScale,
            float falloffPower,
            float defaultBoneRadius,
            bool chainAwareLocality,
            float longitudinalBlendMarginRadii)
        {
            if (!NumericValidity.IsFinite(radiusScale) || radiusScale < MinRadiusScale || radiusScale > MaxRadiusScale)
                throw new DomainException(
                    $"Influence radius scale must be finite and within [{MinRadiusScale}, {MaxRadiusScale}].");
            if (!NumericValidity.IsFinite(falloffPower) || falloffPower < MinFalloffPower || falloffPower > MaxFalloffPower)
                throw new DomainException(
                    $"Influence falloff power must be finite and within [{MinFalloffPower}, {MaxFalloffPower}].");
            if (!NumericValidity.IsFinite(defaultBoneRadius) || defaultBoneRadius < MinDefaultBoneRadius || defaultBoneRadius > MaxDefaultBoneRadius)
                throw new DomainException(
                    $"Default influence radius must be finite and within [{MinDefaultBoneRadius}, {MaxDefaultBoneRadius}].");
            if (!NumericValidity.IsFinite(longitudinalBlendMarginRadii) ||
                longitudinalBlendMarginRadii < 0f || longitudinalBlendMarginRadii > MaxLongitudinalBlendMarginRadii)
                throw new DomainException(
                    $"Longitudinal blend margin must be finite and within [0, {MaxLongitudinalBlendMarginRadii}] radii.");

            RadiusScale = radiusScale;
            FalloffPower = falloffPower;
            FallbackBoneRadius = defaultBoneRadius;
            ChainAwareLocality = chainAwareLocality;
            LongitudinalBlendMarginRadii = longitudinalBlendMarginRadii;
        }

        /// <summary>
        /// Product default: chain-aware locality on with a one-radius blend band past
        /// each endpoint, so a bone affects its own span and the adjoining joint only.
        /// </summary>
        public static InfluenceWeightingPolicy Default =>
            new InfluenceWeightingPolicy(
                DefaultRadiusScale,
                DefaultFalloffPower,
                DefaultBoneRadius,
                chainAwareLocality: true,
                longitudinalBlendMarginRadii: DefaultLongitudinalBlendMarginRadii);

        /// <summary>The pre-locality model: pure radial falloff with no longitudinal gate.</summary>
        public static InfluenceWeightingPolicy Legacy =>
            new InfluenceWeightingPolicy(DefaultRadiusScale, DefaultFalloffPower, DefaultBoneRadius,
                chainAwareLocality: false, longitudinalBlendMarginRadii: 0f);

        /// <summary>
        /// This policy with chain-aware locality disabled, used as the totality
        /// fallback when the gate would otherwise leave a vertex with no influence.
        /// </summary>
        public InfluenceWeightingPolicy WithoutChainAwareLocality()
            => ChainAwareLocality
                ? new InfluenceWeightingPolicy(RadiusScale, FalloffPower, FallbackBoneRadius, chainAwareLocality: false, longitudinalBlendMarginRadii: 0f)
                : this;
    }
}
