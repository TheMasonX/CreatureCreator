# CreatureCreator Audit Synthesis — CCWIND Triangle Winding / Facing

**Report:** `CCWIND-A17600A28EE37777` (triangle-winding-facing audit)
**Mode:** Full reconciliation (read-only; no code changes)
**Date:** 2026-09-06
**Fixed point:** `44323197c1476419a09f483affa175f0c848e755`
**Task system:** MemorySmith `TSK-####` records are authoritative.
**Source audit:** `docs/audits/creaturecreator-triangle-winding-facing-audit-26-09-06-11-22-00.md`

---

## Executive summary

The audit describes a credible mesh winding/facing risk: watertightness is not
an orientation invariant, and several source-level mechanisms can produce
isolated or visually pathological triangles. I confirmed the four central
mechanisms directly against source. The audit recommends **no net-new task** —
the findings absorb into two existing active owners, **TSK-0119** (SDF culling
and non-finite field correctness) and **TSK-0008** (extraction/perf + test
gates). This reconciliation follows that, and both owners carry the explicit
performance gate from the user mandate: **fix it without hurting mesh
generation speeds.**

No runtime, editor, or test code was changed in this reconciliation. Unity
execution was therefore not required.

---

## Verification results

All material claims were checked against the current source at the fixed point.

| ID | Claim | File / symbol | Result |
|---|---|---|---|
| W-01 | Topology validator cannot detect winding errors | `MeshTopologyValidator.CountEdge` | **Confirmed** |
| W-02 | Zero/invalid gradient accepted as valid orientation | `MarchingCubesExtractor.EmitTriangle` | **Confirmed** |
| W-03 | Gradient sampled at nearest grid corner, not centroid | `DensityGrid.EstimateGradient` | **Confirmed** |
| W-04 | `+inf` consumed as a real sign crossing | `ActiveCellBuilder` / `CubeContourResolver.InterpolateEdge` | **Confirmed** |
| W-05 | Contour loops are undirected (`FaceSegment` no direction) | `CubeContourResolver` | **Confirmed** |
| W-06 | Fan triangulation is a documented simplification | `MarchingCubesExtractor` loop emission | **Confirmed** |
| W-07 | Smooth-union / symmetry weak-gradient regions plausible | SDF smooth-min + `min(field, mirrored)` | **Confirmed (risk)** |
| W-08 | Fixed epsilon scale can affect orientation indirectly | generation surface-epsilon normalization | **Confirmed (design)** |
| W-09 | Test suite does not exercise orientation failure modes | extraction test files | **Confirmed** |

Confidence axis (evidence quality) vs severity (impact) are kept independent.
"Confirmed (risk)" for W-07 means the field mechanism is real but production
frequency needs a fixture measurement, matching the audit.

### Source evidence (exact)

- **W-01:** `MeshTopologyValidator.CountEdge` builds key `(int,int) key = a < b ? (a,b) : (b,a)`, so direction is discarded; only `count == 1` (boundary) and `count > 2` (non-manifold) are flagged. Two co-directional adjacent triangles pass as healthy.
- **W-02:** `EmitTriangle` → `bool correctlyWound = Vector3.Dot(faceNormal, gradient) >= 0f;` A zero gradient yields dot `== 0`, so the resolver's arbitrary loop order is retained as "correct."
- **W-03:** `EstimateGradient` → `Mathf.RoundToInt((point.x - Origin.x) / CellSize)` etc. samples the finite-difference neighborhood around the nearest integer grid corner, not the centroid.
- **W-04:** `ActiveCellBuilder.ClassifyCaseIndex` treats `+inf >= 0` as outside, producing a mixed cell for a finite-negative + `+inf` pair; `InterpolateEdge` then sets `t = 0f`/`t = 1f` for a `+inf` endpoint, pinning the "crossing" to the finite corner.

---

## Accepted findings (severity order) and disposition

No net-new task. Two existing InProgress owners are extended (no duplicate
keys created).

### TSK-0119 — Harden fast SDF culling and non-finite field consumers (InProgress, High)
Correctness half of the winding fix.

- **W-02 (P1)** — Orientation API becomes validity-bearing; never silently
  accept loop order on a zero/invalid gradient; deterministic local fallback;
  no reintroduction of per-triangle SDF evaluation.
- **W-04 (P1/P2)** — Cell-boundary invariant so a surface-contributing cell
  has finite corner data on every crossing edge; prefer a culling halo or
  cell-level classification over per-corner `+inf` crossing fabrication.
- **W-07 (P2)** — Weak-gradient fixtures (smooth union through blend center,
  mirrored sphere at X=0, mirrored limbs, combined) to separate bad geometry
  from weak orientation signal.

### TSK-0008 — Profile and optimize preview generation hotspots (InProgress, High)
Extraction-side improvement, test/diagnostic gates, and the benchmark owner.

- **W-01 (P1)** — Test-only directed-edge invariant (opposing traversal on
  every 2-use undirected edge). Kept out of the hot path.
- **W-03 (P1/P2)** — Replace nearest-corner gradient with a cell-local
  trilinear derivative from already-loaded corner densities, benchmarked
  before becoming baseline.
- **W-05 (P2)** — Long-term oriented-contour design; only after W-01 gives a
  failing baseline.
- **W-06 (P2)** — Measure fan-triangulation quality before any change; keep
  fan unless a real fixture proves it is the artifact source.
- **W-08 (P3)** — Scale-ratio fixture characterization; no speculative epsilon
  change.
- **W-09 (P2)** — Orientation test matrix (sphere outward normals, overlapping
  spheres, mirror, smooth-union, adversarial perturbation, scale sweep, coarse
  resolutions).

---

## Task disposition

| Mechanism | Disposition | Owner |
|---|---|---|
| W-01, W-03, W-05, W-06, W-08, W-09 | Extend existing owner (scope + evidence comment) | TSK-0008 |
| W-02, W-04, W-07 | Extend existing owner (scope + evidence comment) | TSK-0119 |
| No findings rejected/fixed/stale | — | — |

Both records remain **InProgress** (no implementation performed in this
reconciliation). The user mandate (verbatim, STRICT) and the performance gate
are recorded on both owners.

---

## Performance-preservation gate (user mandate)

The audit and this reconciliation treat the current speed gains as
authoritative: cached grid data, Burst active-cell scan, region-aware
sampling, and cached-gradient winding. Every winding fix must:

- not reintroduce per-triangle SDF evaluations;
- derive orientation from already-loaded cell data where possible;
- be benchmarked against the recorded production baseline (e.g. VPU-12
  ~147 ms FieldSampling / ~53 ms MeshExtraction) before acceptance;
- reject any proposal that materially regresses FieldSampling / MeshExtraction
  / total preview generation.

TSK-0008 is the explicit benchmark owner that enforces this gate.

---

## Assumptions, blockers, next evidence

- **Assumption:** the audit's recommended two-owner mapping is correct and no
  separate winding task should fragment the extraction/culling contract.
  Existing prior coverage (`TSK-0068` Done, `TSK-0067` Done) is the historical
  contract foundation and was not reopened.
- **No blockers** for task capture. Implementation is deferred to the two
  owners.
- **Next evidence:** W-01 directed-edge test and a sphere outward-normal test
  first (turns the screenshots into an objective failing fixture); then a
  suspect-fixture diagnostic capturing triangle/cell/gradient metadata to
  distinguish wrong winding vs bad vertex placement vs fan geometry.

---

## Source ledger

- `Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/ActiveCellBuilder.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/CubeContourResolver.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/MeshTopologyValidator.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/MeshExtractionResult.cs`
- `Data/Tasks/tsk-0119-harden-fast-sdf-culling-and-non-finite-field-consumers.json`
- `Data/Tasks/tsk-0008-profile-and-optimize-preview-generation-hotspots.json`
- `Data/Tasks/tsk-0068-fast-mode-non-finite-field-contract-inf-outside-culled.json`

Unity execution was not required; this was a read-only task reconciliation.
No code files were modified. Related untracked creature JSON fixtures and
unrelated new tasks (TSK-0127/0128) in the worktree were left untouched.
