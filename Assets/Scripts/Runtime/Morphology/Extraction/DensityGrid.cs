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
    /// <summary>
    /// A fixed-resolution 3D grid of SDF corner samples covering a creature's
    /// bounds. Corner-count per axis follows the same ceiling formula as
    /// <c>GenerationSettings.EstimateVoxelCount</c>; callers must have run the
    /// corner-sample budget check before calling <see cref="SamplePortable"/>.
    ///
    /// CC-064 non-finite contract: fast samples may read <c>+inf</c>
    /// (outside/culled), never NaN. Grid consumers (min/max, interpolation,
    /// gradient) must treat <c>+inf</c> as absent, not as a giant finite distance.
    /// </summary>
    public sealed class DensityGrid : IDisposable
    {
        private const int PortableScratchValueBudget = 8 * 1024 * 1024;
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
        private int CornersZ => CellsZ + 1;

        private DensityGrid(int cellsX, int cellsY, int cellsZ, Vector3 origin, float cellSize, NativeArray<float> samples)
        {
            CellsX = cellsX;
            CellsY = cellsY;
            CellsZ = cellsZ;
            Origin = origin;
            CellSize = cellSize;
            _samples = samples;
        }

        public void Dispose()
        {
            if (_samples.IsCreated)
            {
                _samples.Dispose();
                _samples = default;
            }
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
            if (cornerCountLong > int.MaxValue)
            {
                throw new DomainException("Grid corner count exceeds addressable array size.");
            }
            var origin = new Vector3(-bounds.MaxX, -bounds.MaxY, -bounds.MaxZ);
            var samples = new NativeArray<float>((int)cornerCountLong, Allocator.Persistent);
            int operationCount = program.Operations.Length;
            if (operationCount <= 0)
            {
                samples.Dispose();
                throw new DomainException("Portable program must contain at least one operation.");
            }

            int batchSize = Mathf.Max(1, PortableScratchValueBudget / operationCount);
            long scratchLength = (long)batchSize * operationCount;
            if (scratchLength > int.MaxValue)
            {
                samples.Dispose();
                throw new DomainException("Portable sampler scratch buffer exceeds addressable array size.");
            }

            var scratchValues = new NativeArray<float>((int)scratchLength, Allocator.Persistent);
            try
            {
                if (program.RootIndex < 0 || program.RootIndex >= program.Operations.Length)
                {
                    throw new DomainException("Portable program root index must identify an operation.");
                }

                bool rootHasPotentialBounds = program.HasPotentialBounds;
                float3 rootMin = program.PotentialMinBound;
                float3 rootMax = program.PotentialMaxBound;

                for (int sampleStart = 0; sampleStart < (int)cornerCountLong; sampleStart += batchSize)
                {
                    int sampleCount = Mathf.Min(batchSize, (int)cornerCountLong - sampleStart);
                    var job = new SdfSamplingJob
                    {
                        Operations = program.Operations,
                        ScratchValues = scratchValues,
                        Samples = samples,
                        RootIndex = program.RootIndex,
                        CornersX = cornersX,
                        CornersY = cornersY,
                        CornersZ = cornersZ,
                        Origin = new float3(origin.x, origin.y, origin.z),
                        CellSize = cellSize,
                        SampleStartIndex = sampleStart,
                        InfluenceRadius = program.InfluenceRadius,
                        RootHasPotentialBounds = rootHasPotentialBounds,
                        RootPotentialMinBound = rootMin,
                        RootPotentialMaxBound = rootMax,
                    };
                    JobHandle handle = job.Schedule(sampleCount, 64);
                    handle.Complete();
                }

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
            if (!bounds.IsFinite() || !bounds.IsPositive())
            {
                throw new DomainException("Cannot sample a grid over invalid bounds; validate first.");
            }
            if (!settings.IsFinite() || !settings.IsPositive())
            {
                throw new DomainException("Cannot sample a grid with invalid GenerationSettings; validate first.");
            }
        }

        public Vector3 CornerPosition(int x, int y, int z)
        {
            return Origin + new Vector3(x, y, z) * CellSize;
        }

        public float GetSample(int x, int y, int z)
        {
            return _samples[Index(x, y, z)];
        }

        public void CopyCellCornerSamples(int x, int y, int z, float[] destination)
        {
            if (destination == null || destination.Length < 8)
            {
                throw new DomainException("destination must have at least 8 entries.");
            }

            int rowStride = CornersX;
            int sliceStride = CornersX * CornersY;
            int baseIndex = (z * CornersY + y) * CornersX + x;

            destination[0] = _samples[baseIndex];
            destination[1] = _samples[baseIndex + 1];
            destination[2] = _samples[baseIndex + rowStride];
            destination[3] = _samples[baseIndex + rowStride + 1];
            destination[4] = _samples[baseIndex + sliceStride];
            destination[5] = _samples[baseIndex + sliceStride + 1];
            destination[6] = _samples[baseIndex + sliceStride + rowStride];
            destination[7] = _samples[baseIndex + sliceStride + rowStride + 1];
        }

        public Vector3 EstimateGradient(Vector3 point)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((point.x - Origin.x) / CellSize), 0, CellsX);
            int y = Mathf.Clamp(Mathf.RoundToInt((point.y - Origin.y) / CellSize), 0, CellsY);
            int z = Mathf.Clamp(Mathf.RoundToInt((point.z - Origin.z) / CellSize), 0, CellsZ);

            int previousX = Mathf.Max(x - 1, 0);
            int nextX = Mathf.Min(x + 1, CellsX);
            int previousY = Mathf.Max(y - 1, 0);
            int nextY = Mathf.Min(y + 1, CellsY);
            int previousZ = Mathf.Max(z - 1, 0);
            int nextZ = Mathf.Min(z + 1, CellsZ);

            float gx = EstimateAxis(GetSample(previousX, y, z), GetSample(x, y, z), GetSample(nextX, y, z),
                (nextX - previousX) * CellSize);
            float gy = EstimateAxis(GetSample(x, previousY, z), GetSample(x, y, z), GetSample(x, nextY, z),
                (nextY - previousY) * CellSize);
            float gz = EstimateAxis(GetSample(x, y, previousZ), GetSample(x, y, z), GetSample(x, y, nextZ),
                (nextZ - previousZ) * CellSize);

            return new Vector3(gx, gy, gz);
        }

        public bool TryEstimateGradient(Vector3 point, out Vector3 gradient)
        {
            gradient = Vector3.zero;
            if (!NumericValidity.IsFinite(point)) return false;
            if (!NumericValidity.IsFinite(CellSize) || CellSize <= 0f) return false;

            float fx = Mathf.Clamp((point.x - Origin.x) / CellSize, 0f, CellsX);
            float fy = Mathf.Clamp((point.y - Origin.y) / CellSize, 0f, CellsY);
            float fz = Mathf.Clamp((point.z - Origin.z) / CellSize, 0f, CellsZ);

            int x = Mathf.Clamp(Mathf.FloorToInt(fx), 0, CellsX - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(fy), 0, CellsY - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(fz), 0, CellsZ - 1);
            int x1 = x + 1;
            int y1 = y + 1;
            int z1 = z + 1;
            float u = Mathf.Clamp01(fx - x);
            float v = Mathf.Clamp01(fy - y);
            float w = Mathf.Clamp01(fz - z);

            float c000 = _samples[Index(x, y, z)];
            float c100 = _samples[Index(x1, y, z)];
            float c010 = _samples[Index(x, y1, z)];
            float c110 = _samples[Index(x1, y1, z)];
            float c001 = _samples[Index(x, y, z1)];
            float c101 = _samples[Index(x1, y, z1)];
            float c011 = _samples[Index(x, y1, z1)];
            float c111 = _samples[Index(x1, y1, z1)];

            if (!NumericValidity.IsFinite(c000) || !NumericValidity.IsFinite(c100)
                || !NumericValidity.IsFinite(c010) || !NumericValidity.IsFinite(c110)
                || !NumericValidity.IsFinite(c001) || !NumericValidity.IsFinite(c101)
                || !NumericValidity.IsFinite(c011) || !NumericValidity.IsFinite(c111))
            {
                return false;
            }

            float du = (c100 - c000) * (1f - v) * (1f - w)
                + (c110 - c010) * v * (1f - w)
                + (c101 - c001) * (1f - v) * w
                + (c111 - c011) * v * w;
            float dv = (c010 - c000) * (1f - u) * (1f - w)
                + (c110 - c100) * u * (1f - w)
                + (c011 - c001) * (1f - u) * w
                + (c111 - c101) * u * w;
            float dw = (c001 - c000) * (1f - u) * (1f - v)
                + (c101 - c100) * u * (1f - v)
                + (c011 - c010) * (1f - u) * v
                + (c111 - c110) * u * v;

            gradient = new Vector3(du / CellSize, dv / CellSize, dw / CellSize);
            return NumericValidity.IsFinite(gradient);
        }

        private static float EstimateAxis(float previous, float center, float next, float span)
        {
            if (span <= 0f || !NumericValidity.IsFinite(center)) return 0f;

            bool previousFinite = NumericValidity.IsFinite(previous);
            bool nextFinite = NumericValidity.IsFinite(next);
            if (previousFinite && nextFinite)
            {
                float result = (next - previous) / span;
                return NumericValidity.IsFinite(result) ? result : 0f;
            }
            if (previousFinite)
            {
                float result = (center - previous) / (span * 0.5f);
                return NumericValidity.IsFinite(result) ? result : 0f;
            }
            if (nextFinite)
            {
                float result = (next - center) / (span * 0.5f);
                return NumericValidity.IsFinite(result) ? result : 0f;
            }
            return 0f;
        }

        private int Index(int x, int y, int z)
        {
            return (z * CornersY + y) * CornersX + x;
        }
    }
}
