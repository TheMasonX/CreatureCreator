using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ProceduralCreature.Common;

namespace ProceduralCreature.Morphology.Extraction
{
    /// <summary>
    /// Builds the compact active-cell list for a sampled DensityGrid. This is the
    /// "classify once after dense sampling" pass: every cell is visited exactly
    /// once, its eight corners are epsilon-normalized with the same surface
    /// tolerance the extractor uses, and a mixed-sign cell is retained as an
    /// <see cref="ActiveCellEntry"/>. Cells with case 0 (all corners inside) or
    /// 255 (all corners outside) are dropped.
    ///
    /// The classification runs in a Burst-compiled, single-threaded
    /// <see cref="ActiveCellScanJob"/> over the grid's native sample buffer. It
    /// is deliberately sequential so the output stays ordered by increasing
    /// global cell index, which matches the extractor's deterministic iteration
    /// order and the reference extractor's z/y/x traversal order, so vertex and
    /// triangle ordering are preserved. Dense sampling itself is untouched; this
    /// only classifies the already-sampled grid.
    /// </summary>
    public static class ActiveCellBuilder
    {
        public static ActiveCellEntry[] Build(DensityGrid grid)
        {
            if (grid == null) throw new DomainException("grid must not be null.");

            int cellCount = grid.CellsX * grid.CellsY * grid.CellsZ;
            // Worst case every cell is active; the job writes only the active
            // prefix and reports its length. Transient TempJob buffers.
            var output = new NativeArray<ActiveCellEntry>(cellCount, Allocator.TempJob);
            var outputCount = new NativeArray<int>(1, Allocator.TempJob);
            try
            {
                var job = new ActiveCellScanJob
                {
                    Samples = grid.Samples,
                    CellsX = grid.CellsX,
                    CellsY = grid.CellsY,
                    CellsZ = grid.CellsZ,
                    SurfaceEpsilon = GenerationTolerances.ScalarComparisonEpsilon,
                    Output = output,
                    OutputCount = outputCount,
                };
                job.Run();

                int count = outputCount[0];
                var active = new ActiveCellEntry[count];
                for (int i = 0; i < count; i++)
                {
                    active[i] = output[i];
                }
                return active;
            }
            finally
            {
                output.Dispose();
                outputCount.Dispose();
            }
        }

        /// <summary>
        /// Computes the 8-bit sign case for a cell's corner samples using the same
        /// surface-epsilon normalization as the extractor, so the retained set
        /// matches the reference mixed-cell classification exactly. Bit c is set
        /// when normalized corner c is >= 0 (on or outside the surface).
        /// </summary>
        public static byte ClassifyCaseIndex(float[] cornerDensities)
        {
            if (cornerDensities == null || cornerDensities.Length != 8)
            {
                throw new DomainException("cornerDensities must have exactly 8 entries.");
            }

            byte caseIndex = 0;
            for (int c = 0; c < 8; c++)
            {
                if (GenerationTolerances.NormalizeSurfaceDensity(cornerDensities[c]) >= 0f)
                {
                    caseIndex |= (byte)(1 << c);
                }
            }
            return caseIndex;
        }

        /// <summary>
        /// Decodes a stable linear cell index back into grid coordinates using the
        /// same z-major layout the builder uses to encode it:
        /// cellIndex = (cz * CellsY + cy) * CellsX + cx.
        /// </summary>
        public static void DecodeCellIndex(int cellIndex, int cellsX, int cellsY, out int x, out int y, out int z)
        {
            x = cellIndex % cellsX;
            y = (cellIndex / cellsX) % cellsY;
            z = cellIndex / (cellsX * cellsY);
        }
    }

    /// <summary>
    /// Burst-compiled single-threaded scan over the native corner samples.
    /// Iterates cells in the same z/y/x order the managed builder used and
    /// appends active cells to <see cref="Output"/> in increasing global cell
    /// index, so the returned list is bit-identical to the old managed pass.
    /// Replicates <see cref="GenerationTolerances.NormalizeSurfaceDensity"/>
    /// inline with the same epsilon semantics (+inf stays +inf, NaN stays NaN).
    /// </summary>
    [BurstCompile]
    public struct ActiveCellScanJob : IJob
    {
        [ReadOnly] public NativeArray<float> Samples;
        public int CellsX;
        public int CellsY;
        public int CellsZ;
        public float SurfaceEpsilon;
        [WriteOnly] public NativeArray<ActiveCellEntry> Output;
        [WriteOnly] public NativeArray<int> OutputCount;

        public void Execute()
        {
            int count = 0;
            int cornersX = CellsX + 1;
            int cornersY = CellsY + 1;
            int rowStride = cornersX;
            int sliceStride = cornersX * cornersY;

            for (int cz = 0; cz < CellsZ; cz++)
            for (int cy = 0; cy < CellsY; cy++)
            for (int cx = 0; cx < CellsX; cx++)
            {
                int cellIndex = (cz * CellsY + cy) * CellsX + cx;
                int baseIndex = (cz * cornersY + cy) * cornersX + cx;

                float c0 = Normalize(Samples[baseIndex]);
                float c1 = Normalize(Samples[baseIndex + 1]);
                float c2 = Normalize(Samples[baseIndex + rowStride]);
                float c3 = Normalize(Samples[baseIndex + rowStride + 1]);
                float c4 = Normalize(Samples[baseIndex + sliceStride]);
                float c5 = Normalize(Samples[baseIndex + sliceStride + 1]);
                float c6 = Normalize(Samples[baseIndex + sliceStride + rowStride]);
                float c7 = Normalize(Samples[baseIndex + sliceStride + rowStride + 1]);

                byte caseIndex = 0;
                if (c0 >= 0f) caseIndex |= 1;
                if (c1 >= 0f) caseIndex |= 2;
                if (c2 >= 0f) caseIndex |= 4;
                if (c3 >= 0f) caseIndex |= 8;
                if (c4 >= 0f) caseIndex |= 16;
                if (c5 >= 0f) caseIndex |= 32;
                if (c6 >= 0f) caseIndex |= 64;
                if (c7 >= 0f) caseIndex |= 128;

                if (caseIndex == 0 || caseIndex == 255) continue;

                Output[count] = new ActiveCellEntry(cellIndex, caseIndex);
                count++;
            }

            OutputCount[0] = count;
        }

        private float Normalize(float density)
        {
            return math.abs(density) <= SurfaceEpsilon ? 0f : density;
        }
    }
}
