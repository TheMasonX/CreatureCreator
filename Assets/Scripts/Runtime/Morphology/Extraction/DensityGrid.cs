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
    /// A fixed-resolution 3D grid of SDF samples covering a creature's bounds.
    /// Corner-count-per-axis follows the exact same ceiling formula
    /// GenerationSettings.EstimateVoxelCount uses for its safety-budget estimate
    /// (Sprint 3.1), so the estimate a definition was validated against matches
    /// what actually gets allocated here — callers must have already run that
    /// budget check via DefinitionValidator before calling SamplePortable.
    ///
    /// CC-064 non-finite contract: in Fast culling mode samples may read `+inf`
    /// ("outside/culled"), never NaN. Consumers that scan the grid (min/max,
    /// interpolation, normalization) must treat `+inf` as absent rather than a
    /// giant finite distance.
    /// </summary>
    public sealed class DensityGrid : IDisposable
    {
        private const int PortableScratchValueBudget = 8 * 1024 * 1024;

        /// <summary>
        /// Owned native corner samples. Allocated by <see cref="SamplePortable"/>
        /// (Persistent) and released by <see cref="Dispose"/>. The managed read
        /// API (<see cref="GetSample"/>, <see cref="CopyCellCornerSamples"/>)
        /// reads this buffer directly and Burst jobs (sampling, active-cell
        /// classification) read the same storage, so the grid never round-trips
        /// through a managed copy.
        /// </summary>
        private NativeArray<float> _samples;

        public int CellsX { get; }
        public int CellsY { get; }
        public int CellsZ { get; }
        public Vector3 Origin { get; }
        public float CellSize { get; }
        public int SampleCount => _samples.Length;

        /// <summary>
        /// The native corner-sample buffer, exposed for Burst consumers such as
        /// <see cref="ActiveCellBuilder"/>'s scan job. Read-only for callers;
        /// the grid owns the buffer's lifetime.
        /// </summary>
        public NativeArray<float> Samples => _samples;

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

        /// <summary>
        /// Releases the native sample buffer. Every caller that creates a grid
        /// via <see cref="SamplePortable"/> must dispose it when done: the
        /// generator disposes after extraction and tests wrap grids in using.
        /// </summary>
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

            // The grid owns this Persistent buffer: handed to the DensityGrid on
            // success and disposed on every throw path, so a malformed program can
            // never leak the native allocation (CC-075).
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

            // One scratch buffer sized for the largest batch, reused across every
            // batch (each job completes before the next starts, so reuse is safe).
            // Avoids allocating and freeing the buffer once per batch.
            var scratchValues = new NativeArray<float>((int)scratchLength, Allocator.TempJob);
            try
            {
                // Fail fast on a malformed program before any batch runs. Without
                // this, an out-of-range RootIndex reads past Operations and either
                // crashes (safety checks on) or silently produces garbage (Burst
                // release). Mirrors the SdfProgramEvaluator.Evaluate guard. Throwing
                // inside the try also proves the native allocations are disposed on
                // the exception path (CC-075).
                if (program.RootIndex < 0 || program.RootIndex >= program.Operations.Length)
                {
                    throw new DomainException("Portable program root index must identify an operation.");
                }

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
                    };
                    JobHandle handle = job.Schedule(sampleCount, 64);
                    handle.Complete();
                }

                // Ownership of the native buffer transfers to the grid; the finally
                // below only disposes it on a throw path (CC-075).
                var grid = new DensityGrid(cellsX, cellsY, cellsZ, origin, cellSize, samples);
                samples = default;
                return grid;
            }
            finally
            {
                scratchValues.Dispose();
                if (samples.IsCreated)
                {
                    samples.Dispose();
                }
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

            float dx = GetSample(nextX, y, z) - GetSample(previousX, y, z);
            float dy = GetSample(x, nextY, z) - GetSample(x, previousY, z);
            float dz = GetSample(x, y, nextZ) - GetSample(x, y, previousZ);

            float xSpan = (nextX - previousX) * CellSize;
            float ySpan = (nextY - previousY) * CellSize;
            float zSpan = (nextZ - previousZ) * CellSize;

            return new Vector3(
                xSpan > 0f ? dx / xSpan : 0f,
                ySpan > 0f ? dy / ySpan : 0f,
                zSpan > 0f ? dz / zSpan : 0f);
        }

        private void SetSample(int x, int y, int z, float value)
        {
            _samples[Index(x, y, z)] = value;
        }

        private int Index(int x, int y, int z)
        {
            return (z * CornersY + y) * CornersX + x;
        }
    }
}
