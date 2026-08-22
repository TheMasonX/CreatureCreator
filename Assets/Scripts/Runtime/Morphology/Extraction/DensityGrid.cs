using UnityEngine;
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

        public Vector3 CornerPosition(int x, int y, int z)
        {
            return Origin + new Vector3(x, y, z) * CellSize;
        }

        public float GetSample(int x, int y, int z)
        {
            return _samples[Index(x, y, z)];
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
