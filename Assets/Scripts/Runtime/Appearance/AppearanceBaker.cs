using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

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

        /// <summary>
        /// Slice D: route the per-vertex appearance resolution through the Burst
        /// jobs in <see cref="AppearanceResolveBurst"/> (bit-identical to the
        /// managed <see cref="PartAppearanceSampler.Resolver"/>; the Body
        /// vertical gradient and triplanar noise still run managed). Internal so
        /// parity tests can force the managed path and compare exactly.
        /// </summary>
        internal static bool UseBurstResolve = true;

        public static Color[] Bake(CreatureDefinition definition, MeshExtractionResult mesh)
        {
            return Bake(definition, mesh, null);
        }

        public static Color[] Bake(
            CreatureDefinition definition, MeshExtractionResult mesh,
            GenerationDiagnostics diagnostics)
        {
            var compiledParts = SdfProgramBuilder.CompileIndividualPartsPortable(definition);
            SdfProgram bodyProgram = SdfProgramBuilder.CompilePortableBodyField(definition);
            try
            {
                return Bake(definition, mesh, diagnostics, compiledParts, bodyProgram);
            }
            finally
            {
                foreach ((CreaturePart part, SdfProgram program) in compiledParts) program.Dispose();
                bodyProgram.Dispose();
            }
        }

        internal static Color[] Bake(
            CreatureDefinition definition, MeshExtractionResult mesh,
            GenerationDiagnostics diagnostics,
            System.Collections.Generic.List<(CreaturePart Part, SdfProgram Program)> compiledParts,
            SdfProgram bodyProgram)
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
                if (UseBurstResolve)
                {
                    BakeBurst(definition, mesh, colors, compiledParts, bodyProgram);
                }
                else
                {
                    using (PartAppearanceSampler.Resolver resolver = PartAppearanceSampler.CreateResolver(
                        definition, compiledParts, bodyProgram))
                    {
                        for (int i = 0; i < mesh.Positions.Count; i++)
                        {
                            ResolvedAppearance appearance = resolver.Resolve(mesh.Positions[i]);
                            colors[i] = BakeVertexColor(mesh.Positions[i], mesh.Normals[i], appearance.BaseColor, appearance.NoiseSeed, appearance.NoiseScale);
                        }
                    }
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
        /// Slice D Burst path: evaluates the nearest-part/Body SDF decision for
        /// every vertex in Burst, then applies the Body vertical gradient (only
        /// for Body-owned vertices) and triplanar noise in managed code. The
        /// final colors are bit-identical to the managed resolver path.
        /// </summary>
        private static void BakeBurst(
            CreatureDefinition definition, MeshExtractionResult mesh, Color[] colors,
            System.Collections.Generic.List<(CreaturePart Part, SdfProgram Program)> compiledParts,
            SdfProgram bodyProgram)
        {
                int programCount = compiledParts.Count + 1;
                int maxOps = 1;
                foreach ((CreaturePart part, SdfProgram program) in compiledParts)
                {
                    maxOps = Mathf.Max(maxOps, program.Operations.IsCreated ? program.Operations.Length : 1);
                }
                if (bodyProgram != null && bodyProgram.Operations.IsCreated)
                {
                    maxOps = Mathf.Max(maxOps, bodyProgram.Operations.Length);
                }

                int vertexCount = mesh.Positions.Count;
                long distanceCount = (long)vertexCount * programCount;
                if (distanceCount > int.MaxValue)
                {
                    throw new DomainException("Appearance bake distance matrix exceeds addressable array size.");
                }
                var vertices = new NativeArray<float3>(vertexCount, Allocator.Persistent);
                var distances = new NativeArray<float>((int)distanceCount, Allocator.Persistent);
                var outBase = new NativeArray<float4>(vertexCount, Allocator.Persistent);
                var outSeed = new NativeArray<int>(vertexCount, Allocator.Persistent);
                var outScale = new NativeArray<float>(vertexCount, Allocator.Persistent);
                var outBody = new NativeArray<bool>(vertexCount, Allocator.Persistent);
                try
                {
                    for (int i = 0; i < vertexCount; i++)
                    {
                        vertices[i] = new float3(mesh.Positions[i].x, mesh.Positions[i].y, mesh.Positions[i].z);
                    }

                    AppearanceResolveBurst.ResolveAll(
                        compiledParts, bodyProgram, vertices, programCount, maxOps,
                        distances, outBase, outSeed, outScale, outBody);

                    for (int i = 0; i < vertexCount; i++)
                    {
                        if (outBody[i])
                        {
                            Color bodyColor = BodyVerticalGradientSampler.EvaluateColor(definition, mesh.Positions[i]);
                            colors[i] = BakeVertexColor(mesh.Positions[i], mesh.Normals[i], bodyColor, 0, 1f);
                        }
                        else
                        {
                            var baseColor = new Color(outBase[i].x, outBase[i].y, outBase[i].z, outBase[i].w);
                            colors[i] = BakeVertexColor(mesh.Positions[i], mesh.Normals[i], baseColor, outSeed[i], outScale[i]);
                        }
                    }
                }
                finally
                {
                    vertices.Dispose();
                    distances.Dispose();
                    outBase.Dispose();
                    outSeed.Dispose();
                    outScale.Dispose();
                    outBody.Dispose();
                }
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
