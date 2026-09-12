using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
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
            var scratch = new NativeArray<float>(rowScratchStride * workItemCount, Allocator.Persistent);
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
                    RowsPerExecute = rowsPerExecute,
                };

                // Schedule, rather than Run, is intentional: this regression must
                // exercise multiple parallel work items sharing the same backing
                // scratch array. A missing work-item slice offset creates a real
                // data race and can corrupt operation values between dependent ops.
                job.Schedule(workItemCount, 1).Complete();

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
                scratch.Dispose();
                operations.Dispose();
            }
        }
    }
}
