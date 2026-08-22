using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology.Sdf;

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
        public static MeshExtractionResult Extract(ISdfNode node, DensityGrid grid)
        {
            return Extract(node, grid, collectTimings: false);
        }

        public static MeshExtractionResult Extract(ISdfNode node, DensityGrid grid, bool collectTimings)
        {
            if (node == null) throw new DomainException("node must not be null.");
            if (grid == null) throw new DomainException("grid must not be null.");

            var result = new MeshExtractionResult();
            var vertexCache = new Dictionary<(int X, int Y, int Z, int Axis), int>();

            var cornerDensities = new float[8];
            var cornerPositions = new Vector3[8];
            long contourResolutionTicks = 0;
            long vertexWeldingTicks = 0;
            long triangleEmissionTicks = 0;

            // One dense scan classifies every cell and retains only the mixed-sign
            // cells as an ordered active-cell list. The extractor then iterates
            // that list instead of re-classifying the whole volume, so empty
            // volume is never re-traversed during contour resolution. Dense
            // sampling itself is unchanged; this is purely the classification pass.
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

                // Active cells are mixed-sign by construction, so a sign
                // classification here is redundant; only the surface-epsilon
                // normalization that feeds contour resolution is reapplied.
                grid.CopyCellCornerSamples(cx, cy, cz, cornerDensities);
                for (int c = 0; c < 8; c++)
                {
                    cornerDensities[c] = GenerationTolerances.NormalizeSurfaceDensity(cornerDensities[c]);
                }

                // Positions are consumed only by mixed cells. Avoiding eight
                // Vector3 constructions per cell is what kept the old dense loop
                // cheap for empty volume; active cells are few, but the rule still
                // keeps this tight.
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
                    EmitLoop(
                        grid, loop, cx, cy, cz, vertexCache, result,
                        collectTimings, ref vertexWeldingTicks, ref triangleEmissionTicks);
                }
            }

            result.ContourResolutionTime = StopwatchTicksToTimeSpan(contourResolutionTicks);
            result.VertexWeldingTime = StopwatchTicksToTimeSpan(vertexWeldingTicks);
            result.TriangleEmissionTime = StopwatchTicksToTimeSpan(triangleEmissionTicks);

            return result;
        }

        private static void EmitLoop(
            DensityGrid grid,
            List<CubeContourResolver.LoopVertex> loop,
            int cx, int cy, int cz,
            Dictionary<(int, int, int, int), int> vertexCache,
            MeshExtractionResult result,
            bool collectTimings,
            ref long vertexWeldingTicks,
            ref long triangleEmissionTicks)
        {
            if (loop.Count < 3) return; // degenerate — shouldn't occur for valid input, but cheap to guard

            var indices = new int[loop.Count];
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

            // Fan triangulation from the first vertex. Safe and non-self-intersecting
            // for the star-shaped loops that arise from a single cube's surface
            // crossing at the grid resolutions this system targets; documented as a
            // known simplification rather than a proven-general polygon
            // triangulation (ear clipping) — revisit if golden-fixture testing at
            // very coarse resolution surfaces a self-intersecting fan.
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

            // An exact zero-valued grid corner is represented as an intersection
            // on every incident crossed edge. Use the normalized endpoint value
            // rather than comparing interpolated positions, because near-zero
            // values can produce slightly different floating-point positions on
            // edges incident to the same grid corner.
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
            if (vertexCache.TryGetValue(key, out int existingIndex))
            {
                return existingIndex;
            }

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
            if (faceNormal.sqrMagnitude < 1e-12f)
            {
                return; // degenerate triangle (zero area) — skip rather than emit garbage
            }

            Vector3 centroid = (p0 + p1 + p2) / 3f;
            Vector3 gradient = grid.EstimateGradient(centroid);
            result.GradientEvaluationCount++;

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
