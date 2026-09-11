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
    /// surface variation. A separate stage from mesh extraction (design doc §8),
    /// consuming MeshExtractionResult's plain data rather than a Unity Mesh.
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

            var compiledParts = SdfProgramBuilder.CompileIndividualPartsPortable(definition);
            SdfProgram bodyProgram = SdfProgramBuilder.CompilePortableBodyField(definition);
            ResolvedBody body = definition.Body == null || definition.Body.Samples == null || definition.Body.Samples.Count == 0
                ? default : ResolvedBody.Resolve(definition.Body);
            try
            {
                return Bake(definition, mesh, diagnostics, compiledParts, bodyProgram, body);
            }
            finally
            {
                foreach (ResolvedPartProgram partProgram in compiledParts) partProgram.Program.Dispose();
                bodyProgram.Dispose();
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
            int programCount = compiledParts.Count + 1;
            int maxOps = 1;
            foreach (ResolvedPartProgram partProgram in compiledParts)
                maxOps = Mathf.Max(maxOps, partProgram.Program.Operations.IsCreated ? partProgram.Program.Operations.Length : 1);
            if (bodyProgram != null && bodyProgram.Operations.IsCreated) maxOps = Mathf.Max(maxOps, bodyProgram.Operations.Length);

            int vertexCount = mesh.Positions.Count;
            var vertices = new NativeArray<float3>(vertexCount, Allocator.Persistent);
            var nearestDistances = new NativeArray<float>(vertexCount, Allocator.Persistent);
            var nearestPrograms = new NativeArray<int>(vertexCount, Allocator.Persistent);
            var outBase = new NativeArray<float4>(vertexCount, Allocator.Persistent);
            var outSeed = new NativeArray<int>(vertexCount, Allocator.Persistent);
            var outScale = new NativeArray<float>(vertexCount, Allocator.Persistent);
            var outBody = new NativeArray<bool>(vertexCount, Allocator.Persistent);
            var partColors = new NativeArray<float4>(compiledParts.Count, Allocator.Persistent);
            var partSeeds = new NativeArray<int>(compiledParts.Count, Allocator.Persistent);
            var partScales = new NativeArray<float>(compiledParts.Count, Allocator.Persistent);
            try
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i] = new float3(mesh.Positions[i].x, mesh.Positions[i].y, mesh.Positions[i].z);
                    nearestDistances[i] = float.PositiveInfinity;
                    nearestPrograms[i] = -1;
                }

                for (int p = 0; p < compiledParts.Count; p++)
                {
                    AppearanceDefinition app = compiledParts[p].Part.Appearance;
                    partColors[p] = new float4(app.BaseColor.r, app.BaseColor.g, app.BaseColor.b, app.BaseColor.a);
                    partSeeds[p] = app.NoiseSeed;
                    partScales[p] = app.NoiseScale;
                }

                int batchSize = Mathf.Max(1, ScratchValueBudget / Mathf.Max(maxOps, 1));
                var scratch = new NativeArray<float>(batchSize * maxOps, Allocator.Persistent);
                try
                {
                    for (int p = 0; p < compiledParts.Count; p++)
                    {
                        SdfProgram program = compiledParts[p].Program;
                        if (program == null || !program.Operations.IsCreated) continue;
                        RunNearestBatches(
                            program, vertices, nearestDistances, nearestPrograms,
                            p, isBody: false, maxOps, batchSize, scratch);
                    }

                    if (bodyProgram != null && bodyProgram.Operations.IsCreated)
                    {
                        RunNearestBatches(
                            bodyProgram, vertices, nearestDistances, nearestPrograms,
                            compiledParts.Count, isBody: true, maxOps, batchSize, scratch);
                    }
                }
                finally
                {
                    scratch.Dispose();
                }

                Color defaultColor = AppearanceDefinition.Default.BaseColor;
                for (int i = 0; i < vertexCount; i++)
                {
                    int winner = nearestPrograms[i];
                    if (winner == compiledParts.Count)
                    {
                        // Body wins: the managed tail applies the vertical gradient.
                        Color bodyColor = BodyVerticalGradientSampler.EvaluateColor(
                            snapshot == null ? definition.Body?.Appearance : snapshot.BodyAppearance,
                            body, snapshot == null ? definition.Forward : snapshot.Forward, mesh.Positions[i]);
                        outBase[i] = new float4(bodyColor.r, bodyColor.g, bodyColor.b, bodyColor.a);
                        outSeed[i] = 0;
                        outScale[i] = 1f;
                        outBody[i] = true;
                    }
                    else
                    {
                        outBody[i] = false;
                        if (winner >= 0)
                        {
                            outBase[i] = partColors[winner];
                            outSeed[i] = partSeeds[winner];
                            outScale[i] = partScales[winner];
                        }
                        else
                        {
                            outBase[i] = new float4(defaultColor.r, defaultColor.g, defaultColor.b, defaultColor.a);
                            outSeed[i] = 0;
                            outScale[i] = 1f;
                        }
                    }
                    colors[i] = BakeVertexColor(mesh.Positions[i], mesh.Normals[i],
                        new Color(outBase[i].x, outBase[i].y, outBase[i].z, outBase[i].w),
                        outSeed[i], outScale[i]);
                }
            }
            finally
            {
                vertices.Dispose(); nearestDistances.Dispose(); nearestPrograms.Dispose();
                outBase.Dispose(); outSeed.Dispose(); outScale.Dispose(); outBody.Dispose();
                partColors.Dispose(); partSeeds.Dispose(); partScales.Dispose();
            }
        }

        private const int ScratchValueBudget = 8 * 1024 * 1024;

        private static void RunNearestBatches(
            SdfProgram program, NativeArray<float3> vertices,
            NativeArray<float> nearestDistances, NativeArray<int> nearestPrograms,
            int programIndex, bool isBody, int maxOps, int batchSize, NativeArray<float> scratch)
        {
            int vertexCount = vertices.Length;
            for (int start = 0; start < vertexCount; start += batchSize)
            {
                int count = Mathf.Min(batchSize, vertexCount - start);
                var job = new NearestAppearanceCandidateJob
                {
                    Operations = program.Operations,
                    RootIndex = program.RootIndex,
                    InfluenceRadius = program.InfluenceRadius,
                    Vertices = vertices,
                    NearestDistances = nearestDistances,
                    NearestPrograms = nearestPrograms,
                    ProgramIndex = programIndex,
                    IsBody = isBody,
                    VertexStart = start,
                    ScratchValues = scratch,
                    ScratchStride = maxOps,
                };
                job.Schedule(count, 64).Complete();
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

    [BurstCompile]
    public struct NearestAppearanceCandidateJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SdfOperation>.ReadOnly Operations;
        [ReadOnly] public NativeArray<float3> Vertices;
        [NativeDisableParallelForRestriction] public NativeArray<float> NearestDistances;
        [NativeDisableParallelForRestriction] public NativeArray<int> NearestPrograms;
        [NativeDisableParallelForRestriction] public NativeArray<float> ScratchValues;
        public int RootIndex;
        public int ProgramIndex;
        public int VertexStart;
        public int ScratchStride;
        public float InfluenceRadius;
        public bool IsBody;

        public void Execute(int index)
        {
            int vertexIndex = VertexStart + index;
            float3 point = Vertices[vertexIndex];
            float candidate = SdfProgramEvaluator.EvaluateInto(
                Operations, RootIndex, point, ScratchValues, index * ScratchStride,
                InfluenceRadius, allowCulling: true);
            if (float.IsPositiveInfinity(candidate)) return;

            float current = NearestDistances[vertexIndex];
            // Parts use strict-less ordering, preserving authored compiled-part order.
            // The Body uses non-strict comparison and therefore wins an exact tie.
            bool wins = IsBody ? candidate <= current : candidate < current;
            if (wins)
            {
                NearestDistances[vertexIndex] = candidate;
                NearestPrograms[vertexIndex] = ProgramIndex;
            }
        }
    }
}
