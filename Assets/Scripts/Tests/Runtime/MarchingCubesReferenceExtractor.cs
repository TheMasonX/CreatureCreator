using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology.Extraction;

namespace ProceduralCreature.Tests.Runtime
{
    internal static class MarchingCubesReferenceExtractor
    {
        internal static MeshExtractionResult Extract(DensityGrid grid)
        {
            if (grid == null) throw new DomainException("grid must not be null.");

            var result = new MeshExtractionResult();
            var vertexCache = new Dictionary<(int X, int Y, int Z, int Axis), int>();
            var cornerDensities = new float[8];
            var cornerPositions = new Vector3[8];
            float surfaceEpsilon = GenerationTolerances.ScalarComparisonEpsilon;

            for (int cz = 0; cz < grid.CellsZ; cz++)
            for (int cy = 0; cy < grid.CellsY; cy++)
            for (int cx = 0; cx < grid.CellsX; cx++)
            {
                bool anyInside = false;
                bool anyOutside = false;
                grid.CopyCellCornerSamples(cx, cy, cz, cornerDensities);

                for (int c = 0; c < cornerDensities.Length; c++)
                {
                    float density = cornerDensities[c];
                    if (density >= -surfaceEpsilon && density <= surfaceEpsilon)
                    {
                        density = 0f;
                    }

                    cornerDensities[c] = density;
                    if (density >= 0f) anyOutside = true;
                    else anyInside = true;
                }

                if (!anyInside || !anyOutside)
                {
                    continue;
                }

                for (int c = 0; c < 8; c++)
                {
                    Vector3Int offset = CubeTopology.CornerGridOffsets[c];
                    cornerPositions[c] = grid.CornerPosition(cx + offset.x, cy + offset.y, cz + offset.z);
                }

                result.MixedCellCount++;
                result.ContourResolutionCallCount++;
                List<List<CubeContourResolver.LoopVertex>> loops =
                    CubeContourResolver.ResolveLoops(cornerDensities, cornerPositions);

                foreach (List<CubeContourResolver.LoopVertex> loop in loops)
                {
                    EmitLoop(grid, loop, cx, cy, cz, vertexCache, result);
                }
            }

            return result;
        }

        private static void EmitLoop(
            DensityGrid grid,
            List<CubeContourResolver.LoopVertex> loop,
            int cx,
            int cy,
            int cz,
            Dictionary<(int X, int Y, int Z, int Axis), int> vertexCache,
            MeshExtractionResult result)
        {
            if (loop.Count < 3) return;

            var indices = new int[loop.Count];
            for (int i = 0; i < loop.Count; i++)
            {
                indices[i] = ResolveVertexIndex(loop[i], grid, cx, cy, cz, vertexCache, result);
            }

            for (int i = 1; i < loop.Count - 1; i++)
            {
                EmitTriangle(grid, result, indices[0], indices[i], indices[i + 1]);
            }
        }

        private static int ResolveVertexIndex(
            CubeContourResolver.LoopVertex vertex,
            DensityGrid grid,
            int cx,
            int cy,
            int cz,
            Dictionary<(int X, int Y, int Z, int Axis), int> vertexCache,
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
                return ResolveCachedVertex((gridA.x, gridA.y, gridA.z, -1), positionA, vertexCache, result);
            }

            if (GenerationTolerances.NormalizeSurfaceDensity(grid.GetSample(gridB.x, gridB.y, gridB.z)) == 0f)
            {
                return ResolveCachedVertex((gridB.x, gridB.y, gridB.z, -1), positionB, vertexCache, result);
            }

            int axis = gridA.x != gridB.x ? 0 : gridA.y != gridB.y ? 1 : 2;
            Vector3Int lower = axis switch
            {
                0 => gridA.x < gridB.x ? gridA : gridB,
                1 => gridA.y < gridB.y ? gridA : gridB,
                _ => gridA.z < gridB.z ? gridA : gridB,
            };

            return ResolveCachedVertex(
                (lower.x, lower.y, lower.z, axis), vertex.Position, vertexCache, result);
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
                return;
            }

            Vector3 centroid = (p0 + p1 + p2) / 3f;
            Vector3 gradient = grid.EstimateGradient(centroid);
            result.GradientEvaluationCount++;

            if (Vector3.Dot(faceNormal, gradient) >= 0f)
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
