using System.Collections.Generic;
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
                    ResolvedAppearance appearance = PartAppearanceSampler.Resolve(definition, mesh.Positions[i]);
                    colors[i] = BakeVertexColor(mesh.Positions[i], mesh.Normals[i], appearance.BaseColor, appearance.NoiseSeed, appearance.NoiseScale);
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

        /// <summary>
        /// Bakes a single part's OWN authored appearance (BaseColor + triplanar
        /// noise) onto an arbitrary set of vertices. Used for mesh-asset geometry
        /// items (CC-031): a mesh-asset part is not part of the implicit SDF field,
        /// so nearest-surface appearance resolution cannot reach it. Its appearance
        /// resolves directly from the part instead, while keeping the exact same
        /// noise modulation the implicit bake applies. This deliberately does NOT
        /// run the Body vertical-gradient or nearest-part samplers — a mesh-asset
        /// part's color is its own, never the Body's implicit color.
        /// </summary>
        public static Color[] BakePart(CreaturePart part, IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> normals)
        {
            if (part == null) throw new DomainException("part must not be null.");
            if (positions == null) throw new DomainException("positions must not be null.");
            if (normals == null) throw new DomainException("normals must not be null.");
            if (positions.Count != normals.Count)
            {
                throw new DomainException("positions and normals must have the same length.");
            }

            AppearanceDefinition appearance = part.Appearance;
            var colors = new Color[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                colors[i] = BakeVertexColor(positions[i], normals[i], appearance.BaseColor, appearance.NoiseSeed, appearance.NoiseScale);
            }
            return colors;
        }

        private static Color BakeVertexColor(Vector3 position, Vector3 normal, Color baseColor, int noiseSeed, float noiseScale)
        {
            float noise = TriplanarNoise.Evaluate(position, normal, noiseSeed, noiseScale);

            // Remap noise from [0,1] to [1-BrightnessVariation, 1+BrightnessVariation].
            float brightness = 1f + (noise * 2f - 1f) * BrightnessVariation;

            return new Color(
                Mathf.Clamp01(baseColor.r * brightness),
                Mathf.Clamp01(baseColor.g * brightness),
                Mathf.Clamp01(baseColor.b * brightness),
                baseColor.a);
        }
    }
}
