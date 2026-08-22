namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Creature-level symmetry setting (design doc §6, delta-audit item #2).
    ///
    /// DECISION: symmetry is generation-time-only. A part authored with
    /// CreaturePart.MirrorAcrossSymmetryPlane = true exists as exactly ONE entry in
    /// CreatureDefinition.Parts; the mirrored counterpart is produced by the SDF
    /// compiler (Phase 2) and by skeleton inference (Phase 6) as derived state, never
    /// written back into DNA. This keeps stable-ID rules (§2.2) simple — there is no
    /// phantom mirrored part that needs its own ID lifecycle — and means editing the
    /// authored half in the viewport is definitionally "editing the whole symmetric
    /// pair," since the mirror is recomputed from the single source part every
    /// regeneration rather than requiring a separate synchronized edit.
    /// </summary>
    public enum SymmetryMode
    {
        None = 0,
        MirrorAcrossXAxis = 1,
    }
}
