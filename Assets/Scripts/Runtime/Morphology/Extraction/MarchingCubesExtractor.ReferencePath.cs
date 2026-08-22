using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Morphology.Extraction
{
    public static partial class MarchingCubesExtractor
    {
        /// <summary>
        /// Reference extraction path for Slice 1 of CC-008: the pre-change dense
        /// full-volume classification loop. It visits every cell and resolves
        /// contours inline, exactly as the original extractor did before
        /// active-cell iteration.
        ///
        /// It exists ONLY as a parity oracle so tests can prove the active-cell
        /// path produces identical geometry (same mixed-cell count, vertices,
        /// triangles, bounds, and topology report) and identical ordering. It is
        /// intentionally not used in production. Delete this method together with
        /// the parity tests after Slice 2 replaces dictionary welding with direct
        /// edge ownership; at that point the active-cell path becomes the
        /// baseline.
        /// </summary>
        internal static MeshExtractionResult ExtractLegacy(DensityGrid grid)
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
                    continue; // cube entirely inside or entirely outside — no surface here
                }

                // Positions are consumed only by mixed cells. Avoid constructing
                // eight Vector3 values for every empty cell in a large preview grid.
                for (int c = 0; c < 8; c++)
                {
                    Vector3Int offset = CubeTopology.CornerGridOffsets[c];
                    cornerPositions[c] = grid.CornerPosition(cx + offset.x, cy + offset.y, cz + offset.z);
                }

                result.MixedCellCount++;
                result.ContourResolutionCallCount++;

                List<List<CubeContourResolver.LoopVertex>> loops =
                    CubeContourResolver.ResolveLoops(cornerDensities, cornerPositions);

                long unusedWeldingTicks = 0;
                long unusedEmissionTicks = 0;
                foreach (List<CubeContourResolver.LoopVertex> loop in loops)
                {
                    EmitLoop(grid, loop, cx, cy, cz, vertexCache, result,
                        collectTimings: false, ref unusedWeldingTicks, ref unusedEmissionTicks);
                }
            }

            return result;
        }
    }
}
