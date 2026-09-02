# Handoff: generation speed Slices A–D complete; next is async generation (2026-09-01)

**Status:** The 2026-08-31 mesh-generation speed program (Slices A–D) is
implemented, validated, and committed on `main`. This handoff records the
committed state, the measured improvement, the remaining speed targets, and the
next workstream: **asynchronous generation** (the user's explicit next ask) plus
the residual FieldSampling and contour-resolution hotspots. No code changed in
this handoff beyond this document and the follow-up task record.

## Committed state

| Commit | Slice | Change | Measured win |
| --- | --- | --- | --- |
| `3522b0b` | A | Native `DensityGrid` (no managed copy) + Burst `ActiveCellScanJob` | ActiveCellConstruction 149 ms -> ~11 ms |
| `511d59c` | maintenance | MemorySmith wiki deploy + local MCP ignore + dino anchor authoring (pre-existing, unrelated) | — |
| `48f3451` | C | `PartAppearanceSampler.Resolver` AABB broad phase (internal `EnableBroadPhase`) | Neutral on dino; helps many-part creatures |
| `0349df5` | B | Root-AABB early exit in `SdfSamplingJob` (region-aware FieldSampling) | FieldSampling 507 -> 121 ms |
| `830708d` | D | Burst per-vertex appearance resolution (`AppearanceResolveBurst`); `AppearanceBaker.UseBurstResolve` | AppearanceBake 112 -> 39 ms |

`HEAD` = `830708d` (Slice D). `origin/main` = `0349df5` (Slice B), so **Slice D
is not yet pushed**. Working tree clean.

## Measured improvement (real editor, dino `96^3`, diagnostics on)

| Stage | Before (post-A) | Latest | |
| --- | --- | --- | --- |
| TotalGeneration | 671.5 ms | **216.6 ms** | ~3.1x |
| FieldSampling | 507.5 ms | **121.4 ms** | ~4.2x |
| MeshExtraction | 49.4 ms | 53.0 ms | — |
| MeshActiveCellConstruction | 12.0 ms | 11.0 ms | — |
| MeshContourResolution | 26.0 ms | 30.0 ms | — |
| MeshVertexWelding | 5.0 ms | 5.0 ms | — |
| MeshTriangleEmission | 3.0 ms | 3.0 ms | — |
| MeshValidation | 2.5 ms | 2.5 ms | — |
| AppearanceBake | 111.9 ms | **39.3 ms** | ~2.8x |

Latest breakdown (216.6 ms): FieldSampling 121.4 (56%), AppearanceBake 39.3
(18%), MeshExtraction 53.0 (24%, of which contour 30), MeshValidation 2.5.

## Next focus: asynchronous generation (user mandate)

**User request (verbatim intent): "further resolve rendering speed issues, and
to make it async if at all possible."** Make generation not block the editor:
kick it off on edit-idle or regenerate request, run the heavy stages off the
main thread, and swap the finished preview in without a hitch.

Tracked as **TSK-0103** "Async generation pipeline: keep the editor responsive"
(Backlog, High, `user-mandated` label, user request quoted verbatim; created
2026-09-01 via the MemorySmith MCP task tools). Related tasks: **TSK-0065**
(CC-061 final-mesh hardening, lists async generation in scope), **TSK-0061**
(CC-057 three-tier preview proxy), **TSK-0095** (CC-091 generation pipeline
stage boundaries).

### Why this is clean here

`CreatureMeshGenerator.Generate` splits cleanly:

- **Off-main-thread-safe stages** (pure runtime data, no Unity objects):
  `SdfCompile` -> `FieldSampling` (Burst) -> `MeshExtraction` (Burst active-cell
  + managed contour) -> `AppearanceBake` (Burst resolve + managed tail). These
  consume/emit `SdfProgram`, `DensityGrid`, `MeshExtractionResult`, `Color[]`.
- **Main-thread-only stage**: `MeshExtractionResult.ToUnityMesh()` creates the
  `UnityEngine.Mesh`. Unity Mesh creation/upload must stay on the main thread.

So the async boundary is: run the pipeline through `AppearanceBaker.Bake`
off-thread, marshal the plain-data result back, then build the Unity Mesh and
apply colors on the main thread.

### Recommended design

1. **`GenerationRequest`** (editor or runtime): captures a validated definition,
   a sequence number (or a content fingerprint, following CC-007
   `BuildPlacementFingerprint`), and a callback. Enqueued by
   `CreatureEditorWindow.RegeneratePreview` / `ProcessAutoRegeneration` and by
   `CreatureRuntimePreview`.
2. **Background runner**: executes stages 1–5 on a dedicated background thread
   (or `Task.Run`), producing a plain-data result. Dispose all native buffers
   (`DensityGrid`, job scratch, programs) on that thread exactly once (Slice A
   dispose convention). Do NOT touch `UnityEngine.Mesh` or scene objects here.
3. **Main-thread completion**: post back, discard the result if its sequence
   number is stale (a newer edit arrived), else `ToUnityMesh()` + `SetColors`,
   swap the preview, record `GenerationDiagnostics`.
4. **Cancellation / staleness**: the newest request wins. Old in-flight results
   are dropped at completion, never applied. This preserves the CC-007 stale-
   preview discipline and determinism (async does not change output; same input
   still yields the same mesh).
5. **Three-tier tie-in (CC-057)**: Tier 0 proxy (<16 ms) for interaction, Tier 1
   async Fast SDF refinement (~100-200 ms), Tier 2 exact on finalize. Async
   generation is the Tier 1 -> Tier 2 path. Keep the proxy untouched until the
   async result lands.

### Validation for async

- Determinism test: generate a definition synchronously and asynchronously;
  assert identical mesh topology + colors (the pipeline is deterministic).
- Staleness test: enqueue two requests; assert the older result is dropped.
- EditMode for the scheduler state machine; PlayMode/manual for the editor
  window swap (bridge cannot drive the interactive window).
- Keep the synchronous `Generate` path as the reference/fallback (as CC-014
  kept the scalar evaluator during migration).

## Remaining speed targets (after async)

1. **FieldSampling (121.4 ms, 56%)** — the biggest stage. Slice B pre-fills +inf
   outside the root AABB (86.7% of corners), but the inside-root 13.3% still pay
   the full per-op AABB scan per corner (dino = 252 ops). Options:
   - **Op bucketing / coarse occupancy**: only check ops whose cell/AABB overlaps
     the corner, turning O(252) into O(nearby ops). Preserve the CC-063 `+inf`
     contract and bit-identical Fast output.
   - **Schedule all batches, then `Complete` once** (currently one `Complete`
     per batch serializes the parallel-for); measure whether fewer sync points
     help at 112^3/160^3.
   - Re-run the CC-062 canonical matrix (Dino, 96^3..256^3) before/after.
2. **MeshContourResolution (30 ms)** — now the largest extraction substage, and
   managed. Burst-ing it reads the native grid; the Asymptotic Decider + welding
   are subtle. Validate watertightness + topology parity (reference extractor
   tests) before claiming closure.
3. **MeshValidation (2.5 ms)** — keep for final/export; consider gating it off
   in the interactive preview path (it is cheap; only do this if it shows up).

## Context / gotchas for the next agent

- **Native `DensityGrid` ownership (Slice A):** `SamplePortable` returns a
  grid that owns a `Persistent` buffer. Every caller MUST `Dispose()` it (the
  generator does in a `try/finally`; tests use `using`). Keep this rule in the
  async runner and in any new test.
- **CC-064 `+inf` contract:** a culled/outside sample reads `+inf` = absent,
  never a giant valid distance. `NaN` is always invalid. Fast culling writes
  `+inf`; every consumer (appearance, interpolation, min/max) must treat it as
  "no candidate". The Burst appearance jobs and merge honor this — keep it.
- **Bit-identical parity is the bar.** Each slice added a bit-exact parity test
  (`SamplePortable_RegionAwareEarlyExit_MatchesReferenceEvaluator`,
  `Bake_BurstResolve_MatchesManagedResolveExactly`). Keep the internal toggles
  (`AppearanceBaker.UseBurstResolve`, `Resolver.EnableBroadPhase`) so parity can
  be forced; new perf work must not silently change appearance/topology.
- **Unity Mesh is main-thread-only.** `MeshExtractionResult.ToUnityMesh()` and
  `Mesh.SetColors` must not run on the background thread. This is the async
  boundary; do not cross it.
- **Three-tier model (CC-057) is the agreed UX strategy.** Tier 0 proxy <16 ms,
  Tier 1 Fast SDF ~100s ms, Tier 2 exact final. Do not make the interactive path
  depend on synchronous high-quality remeshing.
- **Too many P1s** was an audit warning (2026-08-24 delta audit). Sequence async
  as the single P1; keep FieldSampling/contour as measured follow-ups on the
  same TSK-0008 evidence trail.
- **MemorySmith is the live task surface.** `Data/Tasks/*.json` are imported
  records (do not edit by hand); `docs/tasks/*.md` are frozen history. Add
  evidence with `memorysmith_task_add_comment`, transition with
  `memorysmith_task_set_status`. TSK-0008 holds the Slice A–D evidence trail.

## Validation commands

- `dotnet build ProceduralCreature.Runtime.csproj --no-restore` and
  `...Tests.Runtime.csproj` — compile gate.
- Unity MCP: `refresh_unity` (compile) then `read_console` (expect 0 errors/0
  warnings), then `run_tests` PlayMode `ProceduralCreature.Tests.Runtime`
  (baseline **422/422** after Slice D).
- Timing: in-editor `execute_code` on `Assets/Creatures/dino_creature.json`
  (dino), Burst-warmed, avg of 3–5; compare against the table above and the
  CC-062 matrix. `AppearanceBaker.UseBurstResolve` is `internal` (not visible
  to `execute_code`); the default is Burst.
