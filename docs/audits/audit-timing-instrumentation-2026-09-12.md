# CreatureCreator — Audit Round: Per-Stage Timing Instrumentation Lands

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `85173ea`
(up from `785f204` — 8 commits).

---

## Directly answers the open question from my performance audit

`5a8466a`/`188b90e`/`c220653` add explicit `Time(diagnostics,
GenerationStage.X, ...)` wrapping around every stage of
`CreatureMeshGenerator`'s pipeline: `SdfCompile`, `FieldSampling`,
`MeshExtraction`, `MeshValidation`, `AppearanceBake`,
`SkeletonInference`, `InfluenceDomainResolution`. This is exactly
recommendation #1 from `audit-sdf-performance-2026-09-09.md` — I'd flagged
that my architectural read (extraction likely dominates for complex
creatures) needed actual measurement before prioritizing the parallelization
work, and this closes that gap. Once this runs against a real benchmark
creature, the question "does stage 2 or stage 3 actually dominate" has a
real answer instead of my reasoned guess.

**One refinement worth making before relying on the numbers:** `MeshExtraction`
is currently one combined timing bucket. In my original analysis, "stage 2"
(the Burst-but-single-threaded active-cell scan, `ActiveCellBuilder`) and
"stage 3" (the fully unparallelized managed-C# triangle extraction/vertex
welding, `MarchingCubesExtractor`) are architecturally very different —
one is Burst-compiled and just single-threaded, the other has no Burst/Jobs
at all. My recommendation was to parallelize whichever one actually
dominates, and those two have different, differently-costly fixes. A single
combined `MeshExtraction` timing won't distinguish which of the two is the
actual bottleneck — worth splitting into two sub-stage timings
(`ActiveCellScan` / `TriangleExtraction`, or similar) if this
instrumentation is meant to inform that specific decision, since the
current granularity would only tell you "extraction as a whole costs X,"
not which half of it to fix first.

## Small, real cleanups in the same window

- `7f69c6e` removes a genuinely dead helper method from
  `CreatureRuntimePreview.cs` (11 lines, no remaining callers) — consistent
  with the "streamline" theme from a few rounds back.
- `eab742e` makes `MorphologyInfluenceRadiusBridge`'s convenience path reuse
  an already-resolved snapshot rather than re-resolving it — a small,
  correctly-scoped dedup, not a new pattern worth flagging on its own.

## Standing items, still unchanged

- Gradient regression in `DensityGrid.TryEstimateGradient` — `dw`'s third
  term still `(c011 - c001)`, should be `(c011 - c010)`. Sixth consecutive
  round unfixed.
- Task-ID collisions — still exactly 14, same list. `tsk-0156` still
  malformed. The normalizer's repair pass (confirmed unblocked several
  rounds ago) still hasn't been run.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| New timing instrumentation directly answers my performance audit's open measurement question | Confirmed (read the instrumentation, matches the exact stages I flagged) |
| `MeshExtraction`'s combined timing won't distinguish active-cell-scan cost from triangle-extraction cost | Confirmed (read the current stage boundaries) |
| Two small cleanups are real and correctly scoped | Confirmed |
| Gradient regression and task-ID collisions remain open | Confirmed (direct recheck) |
