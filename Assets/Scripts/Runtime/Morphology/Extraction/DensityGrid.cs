using System;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Morphology.Extraction
{
    public sealed class DensityGrid : IDisposable
    {
        private const int ScratchValueBudget = 8 * 1024 * 1024;
        private const int MaxRowsPerExecute = 8;
        private NativeArray<float> _samples;
        public int CellsX { get; }
        public int CellsY { get; }
        public int CellsZ { get; }
        public Vector3 Origin { get; }
        public float CellSize { get; }
        public int SampleCount => _samples.Length;
        public NativeArray<float>.ReadOnly Samples => _samples.AsReadOnly();
        internal NativeArray<float> MutableSamples => _samples;
        private int CornersX => CellsX + 1;
        private int CornersY => CellsY + 1;

        private DensityGrid(int cellsX, int cellsY, int cellsZ, Vector3 origin, float cellSize, NativeArray<float> samples)
        {
            CellsX = cellsX; CellsY = cellsY; CellsZ = cellsZ; Origin = origin; CellSize = cellSize; _samples = samples;
        }

        public void Dispose()
        {
            if (_samples.IsCreated) { _samples.Dispose(); _samples = default; }
        }

        public static DensityGrid SamplePortable(SdfProgram program, BoundsDefinition bounds, GenerationSettings settings)
        {
            if (program == null) throw new DomainException("program must not be null.");
            ValidateSamplingInputs(bounds, settings);

            float cellSize = 1f / settings.VoxelsPerUnit;
            int cellsX = Mathf.Max(Mathf.CeilToInt(bounds.MaxX * 2f * settings.VoxelsPerUnit), 1);
            int cellsY = Mathf.Max(Mathf.CeilToInt(bounds.MaxY * 2f * settings.VoxelsPerUnit), 1);
            int cellsZ = Mathf.Max(Mathf.CeilToInt(bounds.MaxZ * 2f * settings.VoxelsPerUnit), 1);
            int cornersX = cellsX + 1;
            int cornersY = cellsY + 1;
            int cornersZ = cellsZ + 1;
            long cornerCountLong = (long)cornersX * cornersY * cornersZ;
            if (cornerCountLong > int.MaxValue) throw new DomainException("Grid corner count exceeds addressable array size.");

            var origin = new Vector3(-bounds.MaxX, -bounds.MaxY, -bounds.MaxZ);
            var samples = new NativeArray<float>((int)cornerCountLong, Allocator.Persistent);
            int operationCount = program.Operations.Length;
            if (operationCount <= 0) { samples.Dispose(); throw new DomainException("Portable program must contain at least one operation."); }
            if (program.RootIndex < 0 || program.RootIndex >= operationCount) { samples.Dispose(); throw new DomainException("Portable program root index must identify an operation."); }

            long rowScratchLength = (long)cornersX * operationCount;
            if (rowScratchLength > ScratchValueBudget) rowScratchLength = (long)cornersX * operationCount;
            int rowsPerExecute = Mathf.Max(1, Mathf.Min(MaxRowsPerExecute, (int)(ScratchValueBudget / Mathf.Max(rowScratchLength, 1L))));
            long scratchLength = rowScratchLength * rowsPerExecute;
            if (scratchLength > int.MaxValue)
            {
                samples.Dispose();
                throw new DomainException("Portable sampler scratch buffer exceeds addressable array size.");
            }

            var scratchValues = new NativeArray<float>((int)scratchLength, Allocator.Persistent);
            try
            {
                var job = new SdfSamplingRowBatchJob
                {
                    Operations = program.Operations,
                    ScratchValues = scratchValues,
                    Samples = samples,
                    RootIndex = program.RootIndex,
                    CornersX = cornersX,
                    CornersY = cornersY,
                    Origin = new float3(origin.x, origin.y, origin.z),
                    CellSize = cellSize,
                    InfluenceRadius = program.InfluenceRadius,
                    RootHasPotentialBounds = program.HasPotentialBounds,
                    RootPotentialMinBound = program.PotentialMinBound,
                    RootPotentialMaxBound = program.PotentialMaxBound,
                    RowsPerExecute = rowsPerExecute,
                };
                int rowCount = cornersY * cornersZ;
                int workItemCount = (rowCount + rowsPerExecute - 1) / rowsPerExecute;
                job.Schedule(workItemCount, 1).Complete();

                var grid = new DensityGrid(cellsX, cellsY, cellsZ, origin, cellSize, samples);
                samples = default;
                return grid;
            }
            finally
            {
                scratchValues.Dispose();
                if (samples.IsCreated) samples.Dispose();
            }
        }

        private static void ValidateSamplingInputs(BoundsDefinition bounds, GenerationSettings settings)
        {
            if (!bounds.IsFinite() || !bounds.IsPositive()) throw new DomainException("Cannot sample a grid over invalid bounds; validate first.");
            if (!settings.IsFinite() || !settings.IsPositive()) throw new DomainException("Cannot sample a grid with invalid GenerationSettings; validate first.");
        }

        public Vector3 CornerPosition(int x, int y, int z) => Origin + new Vector3(x, y, z) * CellSize;
        public float GetSample(int x, int y, int z) => _samples[Index(x, y, z)];

        public void CopyCellCornerSamples(int x, int y, int z, float[] destination)
        {
            if (destination == null || destination.Length < 8) throw new DomainException("destination must have at least 8 entries.");
            int rowStride = CornersX;
            int sliceStride = CornersX * CornersY;
            int baseIndex = (z * CornersY + y) * CornersX + x;
            destination[0] = _samples[baseIndex]; destination[1] = _samples[baseIndex + 1];
            destination[2] = _samples[baseIndex + rowStride]; destination[3] = _samples[baseIndex + rowStride + 1];
            destination[4] = _samples[baseIndex + sliceStride]; destination[5] = _samples[baseIndex + sliceStride + 1];
            destination[6] = _samples[baseIndex + sliceStride + rowStride]; destination[7] = _samples[baseIndex + sliceStride + rowStride + 1];
        }

        public Vector3 EstimateGradient(Vector3 point)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((point.x - Origin.x) / CellSize), 0, CellsX);
            int y = Mathf.Clamp(Mathf.RoundToInt((point.y - Origin.y) / CellSize), 0, CellsY);
            int z = Mathf.Clamp(Mathf.RoundToInt((point.z - Origin.z) / CellSize), 0, CellsZ);
            int previousX = Mathf.Max(x - 1, 0); int nextX = Mathf.Min(x + 1, CellsX);
            int previousY = Mathf.Max(y - 1, 0); int nextY = Mathf.Min(y + 1, CellsY);
            int previousZ = Mathf.Max(z - 1, 0); int nextZ = Mathf.Min(z + 1, CellsZ);
            float gx = EstimateAxis(GetSample(previousX, y, z), GetSample(x, y, z), GetSample(nextX, y, z), (nextX - previousX) * CellSize);
            float gy = EstimateAxis(GetSample(x, previousY, z), GetSample(x, y, z), GetSample(x, nextY, z), (nextY - previousY) * CellSize);
            float gz = EstimateAxis(GetSample(x, y, previousZ), GetSample(x, y, z), GetSample(x, y, nextZ), (nextZ - previousZ) * CellSize);
            return new Vector3(gx, gy, gz);
        }

        public bool TryEstimateGradient(Vector3 point, out Vector3 gradient)
        {
            gradient = Vector3.zero;
            float fx = (point.x - Origin.x) / CellSize; float fy = (point.y - Origin.y) / CellSize; float fz = (point.z - Origin.z) / CellSize;
            int x = Mathf.Clamp(Mathf.FloorToInt(fx), 0, CellsX - 1); int y = Mathf.Clamp(Mathf.FloorToInt(fy), 0, CellsY - 1); int z = Mathf.Clamp(Mathf.FloorToInt(fz), 0, CellsZ - 1);
            int x1 = x + 1, y1 = y + 1, z1 = z + 1;
            float u = fx - x, v = fy - y, w = fz - z;
            float c000 = _samples[Index(x, y, z)], c100 = _samples[Index(x1, y, z)], c010 = _samples[Index(x, y1, z)], c110 = _samples[Index(x1, y1, z)];
            float c001 = _samples[Index(x, y, z1)], c101 = _samples[Index(x1, y, z1)], c011 = _samples[Index(x, y1, z1)], c111 = _samples[Index(x1, y1, z1)];
            if (!NumericValidity.IsFinite(c000) || !NumericValidity.IsFinite(c100) || !NumericValidity.IsFinite(c010) || !NumericValidity.IsFinite(c110) || !NumericValidity.IsFinite(c001) || !NumericValidity.IsFinite(c101) || !NumericValidity.IsFinite(c011) || !NumericValidity.IsFinite(c111)) return false;
            float du = (c100 - c000) * (1f - v) * (1f - w) + (c110 - c010) * v * (1f - w) + (c101 - c001) * (1f - v) * w + (c111 - c011) * v * w;
            float dv = (c010 - c000) * (1f - u) * (1f - w) + (c110 - c100) * u * (1f - w) + (c011 - c001) * (1f - u) * w + (c111 - c101) * u * w;
            float dw = (c001 - c000) * (1f - u) * (1f - v) + (c101 - c100) * u * (1f - v) + (c011 - c010) * (1f - u) * v + (c111 - c110) * u * v;
            gradient = new Vector3(du / CellSize, dv / CellSize, dw / CellSize); return true;
        }

        private static float EstimateAxis(float previous, float center, float next, float span)
        {
            if (span <= 0f || float.IsNaN(center) || float.IsInfinity(center)) return 0f;
            bool previousFinite = !float.IsNaN(previous) && !float.IsInfinity(previous); bool nextFinite = !float.IsNaN(next) && !float.IsInfinity(next);
            if (previousFinite && nextFinite) return (next - previous) / span;
            if (previousFinite) return (center - previous) / (span * 0.5f);
            if (nextFinite) return (next - center) / (span * 0.5f);
            return 0f;
        }

        private int Index(int x, int y, int z) => (z * CornersY + y) * CornersX + x;
    }

    [BurstCompile]
    public struct SdfSamplingRowBatchJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SdfOperation>.ReadOnly Operations;
        [NativeDisableParallelForRestriction] public NativeArray<float> ScratchValues;
        [NativeDisableParallelForRestriction] public NativeArray<float> Samples;
        public int RootIndex;
        public int CornersX;
        public int CornersY;
        public float3 Origin;
        public float CellSize;
        public float InfluenceRadius;
        public bool RootHasPotentialBounds;
        public float3 RootPotentialMinBound;
        public float3 RootPotentialMaxBound;
        public int RowsPerExecute;

        public void Execute(int workItemIndex)
        {
            int firstRow = workItemIndex * RowsPerExecute;
            int totalRows = Samples.Length / CornersX;
            int rowEnd = math.min(firstRow + RowsPerExecute, totalRows);
            int operationCount = Operations.Length;
            int rowScratchStride = CornersX * operationCount;

            for (int row = firstRow; row < rowEnd; row++)
            {
                int localRow = row - firstRow;
                int rowIndex = row;
                int y = rowIndex % CornersY;
                int z = rowIndex / CornersY;
                int sampleBase = (z * CornersY + y) * CornersX;
                int rowValueOffset = localRow * rowScratchStride;

                for (int x = 0; x < CornersX; x++)
                {
                    float3 point = Origin + new float3(x, y, z) * CellSize;
                    int sampleIndex = sampleBase + x;
                    if (RootHasPotentialBounds &&
                        (point.x < RootPotentialMinBound.x - InfluenceRadius || point.x > RootPotentialMaxBound.x + InfluenceRadius ||
                         point.y < RootPotentialMinBound.y - InfluenceRadius || point.y > RootPotentialMaxBound.y + InfluenceRadius ||
                         point.z < RootPotentialMinBound.z - InfluenceRadius || point.z > RootPotentialMaxBound.z + InfluenceRadius))
                    {
                        Samples[sampleIndex] = float.PositiveInfinity;
                        continue;
                    }
                    int valueOffset = rowValueOffset + x * operationCount;
                    Samples[sampleIndex] = SdfProgramEvaluator.EvaluateInto(Operations, RootIndex, point, ScratchValues, valueOffset, InfluenceRadius, allowCulling: true);
                }
            }
        }
    }
}
