namespace ProceduralCreature.Morphology.Extraction
{
    /// <summary>
    /// One active (mixed-sign) cell of a sampled DensityGrid. Carries the stable
    /// global linear cell index and the 8-bit sign case used to classify it.
    ///
    /// Case bit c is set when corner c's epsilon-normalized sample is on or
    /// outside the surface (>= 0). Homogeneous cells (case 0 or 255) are not
    /// retained by the builder, so every stored entry is a surface-crossing cell.
    /// The cell index is a deterministic increasing linear index over the grid
    /// (z-major, then y, then x), so iterating the array in order is stable and
    /// independent of any collection enumeration order.
    /// </summary>
    public readonly struct ActiveCellEntry
    {
        public readonly int CellIndex;
        public readonly byte CaseIndex;

        public ActiveCellEntry(int cellIndex, byte caseIndex)
        {
            CellIndex = cellIndex;
            CaseIndex = caseIndex;
        }
    }
}
