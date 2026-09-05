using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Appearance
{
    /// <summary>
    /// Burst-computed nearest-part appearance resolution (Slice D). For each
    /// vertex this evaluates every compiled part program and the Body program
    /// with the same Fast (CC-063) culling the managed resolver uses, then picks
    /// the nearest finite surface and writes the resolved base color + noise
    /// parameters plus a BodyWins flag.
    ///
    /// The Body vertical-gradient color and triplanar noise stay managed: the
    /// gradient uses Unity's AnimationCurve/Gradient, which must render authored
    /// data exactly, and the noise is only ~1% of the bake. This job removes the
    /// per-vertex managed SDF walks, which are ~97% of the bake time (measured
    /// on the dino at 96^3: resolve 104.6 ms of 107.5 ms bake).
    ///
    /// Output is bit-identical to <see cref="PartAppearanceSampler.Resolver"/> for
    /// the same inputs: same evaluator, same +inf "absent" contract (CC-064),
    /// same strict-less part ordering and non-strict Body comparison.
    /// </summary>
    public static class AppearanceResolveBurst
    {
        private const int ScratchValueBudget = 8 * 1024 * 1024;

        /// <summary>
        /// Resolves appearance for every vertex. <paramref name="distances"/>
        /// (vertexCount * programCount) is filled with +inf first, then each
        /// program job writes its own column (parts 0..PartCount-1, Body column
        /// PartCount), then the merge job resolves each vertex.
        /// </summary>
        public static void ResolveAll(
            List<ResolvedPartProgram> compiledParts,
            SdfProgram bodyProgram,
            NativeArray<float3> vertices,
            int programCount,
            int maxOps,
            NativeArray<float> distances,
            NativeArray<float4> outBaseColor,
            NativeArray<int> outNoiseSeed,
            NativeArray<float> outNoiseScale,
            NativeArray<bool> outBodyWins)
        {
            int vertexCount = vertices.Length;

            var partColors = new NativeArray<float4>(compiledParts.Count, Allocator.Persistent);
            var partSeeds = new NativeArray<int>(compiledParts.Count, Allocator.Persistent);
            var partScales = new NativeArray<float>(compiledParts.Count, Allocator.Persistent);
            try
            {
                for (int p = 0; p < compiledParts.Count; p++)
                {
                    AppearanceDefinition app = compiledParts[p].Part.Appearance;
                    partColors[p] = new float4(app.BaseColor.r, app.BaseColor.g, app.BaseColor.b, app.BaseColor.a);
                    partSeeds[p] = app.NoiseSeed;
                    partScales[p] = app.NoiseScale;
                }

                // CC-064: absent candidates read +inf; pre-fill so skipped or
                // empty programs never look like a real (finite) distance.
                for (int i = 0; i < distances.Length; i++)
                {
                    distances[i] = float.PositiveInfinity;
                }

                int batchSize = Mathf.Max(1, ScratchValueBudget / Mathf.Max(maxOps, 1));
                var scratch = new NativeArray<float>(batchSize * maxOps, Allocator.Persistent);
                try
                {
                    for (int p = 0; p < compiledParts.Count; p++)
                    {
                        SdfProgram program = compiledParts[p].Program;
                        if (program == null || !program.Operations.IsCreated) continue;
                        RunDistanceBatches(program, vertices, distances, programCount, p, maxOps, batchSize, scratch);
                    }

                    if (bodyProgram != null && bodyProgram.Operations.IsCreated)
                    {
                        RunDistanceBatches(
                            bodyProgram, vertices, distances, programCount,
                            compiledParts.Count, maxOps, batchSize, scratch);
                    }

                    Color defaultColor = AppearanceDefinition.Default.BaseColor;
                    var merge = new AppearanceMergeJob
                    {
                        Distances = distances,
                        ProgramCount = programCount,
                        PartCount = compiledParts.Count,
                        PartBaseColors = partColors,
                        PartNoiseSeeds = partSeeds,
                        PartNoiseScales = partScales,
                        DefaultBaseColor = new float4(defaultColor.r, defaultColor.g, defaultColor.b, defaultColor.a),
                        OutBaseColor = outBaseColor,
                        OutNoiseSeed = outNoiseSeed,
                        OutNoiseScale = outNoiseScale,
                        OutBodyWins = outBodyWins,
                    };
                    merge.Schedule(vertexCount, 64).Complete();
                }
                finally
                {
                    scratch.Dispose();
                }
            }
            finally
            {
                partColors.Dispose();
                partSeeds.Dispose();
                partScales.Dispose();
            }
        }

        private static void RunDistanceBatches(
            SdfProgram program, NativeArray<float3> vertices,
            NativeArray<float> distances, int distanceStride, int programIndex,
            int maxOps, int batchSize, NativeArray<float> scratch)
        {
            int vertexCount = vertices.Length;
            for (int start = 0; start < vertexCount; start += batchSize)
            {
                int count = Mathf.Min(batchSize, vertexCount - start);
                var job = new PartSdfDistanceJob
                {
                    Operations = program.Operations,
                    RootIndex = program.RootIndex,
                    InfluenceRadius = program.InfluenceRadius,
                    Vertices = vertices,
                    Distances = distances,
                    DistanceStride = distanceStride,
                    ProgramIndex = programIndex,
                    VertexStart = start,
                    ScratchValues = scratch,
                    ScratchStride = maxOps,
                };
                job.Schedule(count, 64).Complete();
            }
        }
    }

    /// <summary>
    /// Evaluates one SDF program at every vertex (with Fast culling) and writes
    /// Abs(distance) into the program's column, preserving +inf for absent
    /// (culled) candidates. Mirrors the SdfSamplingJob batching/scratch layout.
    /// </summary>
    [BurstCompile]
    public struct PartSdfDistanceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SdfOperation>.ReadOnly Operations;
        [ReadOnly] public NativeArray<float3> Vertices;
        [NativeDisableParallelForRestriction] public NativeArray<float> Distances;
        [NativeDisableParallelForRestriction] public NativeArray<float> ScratchValues;
        public int RootIndex;
        public int ProgramIndex;
        public int DistanceStride;
        public int VertexStart;
        public int ScratchStride;
        public float InfluenceRadius;

        public void Execute(int index)
        {
            int vertexIndex = VertexStart + index;
            float3 point = Vertices[vertexIndex];
            int valueOffset = index * ScratchStride;
            float raw = SdfProgramEvaluator.EvaluateInto(
                Operations, RootIndex, point, ScratchValues, valueOffset, InfluenceRadius, allowCulling: true);
            Distances[vertexIndex * DistanceStride + ProgramIndex] = math.abs(raw);
        }
    }

    /// <summary>
    /// Picks the nearest finite surface per vertex from the precomputed distance
    /// matrix (part columns 0..PartCount-1, Body column PartCount), mirroring
    /// PartAppearanceSampler.Resolver.Resolve: strict &lt; for parts, non-strict
    /// &lt;= for the Body, +inf = absent (CC-064). Writes the resolved appearance.
    /// </summary>
    [BurstCompile]
    public struct AppearanceMergeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Distances;
        [ReadOnly] public NativeArray<float4> PartBaseColors;
        [ReadOnly] public NativeArray<int> PartNoiseSeeds;
        [ReadOnly] public NativeArray<float> PartNoiseScales;
        public int ProgramCount;
        public int PartCount;
        public float4 DefaultBaseColor;
        [WriteOnly] public NativeArray<float4> OutBaseColor;
        [WriteOnly] public NativeArray<int> OutNoiseSeed;
        [WriteOnly] public NativeArray<float> OutNoiseScale;
        [WriteOnly] public NativeArray<bool> OutBodyWins;

        public void Execute(int vertexIndex)
        {
            int row = vertexIndex * ProgramCount;
            float nearest = float.PositiveInfinity;
            int nearestPart = -1;
            for (int p = 0; p < PartCount; p++)
            {
                float d = Distances[row + p];
                if (float.IsPositiveInfinity(d)) continue;
                if (d < nearest)
                {
                    nearest = d;
                    nearestPart = p;
                }
            }

            float bodyD = Distances[row + PartCount];
            if (!float.IsPositiveInfinity(bodyD) && bodyD <= nearest)
            {
                // Body wins: the managed tail applies the vertical gradient.
                // Placeholder white base; noise seed 0, scale 1 (as the Resolver).
                OutBaseColor[vertexIndex] = new float4(1f, 1f, 1f, 1f);
                OutNoiseSeed[vertexIndex] = 0;
                OutNoiseScale[vertexIndex] = 1f;
                OutBodyWins[vertexIndex] = true;
                return;
            }

            OutBodyWins[vertexIndex] = false;
            if (nearestPart >= 0)
            {
                OutBaseColor[vertexIndex] = PartBaseColors[nearestPart];
                OutNoiseSeed[vertexIndex] = PartNoiseSeeds[nearestPart];
                OutNoiseScale[vertexIndex] = PartNoiseScales[nearestPart];
            }
            else
            {
                OutBaseColor[vertexIndex] = DefaultBaseColor;
                OutNoiseSeed[vertexIndex] = 0;
                OutNoiseScale[vertexIndex] = 1f;
            }
        }
    }
}
