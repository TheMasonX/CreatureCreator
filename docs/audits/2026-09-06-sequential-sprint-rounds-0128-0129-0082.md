# Sequential Sprint Log — Rounds 3 (TSK-0128 / TSK-0129-repro / TSK-0082)

Sprint fixed point: `e1b078a` (clean origin/main head). User-confirmed commit
policy: commit + push per round. User-confirmed round set: R1 = TSK-0128 (full),
R2 = TSK-0129 bounded repro/root-cause/regression-fixture slice, R3 = TSK-0082.

## Context / round selection

Freshest Ready, user-mandated work at the fixed point: TSK-0128 (editor body-drag
re-spaces the whole spline) and TSK-0129 (coarse-resolution topology for thin
sub-cell features). TSK-0082 (split `DuplicateBodySampleId` validation) is a
small, independent runtime validator slice chosen for round 3. Round 2 is scoped
as the bounded reproduction + root-cause + regression-fixture slice of TSK-0129;
a large fix approach still needs user direction and is NOT attempted this sprint.

## Rounds

| Round | Task | Title | Status | Commit | Validation | Evidence confidence |
| ----- | ---- | ----- | ------ | ------ | ---------- | ------------------- |
| 1 | TSK-0128 | Localize body-drag commit re-space to the free tail | Done | `e88ba5d` | dotnet Editor/Tests.Editor 0/0 (re-run); Unity EditMode 15/15 + 131/131 (report-trusted) | static independently re-run; Unity report-trusted |
| 2 | TSK-0129 | Coarse-VPU thin-feature topology repro + regression fixture | Ready (repro done; fix pending) | `f0566eb` | dotnet Tests.Runtime 0/0; Unity PlayMode 2/2 + 526/526 (report-trusted) | report-trusted |
| 3 | TSK-0082 | Split DuplicateBodySampleId validation (dup vs out-of-order) | Done | `ef5401c` | dotnet Runtime + Tests.Runtime 0/0 (re-run); Unity PlayMode validator 2/2 + Runtime 526/526; EditMode 131/131 (report-trusted) | static independently re-run; Unity report-trusted |

## Round log

### Round 1 — TSK-0128 (Done, `e88ba5d`)
- `BodySplineAuthoring.SpaceFreeTailEvenly` replaces the whole-spline `SpaceEvenly` in `CommitBodyDrag`; head/torso fixed, committed spline stays even.
- Review: read production + test diffs; reuses `ArcCoordinates`/`WalkEvenChords`; ownership preserved; tests encode the STRICT mandate.
- Evidence confidence: static independently re-run (dotnet 0/0); Unity EditMode 131/131 report-trusted (129→131 consistent).

### Round 2 — TSK-0129 repro slice (test-only, `f0566eb`; task Ready — fix pending)
- Minimal body+thin-finger (r0.075) fixture torn at VPU 5 (8 boundary, 0 non-manifold), watertight at VPU 16; free thin finger dropped at VPU 10, watertight at VPU 32. Winding (W-02/W-03 on main) ruled out; root cause = uniform-grid under-sampling.
- `CoarseThinFeatureTopologyTests` characterization gate (test-only) stays green; future robust-coarse fix must update to watertight/present.
- Review: read the new test file; uses only existing APIs; no production change, no benchmark. Evidence confidence report-trusted.

### Round 3 — TSK-0082 (Done, `ef5401c`)
- Added `ValidationCode.OutOfOrderBodySampleId`; `ValidateBody` maps a reused Id to `DuplicateBodySampleId` and a unique-but-non-monotonic list to `OutOfOrderBodySampleId` (else-if mutually exclusive; report-only).
- Review: read production + test diffs; actual file formatting verified clean (a merged comment/code line in the raw diff was a terminal display artifact). Static independently re-run (dotnet 0/0).

## Sprint summary
All three rounds committed and pushed: `e88ba5d` (TSK-0128 Done), `f0566eb` (TSK-0129 repro slice; full task Ready), `ef5401c` (TSK-0082 Done). Canonical MemorySmith task evidence updated on each; no `Data/Tasks/*.json` hand-edited. Unrelated pre-existing + concurrent animation-workstream changes (task records TSK-0010/0011/0073/0077/0118, new TSK-0130-0134, Assets/Creatures/dinus_uprightus.json, animation audit docs) were left untouched.

## Follow-ups
- TSK-0128: optional manual Unity viewport drag on `temp-bodysegmentmovebug` for visual confidence.
- TSK-0129: full robust-coarse fix still needs user direction on resolution-vs-speed; characterization gate now exists.
- TSK-0082: `Data/Memories/Working/creaturecreator-definition-mutation-validation.json` still documents the old combined code behavior — refresh via kb-ingest.
