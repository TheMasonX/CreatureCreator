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
    /// budget check via DefinitionValidator before calling Sample.
    /// </summary>
    public sealed class DensityGrid
    {
        private readonly float[] _samples;

        public int CellsX { get; }
        public int CellsY { get; }
        public int CellsZ { get; }
        public Vector3 Origin { get; }
        public float CellSize { get; }
        public int SampleCount => _samples.Length;

        private int CornersX => CellsX + 1;
        private int CornersY => CellsY + 1;
        private int CornersZ => CellsZ + 1;

        private DensityGrid(int cellsX, int cellsY, int cellsZ, Vector3 origin, float cellSize)
        {
            CellsX = cellsX;
            CellsY = cellsY;
            CellsZ = cellsZ;
            Origin = origin;
            CellSize = cellSize;
            _samples = new float[(long)(cellsX + 1) * (cellsY + 1) * (cellsZ + 1) <= int.MaxValue
                ? (cellsX + 1) * (cellsY + 1) * (cellsZ + 1)
                : throw new DomainException("Grid corner count exceeds addressable array size.")];
        }

        public static DensityGrid Sample(ISdfNode node, BoundsDefinition bounds, GenerationSettings settings)
        {
            if (node == null) throw new DomainException("node must not be null.");
            if (!bounds.IsFinite() || !bounds.IsPositive())
            {
                throw new DomainException("Cannot sample a grid over invalid bounds; validate first.");
            }
            if (!settings.IsFinite() || !settings.IsPositive())
            {
                throw new DomainException("Cannot sample a grid with invalid GenerationSettings; validate first.");
            }

            float cellSize = 1f / settings.VoxelsPerUnit;
            int cellsX = Mathf.CeilToInt(bounds.MaxX * 2f * settings.VoxelsPerUnit);
            int cellsY = Mathf.CeilToInt(bounds.MaxY * 2f * settings.VoxelsPerUnit);
            int cellsZ = Mathf.CeilToInt(bounds.MaxZ * 2f * settings.VoxelsPerUnit);
            cellsX = Mathf.Max(cellsX, 1);
            cellsY = Mathf.Max(cellsY, 1);
            cellsZ = Mathf.Max(cellsZ, 1);

            Vector3 origin = new Vector3(-bounds.MaxX, -bounds.MaxY, -bounds.MaxZ);
            var grid = new DensityGrid(cellsX, cellsY, cellsZ, origin, cellSize);

            for (int z = 0; z < grid.CornersZ; z++)
            for (int y = 0; y < grid.CornersY; y++)
            for (int x = 0; x < grid.CornersX; x++)
            {
                Vector3 worldPoint = grid.CornerPosition(x, y, z);
                grid.SetSample(x, y, z, node.Evaluate(worldPoint));
            }

            return grid;
        }

        public static DensityGrid SamplePortable(SdfProgram program, BoundsDefinition bounds, GenerationSettings settings)
        {
            if (program == null) throw new DomainException("program must not be null.");
            ValidateSamplingInputs(bounds, settings);

            float cellSize = 1f / settings.VoxelsPerUnit;
            int cellsX = Mathf.Max(Mathf.CeilToInt(bounds.MaxX * 2f * settings.VoxelsPerUnit), 1);
            int cellsY = Mathf.Max(Mathf.CeilToInt(bounds.MaxY * 2f * settings.VoxelsPerUnit), 1);
            int cellsZ = Mathf.Max(Mathf.CeilToInt(bounds.MaxZ * 2f * settings.VoxelsPerUnit), 1);
            var grid = new DensityGrid(cellsX, cellsY, cellsZ,
                new Vector3(-bounds.MaxX, -bounds.MaxY, -bounds.MaxZ), cellSize);
            var samples = new NativeArray<float>(grid.SampleCount, Allocator.TempJob);
            long scratchLength = (long)grid.SampleCount * program.Operations.Length;
            if (scratchLength > int.MaxValue)
            {
                samples.Dispose();
                throw new DomainException("Portable sampler scratch buffer exceeds addressable array size.");
            }
            var scratchValues = new NativeArray<float>((int)scratchLength, Allocator.TempJob);
            var job = new SdfSamplingJob
            {
                Operations = program.Operations,
                ScratchValues = scratchValues,
                Samples = samples,
                RootIndex = program.RootIndex,
                CornersX = grid.CornersX,
                CornersY = grid.CornersY,
                CornersZ = grid.CornersZ,
                Origin = new float3(grid.Origin.x, grid.Origin.y, grid.Origin.z),
                CellSize = grid.CellSize,
            };
            JobHandle handle = job.Schedule(grid.SampleCount, 64);
            handle.Complete();
            for (int i = 0; i < grid._samples.Length; i++) grid._samples[i] = samples[i];
            samples.Dispose();
            scratchValues.Dispose();
            return grid;
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
