using System.Collections.Generic;
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
    /// The resulting array is ordered by increasing global cell index, which
    /// matches the extractor's deterministic iteration order and the reference
    /// extractor's z/y/x traversal order, so vertex and triangle ordering are
    /// preserved. Dense sampling itself is untouched; this only classifies the
    /// already-sampled grid.
    /// </summary>
    public static class ActiveCellBuilder
    {
        public static ActiveCellEntry[] Build(DensityGrid grid)
        {
            if (grid == null) throw new DomainException("grid must not be null.");

            var active = new List<ActiveCellEntry>(64);
            var corners = new float[8];

            for (int cz = 0; cz < grid.CellsZ; cz++)
            for (int cy = 0; cy < grid.CellsY; cy++)
            for (int cx = 0; cx < grid.CellsX; cx++)
            {
                grid.CopyCellCornerSamples(cx, cy, cz, corners);
                byte caseIndex = ClassifyCaseIndex(corners);
                if (caseIndex == 0 || caseIndex == 255) continue;

                int cellIndex = (cz * grid.CellsY + cy) * grid.CellsX + cx;
                active.Add(new ActiveCellEntry(cellIndex, caseIndex));
            }

            return active.ToArray();
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
}
