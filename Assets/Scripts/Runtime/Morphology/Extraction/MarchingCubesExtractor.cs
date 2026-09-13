using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Morphology.Extraction
{
    /// <summary>
    /// Extracts a triangle mesh from a DensityGrid using CubeContourResolver's
    /// per-cube loops. Shared grid-edge/corner identity is owned here and uses
    /// direct integer tables rather than a hash map; winding uses the cached grid
    /// gradient and never re-evaluates the SDF.
    /// </summary>
    public static partial class MarchingCubesExtractor
    {
        private const float CoarseLoopSuppressionCellSize = 0.2f;
        private const int MaxCubeEdges = 12;

        public static MeshExtractionResult Extract(DensityGrid grid) => Extract(grid, collectTimings: false);

        public static MeshExtractionResult Extract(DensityGrid grid, bool collectTimings) => ExtractCachedGrid(grid, collectTimings);

        private static MeshExtractionResult ExtractCachedGrid(DensityGrid grid, bool collectTimings)
        {
            if (grid == null) throw new DomainException("grid must not be null.");
            var result = new MeshExtractionResult();
            var ownership = new GridVertexOwnership(grid);
            var cornerDensities = new float[8];
            var cornerPositions = new Vector3[8];
            var loopIndices = new int[MaxCubeEdges];
            long contourResolutionTicks = 0, vertexWeldingTicks = 0, triangleEmissionTicks = 0;

            Stopwatch activeCellStopwatch = collectTimings ? Stopwatch.StartNew() : null;
            ActiveCellEntry[] activeCells = ActiveCellBuilder.Build(grid);
            if (collectTimings)
            {
                activeCellStopwatch.Stop();
                result.ActiveCellConstructionTime = StopwatchTicksToTimeSpan(activeCellStopwatch.ElapsedTicks);
            }

            for (int i = 0; i < activeCells.Length; i++)
            {
                ActiveCellEntry cell = activeCells[i];
                ActiveCellBuilder.DecodeCellIndex(cell.CellIndex, grid.CellsX, grid.CellsY, out int cx, out int cy, out int cz);
                grid.CopyCellCornerSamples(cx, cy, cz, cornerDensities);
                for (int c = 0; c < 8; c++) cornerDensities[c] = GenerationTolerances.NormalizeSurfaceDensity(cornerDensities[c]);
                for (int c = 0; c < 8; c++)
                {
                    Vector3Int offset = CubeTopology.CornerGridOffsets[c];
                    cornerPositions[c] = grid.CornerPosition(cx + offset.x, cy + offset.y, cz + offset.z);
                }

                result.MixedCellCount++;
                result.ContourResolutionCallCount++;
                List<List<CubeContourResolver.LoopVertex>> loops;
                if (collectTimings)
                {
                    long contourStart = Stopwatch.GetTimestamp();
                    loops = CubeContourResolver.ResolveLoops(cornerDensities, cornerPositions);
                    contourResolutionTicks += Stopwatch.GetTimestamp() - contourStart;
                }
                else loops = CubeContourResolver.ResolveLoops(cornerDensities, cornerPositions);

                foreach (List<CubeContourResolver.LoopVertex> loop in loops)
                {
                    if (ShouldSuppressCoarseLoop(loop, grid)) continue;
                    EmitLoop(grid, loop, cx, cy, cz, ownership, result, loopIndices,
                        collectTimings, ref vertexWeldingTicks, ref triangleEmissionTicks);
                }
            }

            result.ContourResolutionTime = StopwatchTicksToTimeSpan(contourResolutionTicks);
            result.VertexWeldingTime = StopwatchTicksToTimeSpan(vertexWeldingTicks);
            result.TriangleEmissionTime = StopwatchTicksToTimeSpan(triangleEmissionTicks);
            return result;
        }

        private static bool ShouldSuppressCoarseLoop(List<CubeContourResolver.LoopVertex> loop, DensityGrid grid)
        {
            if (loop == null || loop.Count < 3) return true;
            if (grid.CellSize < CoarseLoopSuppressionCellSize) return false;
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (CubeContourResolver.LoopVertex vertex in loop)
            {
                Vector3 p = vertex.Position;
                min.x = Mathf.Min(min.x, p.x); min.y = Mathf.Min(min.y, p.y); min.z = Mathf.Min(min.z, p.z);
                max.x = Mathf.Max(max.x, p.x); max.y = Mathf.Max(max.y, p.y); max.z = Mathf.Max(max.z, p.z);
            }
            Vector3 extent = max - min;
            float maximumExtent = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));
            return maximumExtent <= grid.CellSize * 0.75f;
        }

        private static void EmitLoop(DensityGrid grid, List<CubeContourResolver.LoopVertex> loop,
            int cx, int cy, int cz, GridVertexOwnership ownership, MeshExtractionResult result, int[] indices,
            bool collectTimings, ref long vertexWeldingTicks, ref long triangleEmissionTicks)
        {
            if (loop.Count < 3) return;
            if (loop.Count > indices.Length) throw new DomainException($"Cube contour loop contains {loop.Count} edges; maximum supported is {indices.Length}.");
            for (int i = 0; i < loop.Count; i++)
            {
                if (collectTimings)
                {
                    long start = Stopwatch.GetTimestamp();
                    indices[i] = ResolveVertexIndex(loop[i], grid, cx, cy, cz, ownership, result);
                    vertexWeldingTicks += Stopwatch.GetTimestamp() - start;
                }
                else indices[i] = ResolveVertexIndex(loop[i], grid, cx, cy, cz, ownership, result);
            }
            for (int i = 1; i < loop.Count - 1; i++)
            {
                if (collectTimings)
                {
                    long start = Stopwatch.GetTimestamp();
                    EmitTriangle(grid, result, indices[0], indices[i], indices[i + 1]);
                    triangleEmissionTicks += Stopwatch.GetTimestamp() - start;
                }
                else EmitTriangle(grid, result, indices[0], indices[i], indices[i + 1]);
            }
        }

        private static System.TimeSpan StopwatchTicksToTimeSpan(long ticks) => System.TimeSpan.FromSeconds((double)ticks / Stopwatch.Frequency);

        private static int ResolveVertexIndex(CubeContourResolver.LoopVertex vertex, DensityGrid grid,
            int cx, int cy, int cz, GridVertexOwnership ownership, MeshExtractionResult result)
        {
            (int A, int B) edgeCorners = CubeTopology.EdgeCorners[vertex.EdgeIndex];
            Vector3Int offsetA = CubeTopology.CornerGridOffsets[edgeCorners.A];
            Vector3Int offsetB = CubeTopology.CornerGridOffsets[edgeCorners.B];
            Vector3Int gridA = new Vector3Int(cx + offsetA.x, cy + offsetA.y, cz + offsetA.z);
            Vector3Int gridB = new Vector3Int(cx + offsetB.x, cy + offsetB.y, cz + offsetB.z);
            Vector3 positionA = grid.CornerPosition(gridA.x, gridA.y, gridA.z);
            Vector3 positionB = grid.CornerPosition(gridB.x, gridB.y, gridB.z);

            if (GenerationTolerances.NormalizeSurfaceDensity(grid.GetSample(gridA.x, gridA.y, gridA.z)) == 0f)
                return ownership.ResolveCorner(gridA.x, gridA.y, gridA.z, positionA, result);
            if (GenerationTolerances.NormalizeSurfaceDensity(grid.GetSample(gridB.x, gridB.y, gridB.z)) == 0f)
                return ownership.ResolveCorner(gridB.x, gridB.y, gridB.z, positionB, result);

            int axis = gridA.x != gridB.x ? 0 : gridA.y != gridB.y ? 1 : 2;
            Vector3Int lower = axis switch
            {
                0 => gridA.x < gridB.x ? gridA : gridB,
                1 => gridA.y < gridB.y ? gridA : gridB,
                _ => gridA.z < gridB.z ? gridA : gridB,
            };
            return ownership.ResolveEdge(axis, lower.x, lower.y, lower.z, vertex.Position, result);
        }

        private static void EmitTriangle(DensityGrid grid, MeshExtractionResult result, int i0, int i1, int i2)
        {
            Vector3 p0 = result.Positions[i0], p1 = result.Positions[i1], p2 = result.Positions[i2];
            Vector3 faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
            if (faceNormal.sqrMagnitude < 1e-12f) return;
            Vector3 centroid = (p0 + p1 + p2) / 3f;
            result.GradientEvaluationCount++;
            Vector3 gradient;
            if (!grid.TryEstimateGradient(centroid, out gradient)) gradient = grid.EstimateGradient(centroid);
            if (Vector3.Dot(faceNormal, gradient) >= 0f)
            {
                result.Triangles.Add(i0); result.Triangles.Add(i1); result.Triangles.Add(i2);
            }
            else
            {
                result.Triangles.Add(i0); result.Triangles.Add(i2); result.Triangles.Add(i1);
            }
        }

        private sealed class GridVertexOwnership
        {
            private readonly int _cornersX, _cornersY, _cellsX, _cellsY;
            private readonly int _xEdgeCount, _yEdgeCount;
            private readonly int[] _cornerOwners, _edgeOwners;

            public GridVertexOwnership(DensityGrid grid)
            {
                _cornersX = checked(grid.CellsX + 1); _cornersY = checked(grid.CellsY + 1);
                _cellsX = grid.CellsX; _cellsY = grid.CellsY;
                long cornerCount = (long)_cornersX * _cornersY * checked(grid.CellsZ + 1);
                long xEdgeCount = (long)_cellsX * _cornersY * checked(grid.CellsZ + 1);
                long yEdgeCount = (long)_cornersX * _cellsY * checked(grid.CellsZ + 1);
                long zEdgeCount = (long)_cornersX * _cornersY * grid.CellsZ;
                long edgeCount = xEdgeCount + yEdgeCount + zEdgeCount;
                if (cornerCount > int.MaxValue || edgeCount > int.MaxValue) throw new DomainException("Grid vertex ownership table exceeds addressable array size.");
                _xEdgeCount = (int)xEdgeCount; _yEdgeCount = (int)yEdgeCount;
                _cornerOwners = new int[(int)cornerCount]; _edgeOwners = new int[(int)edgeCount];
            }

            public int ResolveCorner(int x, int y, int z, Vector3 position, MeshExtractionResult result)
            {
                int key = ((z * _cornersY) + y) * _cornersX + x;
                return GetOrCreate(_cornerOwners, key, position, result);
            }

            public int ResolveEdge(int axis, int x, int y, int z, Vector3 position, MeshExtractionResult result)
            {
                int localIndex, offset;
                switch (axis)
                {
                    case 0: localIndex = ((z * _cornersY) + y) * _cellsX + x; offset = 0; break;
                    case 1: localIndex = ((z * _cellsY) + y) * _cornersX + x; offset = _xEdgeCount; break;
                    case 2: localIndex = ((z * _cornersY) + y) * _cornersX + x; offset = _xEdgeCount + _yEdgeCount; break;
                    default: throw new DomainException("edge axis must be X (0), Y (1), or Z (2).");
                }
                return GetOrCreate(_edgeOwners, checked(offset + localIndex), position, result);
            }

            private static int GetOrCreate(int[] owners, int key, Vector3 position, MeshExtractionResult result)
            {
                int encodedOwner = owners[key];
                if (encodedOwner != 0) return encodedOwner - 1;
                int newIndex = result.Positions.Count;
                result.Positions.Add(position);
                owners[key] = checked(newIndex + 1);
                return newIndex;
            }
        }
    }
}
