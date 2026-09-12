using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class SdfSamplingRowBatchJobTests
    {
        [Test]
        public void MultipleWorkItems_IsolateScratchRowsAndMatchReference()
        {
            const int cornersX = 4;
            const int cornersY = 4;
            const int cornersZ = 2;
            const int totalRows = cornersY * cornersZ;
            const int rowsPerExecute = 2;

            var operations = new NativeArray<SdfOperation>(3, Allocator.Persistent);
            operations[0] = SdfOperation.Primitive(
                SdfOperationType.Sphere, new float3(0.75f, 0f, 0f));
            operations[1] = SdfOperation.Primitive(
                SdfOperationType.Sphere, new float3(0.5f, 0f, 0f));
            operations[2] = new SdfOperation
            {
                Type = SdfOperationType.SmoothUnion,
                A = 0,
                B = 1,
                Parameters = new float3(0.25f, 0f, 0f),
            };

            int rowScratchStride = cornersX * operations.Length;
            int workItemCount = (totalRows + rowsPerExecute - 1) / rowsPerExecute;
            // Each work item owns rowsPerExecute rows of disjoint scratch, so the
            // backing array must cover the whole scheduled work-item set. The trailing
            // half is a sentinel guard and must stay untouched: Burst disables NativeArray
            // bounds checks, so an escaped scratch base offset corrupts memory silently
            // instead of throwing.
            int scratchLength = rowScratchStride * rowsPerExecute * workItemCount;
            const float sentinel = -98765.5f;
            var scratchBacking = new NativeArray<float>(scratchLength * 2, Allocator.Persistent);
            for (int i = scratchLength; i < scratchBacking.Length; i++)
            {
                scratchBacking[i] = sentinel;
            }
            NativeArray<float> scratch = scratchBacking.GetSubArray(0, scratchLength);
            var samples = new NativeArray<float>(cornersX * totalRows, Allocator.Persistent);
            try
            {
                var job = new SdfSamplingRowBatchJob
                {
                    Operations = operations.AsReadOnly(),
                    ScratchValues = scratch,
                    Samples = samples,
                    RootIndex = 2,
                    CornersX = cornersX,
                    CornersY = cornersY,
                    Origin = new float3(-1f, -1f, 0f),
                    CellSize = 0.5f,
                    InfluenceRadius = 0.25f,
                    RootHasPotentialBounds = false,
                    RootPotentialMinBound = default,
                    RootPotentialMaxBound = default,
                    RowStart = 0,
                    RowsPerExecute = rowsPerExecute,
                };

                // Schedule, rather than Run, is intentional: this regression must
                // exercise multiple parallel work items sharing the same backing
                // scratch array. A missing work-item slice offset creates a real
                // data race and can corrupt operation values between dependent ops.
                job.Schedule(workItemCount, 1).Complete();

                for (int i = scratchLength; i < scratchBacking.Length; i++)
                {
                    Assert.AreEqual(sentinel, scratchBacking[i],
                        $"scratch writes escaped the {scratchLength}-float slice into guard index {i}");
                }

                for (int row = 0; row < totalRows; row++)
                for (int x = 0; x < cornersX; x++)
                {
                    int sampleIndex = row * cornersX + x;
                    float3 point = job.Origin + new float3(x, row % cornersY, row / cornersY) * job.CellSize;
                    float expected = SdfProgramEvaluator.Evaluate(
                        operations.AsReadOnly(), 2, point, 0.25f, allowCulling: false);
                    Assert.AreEqual(expected, samples[sampleIndex], 0f,
                        $"parallel batch mismatch at row={row}, x={x}");
                }
            }
            finally
            {
                samples.Dispose();
                scratchBacking.Dispose();
                operations.Dispose();
            }
        }
    }
}
