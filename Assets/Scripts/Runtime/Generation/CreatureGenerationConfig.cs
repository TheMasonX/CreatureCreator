using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Appearance;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    /// <summary>
    /// Shared runtime/editor generation defaults and palette references. Transient
    /// requests such as preview quality may override these values per generation.
    /// </summary>
    [CreateAssetMenu(menuName = "Procedural Creature/Generation Config", fileName = "CreatureGenerationConfig")]
    public sealed class CreatureGenerationConfig : ScriptableObject
    {
        [SerializeField] private float defaultVoxelsPerUnit = 16f;
        [SerializeField] private CreatureMeshPalette meshPalette;
        [SerializeField] private CreatureMaterialPalette materialPalette;

        [Header("Skinning Weights")]
        [Tooltip("Falloff tube radius as a multiple of each bone's morphology radius. Higher values spread influence further.")]
        [SerializeField, Range(InfluenceWeightingPolicy.MinRadiusScale, InfluenceWeightingPolicy.MaxRadiusScale)]
        private float influenceRadiusScale = InfluenceWeightingPolicy.DefaultRadiusScale;

        [Tooltip("Exponent applied to the normalized falloff. Higher values tighten the blend toward the bone.")]
        [SerializeField, Range(InfluenceWeightingPolicy.MinFalloffPower, InfluenceWeightingPolicy.MaxFalloffPower)]
        private float influenceFalloffPower = InfluenceWeightingPolicy.DefaultFalloffPower;

        [Tooltip("Radius used for bones that have no resolved morphology radius.")]
        [SerializeField, Range(InfluenceWeightingPolicy.MinDefaultBoneRadius, InfluenceWeightingPolicy.MaxDefaultBoneRadius)]
        private float defaultBoneInfluenceRadius = InfluenceWeightingPolicy.DefaultBoneRadius;

        [Tooltip("When on, a bone only influences its own span plus a blend band past each endpoint, so a forearm cannot drag the upper arm.")]
        [SerializeField] private bool chainAwareWeightLocality = true;

        [Tooltip("Chain-aware blend band past each bone endpoint, in bone radii. 1 keeps the joint blend tight; higher values soften it.")]
        [SerializeField, Range(0f, InfluenceWeightingPolicy.MaxLongitudinalBlendMarginRadii)]
        private float longitudinalBlendMarginRadii = InfluenceWeightingPolicy.DefaultLongitudinalBlendMarginRadii;

        public float DefaultVoxelsPerUnit => Mathf.Max(1f, defaultVoxelsPerUnit);
        public CreatureMeshPalette MeshPalette => meshPalette;
        public CreatureMaterialPalette MaterialPalette => materialPalette;

        /// <summary>
        /// The authored weighting policy, clamped to the value ranges the builder
        /// accepts so an out-of-range inspector edit cannot abort a generation.
        /// </summary>
        public InfluenceWeightingPolicy WeightingPolicy => new InfluenceWeightingPolicy(
            Mathf.Clamp(influenceRadiusScale, InfluenceWeightingPolicy.MinRadiusScale, InfluenceWeightingPolicy.MaxRadiusScale),
            Mathf.Clamp(influenceFalloffPower, InfluenceWeightingPolicy.MinFalloffPower, InfluenceWeightingPolicy.MaxFalloffPower),
            Mathf.Clamp(defaultBoneInfluenceRadius, InfluenceWeightingPolicy.MinDefaultBoneRadius, InfluenceWeightingPolicy.MaxDefaultBoneRadius),
            chainAwareWeightLocality,
            Mathf.Clamp(longitudinalBlendMarginRadii, 0f, InfluenceWeightingPolicy.MaxLongitudinalBlendMarginRadii));
    }
}
