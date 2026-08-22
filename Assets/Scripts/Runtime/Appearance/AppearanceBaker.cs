using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Morphology.Extraction;

namespace ProceduralCreature.Appearance
{
    /// <summary>
    /// Bakes per-vertex colors onto an extracted mesh: for each vertex, resolves
    /// which part's appearance parameters apply (PartAppearanceSampler), then
    /// modulates that part's BaseColor by triplanar noise (TriplanarNoise) for
    /// surface variation. A separate stage from mesh extraction (design doc §8),
    /// consuming MeshExtractionResult's plain data rather than a Unity Mesh.
    /// </summary>
    public static class AppearanceBaker
    {
        /// <summary>
        /// Noise output (0..1) is remapped to this brightness range around 1.0 —
        /// e.g. [0.85, 1.15] means noise darkens/brightens the base color by at
        /// most 15%. Kept as a named constant rather than inline magic numbers,
        /// consistent with the project's tolerance-naming convention
        /// (GenerationTolerances.cs); revisit alongside real visual-fidelity
        /// testing rather than treating this value as load-bearing.
        /// </summary>
        private const float BrightnessVariation = 0.15f;

        public static Color[] Bake(CreatureDefinition definition, MeshExtractionResult mesh)
        {
            return Bake(definition, mesh, diagnostics: null);
        }

        public static Color[] Bake(CreatureDefinition definition, MeshExtractionResult mesh, GenerationDiagnostics diagnostics)
        {
            if (definition == null) throw new DomainException("definition must not be null.");
            if (mesh == null) throw new DomainException("mesh must not be null.");

            Color[] colors = null;

            void DoBake()
            {
                if (mesh.Normals.Count != mesh.Positions.Count)
                {
                    mesh.ComputeAngleWeightedNormals();
                }

                colors = new Color[mesh.Positions.Count];
                for (int i = 0; i < mesh.Positions.Count; i++)
                {
                    Vector3 position = mesh.Positions[i];
                    Vector3 normal = mesh.Normals[i];

                    ResolvedAppearance appearance = PartAppearanceSampler.Resolve(definition, position);
                    float noise = TriplanarNoise.Evaluate(position, normal, appearance.NoiseSeed, appearance.NoiseScale);

                    // Remap noise from [0,1] to [1-BrightnessVariation, 1+BrightnessVariation].
                    float brightness = 1f + (noise * 2f - 1f) * BrightnessVariation;

                    Color baseColor = appearance.BaseColor;
                    colors[i] = new Color(
                        Mathf.Clamp01(baseColor.r * brightness),
                        Mathf.Clamp01(baseColor.g * brightness),
                        Mathf.Clamp01(baseColor.b * brightness),
                        baseColor.a);
                }
            }

            if (diagnostics != null)
            {
                diagnostics.TimeStage(GenerationStage.AppearanceBake, DoBake);
            }
            else
            {
                DoBake();
            }

            return colors;
        }
    }
}
