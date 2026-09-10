# CreatureCreator — Audit Round: SDF/Marching-Cubes Core

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `28240f0`
(unchanged tip). Context noted for this and future rounds: work on this
branch includes at least one external agent operating with git access only
— no MemorySmith MCP tools, no Unity test/build execution. That materially
explains two patterns already flagged (task-JSON hand-edits producing ID
collisions when the proper tool isn't reachable; compile-breaks landing
because "0 compile errors" claims can't always be backed by an actual
build in that operating mode). Worth keeping in mind when weighing how much
independent verification any given commit's stated validation deserves —
not a reason to distrust the code itself, just a reason my own
read-the-source verification stays the deciding check rather than a
convenience double-check.

---

## Scope this round

Read, for the first time in this whole review, the core SDF/Marching-Cubes
geometry pipeline: `SmoothMinMath.cs`, `AsymptoticDecider.cs`, the ambiguous-
face handling in `CubeContourResolver.cs`, `GenerationTolerances.cs`, and
the degenerate-handling paths in `BodyEditSolver.cs`. This is the most
consequential unexplored code left in the project (it's the literal
mesh-generation math), and given how many real bugs I found via source
reading in Appearance/Morphology/Generation over the last several rounds, I
expected to find something here.

## Result: no new defects found — worth reporting honestly rather than manufacturing one

- **`SmoothMinMath.SmoothMin`** is the standard, correct polynomial
  smooth-min formula, with a properly-handled `blendRadius <= 0` fallback
  to hard `min` (avoiding the division-by-zero a naive implementation would
  hit) — and the fallback is explicitly justified against a real authored
  case (`SmoothBlendRadius == 0` is valid per `ShapeDefinition.HasValidParameters`,
  so it has to union cleanly, not degrade).
- **`AsymptoticDecider`** implements the genuine Nielson–Hamann asymptotic
  decider for resolving ambiguous Marching-Cubes faces — I checked the
  derivation in the doc comment against the actual formula and it's
  correct (the saddle-point value and sign test match the standard
  bilinear-interpolant derivation). The degenerate-denominator fallback is
  handled with a specific, correct justification for why the fallback
  choice doesn't matter (both cubes sharing a face compute identical
  inputs from identical shared corners, so a consistent fallback still
  produces a watertight mesh — the actual property that matters).
- **`CubeContourResolver`**'s one call site gates
  `AsymptoticDecider.DiagonalConnectsThroughMiddle` on `crossedEdges.Count
  == 4`, not a literal call to `IsFaceAmbiguous` — I checked whether this
  violates the decider's own "callers must check `IsFaceAmbiguous` first"
  contract note. It doesn't: a 4-edge-face has exactly one crossed edge per
  side if and only if it's the ambiguous checkerboard case, so the
  precondition is genuinely equivalent, just expressed differently. Not a
  bug, arguably worth a one-word doc clarification ("...or an equivalent
  precondition") but not worth a task on its own.
- **`GenerationTolerances.cs`** — checked specifically for the units-
  confusion class of bug that `ab7d3a0` just fixed elsewhere
  (linear-vs-squared threshold mismatch). Every constant here is
  consistently linear-scale (`1e-3f`-class values with names like
  `...SegmentLength`/`...Tolerance`, no squared variants), and I grepped
  the wider runtime/editor tree for the same shape of mismatch
  (`.magnitude` compared against anything named `Sqr`, and the reverse) —
  found no other instance. `ab7d3a0` looks like a genuine one-off, now
  correctly fixed and tested, not a symptom of a wider pattern.
- **`BodyEditSolver.cs`**'s degenerate-segment handling (`RelaxCompression`)
  correctly compares a linear `Epsilon = 1e-6f` against `.magnitude`
  (linear) throughout — no units mismatch here either.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| `SmoothMinMath`/`AsymptoticDecider` are mathematically correct as implemented | Confirmed (derivation-level review, not just pattern-matching) |
| `CubeContourResolver`'s ambiguous-face gating is a valid equivalent precondition, not a contract violation | Confirmed |
| The linear/squared units-confusion pattern from `ab7d3a0` does not recur elsewhere in current source | Checked via targeted grep across the full runtime/editor tree; absence of evidence, not a formal proof |
| No new defects in this round's scope | Confirmed for the files read; this is a narrower claim than "the SDF pipeline has no bugs" — only these specific files were read in depth |
