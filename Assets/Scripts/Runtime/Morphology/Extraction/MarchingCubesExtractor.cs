using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Morphology.Extraction
{
    /// <summary>
    /// Extracts a triangle mesh from a DensityGrid using CubeContourResolver's
    /// per-cube loops. Two things happen here that CubeContourResolver
    /// deliberately doesn't do, because they're extraction-loop concerns, not
    /// per-cube concerns:
    ///
    /// VERTEX WELDING: a loop vertex sits on a specific grid edge (identified by
    /// its lower corner's grid coordinates + axis, independent of which of the up
    ///-to-8 cubes touching that edge computed it). Two neighboring cubes sharing
    /// an edge will each produce a loop vertex for it; keying a shared dictionary
    /// by that edge identity means both cubes reuse the SAME output vertex,
    /// producing a connected mesh instead of duplicated coincident vertices per
    /// cube — this is what actually delivers "avoid the fragmentation that
    /// complicates smooth skinning," not just the hole-closing itself.
    ///
    /// WINDING CONSISTENCY: CubeContourResolver's loop traversal direction isn't
    /// guaranteed to produce outward-facing winding on its own. Rather than solve
    /// global winding consistency analytically, each triangle's winding is
    /// corrected independently using a finite-difference gradient estimated from
    /// the cached DensityGrid samples: a correctly wound outward-facing triangle's
    /// face normal points in the same general direction as the gradient (density
    /// increases from inside/negative to outside/positive). This is a safe, local,
    /// per-triangle fix that doesn't depend on getting a global traversal-order
    /// argument right, and it avoids re-evaluating the full SDF during extraction.
    /// </summary>
    public static partial class MarchingCubesExtractor
    {
        private const float CoarseLoopSuppressionCellSize = 0.2f;
        private const int MaxCubeEdges = 12;

        public static MeshExtractionResult Extract(DensityGrid grid)
        {
            return Extract(grid, collectTimings: false);
        }

        public static MeshExtractionResult Extract(DensityGrid grid, bool collectTimings)
        {
            return ExtractCachedGrid(grid, collectTimings);
        }

        private static MeshExtractionResult ExtractCachedGrid(DensityGrid grid, bool collectTimings)
        {
            if (grid == null) throw new DomainException("grid must not be null.");

            var result = new MeshExtractionResult();
            var vertexCache = new Dictionary<(int X, int Y, int Z, int Axis), int>();

            var cornerDensities = new float[8];
            var cornerPositions = new Vector3[8];
            var loopIndices = new int[MaxCubeEdges];
            long contourResolutionTicks = 0;
            long vertexWeldingTicks = 0;
            long triangleEmissionTicks = 0;

            Stopwatch activeCellStopwatch = collectTimings ? Stopwatch.StartNew() : null;
            ActiveCellEntry[] activeCells = ActiveCellBuilder.Build(grid);
            if (collectTimings)
            {
                activeCellStopwatch.Stop();
                result.ActiveCellConstructionTime =
                    StopwatchTicksToTimeSpan(activeCellStopwatch.ElapsedTicks);
            }

            for (int i = 0; i < activeCells.Length; i++)
            {
                ActiveCellEntry cell = activeCells[i];
                ActiveCellBuilder.DecodeCellIndex(
                    cell.CellIndex, grid.CellsX, grid.CellsY, out int cx, out int cy, out int cz);

                grid.CopyCellCornerSamples(cx, cy, cz, cornerDensities);
                for (int c = 0; c < 8; c++)
                {
                    cornerDensities[c] = GenerationTolerances.NormalizeSurfaceDensity(cornerDensities[c]);
                }

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
                else
                {
                    loops = CubeContourResolver.ResolveLoops(cornerDensities, cornerPositions);
                }

                foreach (List<CubeContourResolver.LoopVertex> loop in loops)
                {
                    if (ShouldSuppressCoarseLoop(loop, grid)) continue;

                    EmitLoop(
                        grid, loop, cx, cy, cz, vertexCache, result, loopIndices,
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
                min.x = Mathf.Min(min.x, p.x);
                min.y = Mathf.Min(min.y, p.y);
                min.z = Mathf.Min(min.z, p.z);
                max.x = Mathf.Max(max.x, p.x);
                max.y = Mathf.Max(max.y, p.y);
                max.z = Mathf.Max(max.z, p.z);
            }

            Vector3 extent = max - min;
            float maximumExtent = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));
            return maximumExtent <= grid.CellSize * 0.75f;
        }

        private static void EmitLoop(
            DensityGrid grid,
            List<CubeContourResolver.LoopVertex> loop,
            int cx, int cy, int cz,
            Dictionary<(int, int, int, int), int> vertexCache,
            MeshExtractionResult result,
            int[] indices,
            bool collectTimings,
            ref long vertexWeldingTicks,
            ref long triangleEmissionTicks)
        {
            if (loop.Count < 3) return;
            if (loop.Count > indices.Length)
            {
                throw new DomainException(
                    $"Cube contour loop contains {loop.Count} edges; maximum supported is {indices.Length}.");
            }

            for (int i = 0; i < loop.Count; i++)
            {
                if (collectTimings)
                {
                    long weldingStart = Stopwatch.GetTimestamp();
                    indices[i] = ResolveVertexIndex(loop[i], grid, cx, cy, cz, vertexCache, result);
                    vertexWeldingTicks += Stopwatch.GetTimestamp() - weldingStart;
                }
                else
                {
                    indices[i] = ResolveVertexIndex(loop[i], grid, cx, cy, cz, vertexCache, result);
                }
            }

            for (int i = 1; i < loop.Count - 1; i++)
            {
                if (collectTimings)
                {
                    long emissionStart = Stopwatch.GetTimestamp();
                    EmitTriangle(grid, result, indices[0], indices[i], indices[i + 1]);
                    triangleEmissionTicks += Stopwatch.GetTimestamp() - emissionStart;
                }
                else
                {
                    EmitTriangle(grid, result, indices[0], indices[i], indices[i + 1]);
                }
            }
        }

        private static System.TimeSpan StopwatchTicksToTimeSpan(long ticks)
        {
            return System.TimeSpan.FromSeconds((double)ticks / Stopwatch.Frequency);
        }

        private static int ResolveVertexIndex(
            CubeContourResolver.LoopVertex vertex,
            DensityGrid grid,
            int cx, int cy, int cz,
            Dictionary<(int, int, int, int), int> vertexCache,
            MeshExtractionResult result)
        {
            (int A, int B) edgeCorners = CubeTopology.EdgeCorners[vertex.EdgeIndex];
            Vector3Int offsetA = CubeTopology.CornerGridOffsets[edgeCorners.A];
            Vector3Int offsetB = CubeTopology.CornerGridOffsets[edgeCorners.B];

            Vector3Int gridA = new Vector3Int(cx + offsetA.x, cy + offsetA.y, cz + offsetA.z);
            Vector3Int gridB = new Vector3Int(cx + offsetB.x, cy + offsetB.y, cz + offsetB.z);

            Vector3 positionA = grid.CornerPosition(gridA.x, gridA.y, gridA.z);
            Vector3 positionB = grid.CornerPosition(gridB.x, gridB.y, gridB.z);

            if (GenerationTolerances.NormalizeSurfaceDensity(grid.GetSample(gridA.x, gridA.y, gridA.z)) == 0f)
            {
                return ResolveCachedVertex(
                    (gridA.x, gridA.y, gridA.z, -1), positionA, vertexCache, result);
            }

            if (GenerationTolerances.NormalizeSurfaceDensity(grid.GetSample(gridB.x, gridB.y, gridB.z)) == 0f)
            {
                return ResolveCachedVertex(
                    (gridB.x, gridB.y, gridB.z, -1), positionB, vertexCache, result);
            }

            int axis = gridA.x != gridB.x ? 0 : gridA.y != gridB.y ? 1 : 2;
            Vector3Int lower = axis switch
            {
                0 => gridA.x < gridB.x ? gridA : gridB,
                1 => gridA.y < gridB.y ? gridA : gridB,
                _ => gridA.z < gridB.z ? gridA : gridB,
            };

            var key = (lower.x, lower.y, lower.z, axis);
            return ResolveCachedVertex(key, vertex.Position, vertexCache, result);
        }

        private static int ResolveCachedVertex(
            (int X, int Y, int Z, int Axis) key,
            Vector3 position,
            Dictionary<(int X, int Y, int Z, int Axis), int> vertexCache,
            MeshExtractionResult result)
        {
            if (vertexCache.TryGetValue(key, out int existingIndex)) return existingIndex;

            int newIndex = result.Positions.Count;
            result.Positions.Add(position);
            vertexCache[key] = newIndex;
            return newIndex;
        }

        private static void EmitTriangle(DensityGrid grid, MeshExtractionResult result, int i0, int i1, int i2)
        {
            Vector3 p0 = result.Positions[i0];
            Vector3 p1 = result.Positions[i1];
            Vector3 p2 = result.Positions[i2];

            Vector3 faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
            if (faceNormal.sqrMagnitude < 1e-12f) return;

            Vector3 centroid = (p0 + p1 + p2) / 3f;
            result.GradientEvaluationCount++;

            Vector3 gradient;
            if (!grid.TryEstimateGradient(centroid, out gradient))
            {
                gradient = grid.EstimateGradient(centroid);
            }

            bool correctlyWound = Vector3.Dot(faceNormal, gradient) >= 0f;
            if (correctlyWound)
            {
                result.Triangles.Add(i0);
                result.Triangles.Add(i1);
                result.Triangles.Add(i2);
            }
            else
            {
                result.Triangles.Add(i0);
                result.Triangles.Add(i2);
                result.Triangles.Add(i1);
            }
        }

    }
}
