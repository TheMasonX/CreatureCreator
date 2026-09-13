using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Morphology;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Appearance
{
    /// <summary>
    /// Bakes per-vertex colors onto an extracted mesh: for each vertex, resolves
    /// which part's appearance parameters apply (PartAppearanceSampler), then
    /// modulates that part's BaseColor by triplanar noise (TriplanarNoise) for
    /// surface variation.
    /// </summary>
    public static class AppearanceBaker
    {
        private const float BrightnessVariation = 0.15f;
        internal static bool UseBurstResolve = true;

        public static Color[] Bake(CreatureDefinition definition, MeshExtractionResult mesh)
        {
            return Bake(definition, mesh, null);
        }

        public static Color[] Bake(CreatureDefinition definition, MeshExtractionResult mesh, GenerationDiagnostics diagnostics)
        {
            if (definition == null) throw new DomainException("definition must not be null.");
            if (mesh == null) throw new DomainException("mesh must not be null.");

            List<ResolvedPartProgram> compiledParts = null;
            SdfProgram bodyProgram = null;
            try
            {
                compiledParts = SdfProgramBuilder.CompileIndividualPartsPortable(definition);
                bodyProgram = SdfProgramBuilder.CompilePortableBodyField(definition);
                ResolvedBody body = definition.Body == null || definition.Body.Samples == null || definition.Body.Samples.Count == 0
                    ? default : ResolvedBody.Resolve(definition.Body);

                return Bake(definition, mesh, diagnostics, compiledParts, bodyProgram, body);
            }
            finally
            {
                if (compiledParts != null)
                {
                    foreach (ResolvedPartProgram partProgram in compiledParts)
                        partProgram.Program?.Dispose();
                }
                bodyProgram?.Dispose();
            }
        }

        internal static Color[] Bake(CreatureDefinition definition, MeshExtractionResult mesh, GenerationDiagnostics diagnostics,
            List<ResolvedPartProgram> compiledParts, SdfProgram bodyProgram, ResolvedBody body)
        {
            return Bake(definition, mesh, diagnostics, compiledParts, bodyProgram, body, null);
        }

        internal static Color[] Bake(CreatureDefinition definition, MeshExtractionResult mesh, GenerationDiagnostics diagnostics,
            List<ResolvedPartProgram> compiledParts, SdfProgram bodyProgram, ResolvedBody body, ResolvedCreatureSnapshot snapshot)
        {
            if (definition == null) throw new DomainException("definition must not be null.");
            if (mesh == null) throw new DomainException("mesh must not be null.");

            Color[] colors = null;
            void DoBake()
            {
                if (mesh.Normals.Count != mesh.Positions.Count) mesh.ComputeAngleWeightedNormals();
                colors = new Color[mesh.Positions.Count];
                if (UseBurstResolve)
                    BakeBurst(definition, mesh, colors, compiledParts, bodyProgram, body, snapshot);
                else
                {
                    using (PartAppearanceSampler.Resolver resolver = snapshot == null
                        ? PartAppearanceSampler.CreateResolver(definition, compiledParts, bodyProgram)
                        : PartAppearanceSampler.CreateResolver(definition, compiledParts, bodyProgram, snapshot))
                    {
                        for (int i = 0; i < mesh.Positions.Count; i++)
                        {
                            ResolvedAppearance appearance = resolver.Resolve(mesh.Positions[i]);
                            colors[i] = BakeVertexColor(mesh.Positions[i], mesh.Normals[i], appearance.BaseColor, appearance.NoiseSeed, appearance.NoiseScale);
                        }
                    }
                }
            }

            if (diagnostics != null) diagnostics.TimeStage(GenerationStage.AppearanceBake, DoBake);
            else DoBake();
            return colors;
        }

        private static void BakeBurst(CreatureDefinition definition, MeshExtractionResult mesh, Color[] colors,
            List<ResolvedPartProgram> compiledParts, SdfProgram bodyProgram, ResolvedBody body, ResolvedCreatureSnapshot snapshot)
        {
            int maxOps = 1;
            foreach (ResolvedPartProgram partProgram in compiledParts)
                maxOps = Mathf.Max(maxOps, partProgram.Program.Operations.IsCreated ? partProgram.Program.Operations.Length : 1);
            if (bodyProgram != null && bodyProgram.Operations.IsCreated) maxOps = Mathf.Max(maxOps, bodyProgram.Operations.Length);

            int vertexCount = mesh.Positions.Count;
            var vertices = new NativeArray<float3>(vertexCount, Allocator.Persistent);
            var nearestDistances = new NativeArray<float>(vertexCount, Allocator.Persistent);
            var nearestPrograms = new NativeArray<int>(vertexCount, Allocator.Persistent);

            // Body frames are part of the resolved snapshot when the main generation
            // path is used. For compatibility callers without a snapshot still derive
            // them once per bake — never once per vertex.
            Vector3 forward = snapshot == null ? definition.Forward : snapshot.Forward;
            BodyFrame[] bodyFrames = body.SamplePositions != null && body.SamplePositions.Count > 0
                ? snapshot?.BodyFrames ?? BodyFrameResolver.ComputeSampleFrames(body, forward)
                : null;
            try
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i] = new float3(mesh.Positions[i].x, mesh.Positions[i].y, mesh.Positions[i].z);
                    nearestDistances[i] = float.PositiveInfinity;
                    nearestPrograms[i] = -1;
                }

                AppearanceResolveBurst.ResolveAll(
                    compiledParts, bodyProgram, vertices, maxOps,
                    nearestDistances, nearestPrograms);

                Color defaultColor = AppearanceDefinition.Default.BaseColor;
                for (int i = 0; i < vertexCount; i++)
                {
                    int winner = nearestPrograms[i];
                    if (winner == compiledParts.Count)
                    {
                        Color bodyColor = BodyVerticalGradientSampler.EvaluateColor(
                            snapshot == null ? definition.Body?.Appearance : snapshot.BodyAppearance,
                            body, forward, bodyFrames, mesh.Positions[i]);
                        colors[i] = BakeVertexColor(mesh.Positions[i], mesh.Normals[i], bodyColor, 0, 1f);
                    }
                    else
                    {
                        Color baseColor;
                        int seed;
                        float scale;
                        if (winner >= 0)
                        {
                            AppearanceDefinition appearance = compiledParts[winner].Part.Appearance;
                            baseColor = appearance.BaseColor;
                            seed = appearance.NoiseSeed;
                            scale = appearance.NoiseScale;
                        }
                        else
                        {
                            baseColor = defaultColor;
                            seed = 0;
                            scale = 1f;
                        }
                        colors[i] = BakeVertexColor(mesh.Positions[i], mesh.Normals[i], baseColor, seed, scale);
                    }
                }
            }
            finally
            {
                vertices.Dispose(); nearestDistances.Dispose(); nearestPrograms.Dispose();
            }
        }

        public static Color[] BakePart(CreaturePart part, IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> normals)
        {
            if (part == null) throw new DomainException("part must not be null.");
            return BakePart(part.Appearance, positions, normals);
        }

        public static Color[] BakePart(AppearanceDefinition appearance, IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> normals)
        {
            if (positions == null) throw new DomainException("positions must not be null.");
            if (normals == null) throw new DomainException("normals must not be null.");
            if (positions.Count != normals.Count) throw new DomainException("positions and normals must have the same length.");
            var colors = new Color[positions.Count];
            for (int i = 0; i < positions.Count; i++) colors[i] = BakeVertexColor(positions[i], normals[i], appearance.BaseColor, appearance.NoiseSeed, appearance.NoiseScale);
            return colors;
        }

        private static Color BakeVertexColor(Vector3 position, Vector3 normal, Color baseColor, int noiseSeed, float noiseScale)
        {
            float noise = TriplanarNoise.Evaluate(position, normal, noiseSeed, noiseScale);
            float brightness = 1f + (noise * 2f - 1f) * BrightnessVariation;
            return new Color(Mathf.Clamp01(baseColor.r * brightness), Mathf.Clamp01(baseColor.g * brightness),
                Mathf.Clamp01(baseColor.b * brightness), baseColor.a);
        }
    }
}
