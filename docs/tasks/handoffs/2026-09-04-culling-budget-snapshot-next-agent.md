# Next-agent handoff — fast-field correctness and snapshot authority

**Date:** 2026-09-04
**Branch:** `agent/2026-09-04-culling-budget-snapshot-hardening`
**Base:** `48ffccd97fb89d2aa27032a78fd6718daeff8fc3`
**Latest implementation/handoff commit:** `af6f02a73ea61bddbcaa53ac9f9ee63e04e652d2`

## Current state

This round reviewed the latest implementation commits and the prior agent's final
response. The prior response's “no Runtime defect” conclusion was superseded by a
source-level review of the fast SDF contract: the repository retained `Cullable` safety
metadata but the evaluator and root sampling shortcut did not consume it.

CC-089 is now marked Done in the Markdown task surface. Its latest implementation
uses `TryResolve` for routine malformed body/limb envelope cases and detaches the
hierarchy index's parts view. No second graph-mechanics task is needed.

CC-091 remains In Progress. Its product/architecture decisions are now explicit:

- `MaxVoxelBudget` is a **corner-sample allocation budget**, because
  `DensityGrid.SamplePortable` actually allocates `(cellsX+1)*(cellsY+1)*(cellsZ+1)`
  float samples. `EstimateVoxelCount` is a cell-count diagnostic only.
- The authoritative generation snapshot must represent the **same canonicalized input**
  whose canonical representation supplies `RevisionId`. Generation should canonicalize a
  detached copy before resolving; the authored `CreatureDefinition` must not be mutated.
- Keep `DefinitionValidator.Validate(...)` as the single public validation façade. If
  validation context is later factored, use one concrete context/index passed through the
  existing checks; do not create a generic validator/service framework.
- Keep both authoring-local and resolved-world envelope diagnostics. They answer different
  questions: local authoring constraints versus actual generation-domain reachability.
  They must have distinct validation codes/messages and must not silently collapse into
  one ambiguous “out of bounds” diagnostic.
- No second snapshot task may be created.

## Work already implemented on this branch

### CC-099 / fast-field correctness

`SdfProgramEvaluator` now requires both valid bounds and `operation.Cullable` before
returning `+inf` from an AABB skip. The same contract is used by subtree evaluation.

`SdfSamplingJob` now uses `RootCanCull`, derived from the root operation's `Cullable`
flag and valid AABB, so approximate ellipsoid roots cannot be skipped by the region
shortcut.

`DensityGrid.EstimateGradient` now uses finite-aware differences:

- centered difference when both neighbors are finite;
- one-sided difference when exactly one neighbor is finite;
- zero when the center or both neighbors are invalid.

This prevents `+inf - +inf` / finite-`+inf` arithmetic from producing non-finite
extraction gradients.

Regression tests were added for:

- an elongated ellipsoid outside its AABB with a finite approximate field value;
- a culling-boundary sphere gradient using one-sided finite differences.

## Important validation state

The branch has **not** been Unity-validated after the CC-099 edits in this session. Do
not claim a test pass from source inspection or from the earlier base-commit validation.
The next agent MUST run focused SDF tests and then the full runtime/editor suites.

## Next implementation wave

1. **Validate CC-099 first.** If tests fail, fix correctness before any optimization.
2. Add an explicit root-region parity regression proving an ellipsoid root is never
   early-exited, not just the scalar evaluator parity test.
3. Add a grid/extraction regression that proves no gradient returned from a fast-culling
   grid is NaN/Infinity at active-cell boundaries.
4. Then complete CC-091's authority boundary:
   - canonicalize a detached input copy before snapshot resolution;
   - make `RevisionId` derive from that exact canonical snapshot input;
   - ensure downstream generation stages consume snapshot data only;
   - replace internal `(CreaturePart, SdfProgram)` correspondence with resolved part
     correspondence (`ResolvedPartSnapshot` + program or equivalent concrete value type);
   - expose read-only views for compiled program and density-grid native buffers;
   - keep raw-definition compatibility overloads only at the outer boundary.
5. Audit `SdfProgramBuilder` for duplicate primitive emission between whole-creature and
   individual-part paths and consolidate mechanically identical code into small concrete
   helpers. Do not introduce generic compiler/service interfaces.
6. Align the editor budget display with the decision: show corner samples against
   `MaxVoxelBudget`, optionally showing cell count separately as a diagnostic.
7. Re-run deterministic generation, topology, appearance, mesh-placement, and scheduler
   parity after the authority changes.

## Do not do

- Do not create another snapshot task.
- Do not reopen CC-089 graph mechanics unless a new defect is proven.
- Do not remove `Cullable`; it is the correct proof-bearing safety gate.
- Do not use AABB bounds alone as a culling proof.
- Do not canonicalize/mutate the editor's authoritative object in place.
- Do not turn local and resolved bounds validation into one ambiguous rule.
- Do not add a generic abstraction framework.
- Do not claim PlayMode validation from build-only or source-only evidence.

## Expected implementation shape

```text
CreatureDefinition (authoring)
    -> validate
    -> canonical detached input
    -> one ResolvedCreatureSnapshot
    -> field/program generation
    -> sampling/extraction
    -> appearance
    -> mesh-asset placement
    -> assembly
```

All downstream stages should consume resolved values or generated artifacts. A
compatibility method may still accept `CreatureDefinition`, but it should immediately
construct the canonical/resolved boundary and delegate to the same implementation.

## Validation commands/evidence required

- Unity focused SDF tests, including the new `SdfProgramBuilderTests` regressions.
- Full `ProceduralCreature.Tests.Runtime` PlayMode suite.
- Full `ProceduralCreature.Tests.Editor` EditMode suite.
- `dotnet build` for affected runtime/tests with `--no-restore`.
- `git diff --check`.
- Record exact Unity version, test counts, failures/skips, and any environment limitation.

## Prompt for the next agent

Review this handoff and the current branch, then continue implementation without asking
for clarification. Start by validating CC-099 on the current source. Treat failures as
real until disproven. Once CC-099 is green, implement the next smallest reversible CC-091
authority slice: canonicalize a detached generation input before snapshot resolution,
make the snapshot/revision correspond to that exact canonical input, and close any raw-DNA
bypass discovered in downstream stages. Preserve the one-snapshot rule and existing public
compatibility behavior. Keep validation as one façade and retain distinct local-vs-resolved
bounds diagnostics. Use existing task ownership; create no duplicate CC ticket beyond the
already-created CC-099 correctness task. Update the task/handoff evidence, commit each
coherent slice, and leave the branch buildable/testable.
