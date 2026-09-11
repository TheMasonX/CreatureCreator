using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Appearance
{
    /// <summary>
    /// Burst-computed nearest-part appearance resolution. Each compiled program is
    /// streamed through a nearest-candidate buffer instead of materializing a
    /// vertex-count × program-count distance matrix. Program order remains stable,
    /// parts use strict-less comparison, and Body uses non-strict comparison so an
    /// exact Body/part tie still resolves to Body.
    /// </summary>
    public static class AppearanceResolveBurst
    {
        private const int ScratchValueBudget = 8 * 1024 * 1024;

        public static void ResolveAll(
            List<ResolvedPartProgram> compiledParts,
            SdfProgram bodyProgram,
            NativeArray<float3> vertices,
            int maxOps,
            NativeArray<float> nearestDistances,
            NativeArray<int> nearestPrograms)
        {
            int batchSize = Mathf.Max(1, ScratchValueBudget / Mathf.Max(maxOps, 1));
            var scratch = new NativeArray<float>(batchSize * maxOps, Allocator.Persistent);
            try
            {
                for (int p = 0; p < compiledParts.Count; p++)
                {
                    SdfProgram program = compiledParts[p].Program;
                    if (program == null || !program.Operations.IsCreated) continue;
                    RunNearestBatches(program, vertices, nearestDistances, nearestPrograms,
                        p, isBody: false, maxOps, batchSize, scratch);
                }

                if (bodyProgram != null && bodyProgram.Operations.IsCreated)
                {
                    RunNearestBatches(bodyProgram, vertices, nearestDistances, nearestPrograms,
                        compiledParts.Count, isBody: true, maxOps, batchSize, scratch);
                }
            }
            finally
            {
                scratch.Dispose();
            }
        }

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
            float raw = SdfProgramEvaluator.EvaluateInto(
                Operations, RootIndex, point, ScratchValues, index * ScratchStride,
                InfluenceRadius, allowCulling: true);
            float candidate = math.abs(raw);
            if (float.IsPositiveInfinity(candidate)) return;

            float current = NearestDistances[vertexIndex];
            bool wins = IsBody ? candidate <= current : candidate < current;
            if (wins)
            {
                NearestDistances[vertexIndex] = candidate;
                NearestPrograms[vertexIndex] = ProgramIndex;
            }
        }
    }
}
