# CreatureCreator Audit Synthesis - 2026-09-02

## Scope

This synthesis reconciles Delta Audit #13 and Delta Audit #14 against the
current source and MemorySmith task board. The audit fixed point named by both
audits is `dff4a69`; implementation and validation below refer to the current
uncommitted working tree after the council review. The review covers runtime
SDF evaluation, resolved shape semantics, and body appearance baking.

**Decision:** The runtime correctness fixes are ready for commit after the
recorded validation, but TSK-0095 and TSK-0008 remain InProgress until
mutation-after-snapshot and performance evidence are added. TSK-0098 remains
open for editor lifecycle checks.

## Results

| Finding | Audit result | Task disposition |
| --- | --- | --- |
| Portable symmetry evaluates a mirrored composite subtree through original-point value slots | Confirmed | Extend TSK-0014 / CC-014 |
| `ResolvedShape.Resolve` independently reimplements legacy `PrimarySize` fallback and uses `1f` for missing capsule height | Confirmed | Keep under TSK-0094 / CC-090 |
| Body appearance recomputes resolved polyline lengths and allocates once per body vertex | Confirmed | Implemented in the generation path; retain TSK-0095 / CC-091 and TSK-0008 / CC-008 for authority and performance evidence |
| Deleted managed `BoxSdfNode` and old `SkeletonInferrer` reflection declaration | Confirmed resolved by prior work | No new task |

## Verification

### F-01: SDF symmetry subtree evaluation

Delta Audit #13's source claim is confirmed in
`Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs`. The `Symmetry`
operation previously evaluated the mirrored child by calling `EvaluateOperation`
with the same value buffer and therefore reused values calculated at the
unmirrored point. That is incorrect when `A` identifies a composite subtree.

The implementation now recursively evaluates the subtree at the reflected point,
including `SmoothUnion` and nested `Symmetry`, while preserving the `+inf`
absent-value contract. Reflected subtree culling is conservatively disabled
because child AABBs are stored in the original space; grid parity and a
performance benchmark remain follow-up evidence gates.

### F-02: Shape fallback duplication

Delta Audit #13's claim is confirmed in
`Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`.
`ResolvedShape.Resolve` reads `PrimarySize` and independently expands radius,
capsule height, ellipsoid radii, and box extents. The capsule-height fallback is
hardcoded to `1f`, unlike the legacy-size interpretation of the other fields.
This remains an open CC-090 consolidation item. No speculative change was made
because changing the fallback semantics requires explicit current-schema and
legacy-schema parity evidence.

### F-03: Body appearance recomputation

Delta Audit #14's claim is confirmed in
`Assets/Scripts/Runtime/Appearance/BodyVerticalGradientSampler.cs` and
`Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs`. The sampler allocated a
segment-length array and recomputed total length for every body-surface vertex,
even though `ResolvedBody` already owns those values.

The implementation adds `ResolvedBody` overloads for body sampling and color
evaluation. The definition overloads resolve once for standalone callers. The
generation snapshot now captures a cloned Body appearance and `Forward`; the
internal appearance bake receives that snapshot and uses captured Body and
part appearances in both managed and Burst-resolve paths. One-sample and empty
Body fallbacks are covered. Closest-point projection and vertical sampling
remain per-vertex work because they depend on the query point.

## Council Review

Three independent BeastMaster seats reviewed the implementation and task
state. All seats initially recommended no commit because of the one-sample
Body exception, incomplete snapshot authority, and insufficient current-source
evidence.

- Runtime generation seat: 99% confidence on the one-sample crash, 96% on the
  snapshot gap, and 98% on the managed compatibility-path gap. The crash and
  snapshot gaps were repaired; the compatibility path remains explicitly
  standalone while the generation path uses captured inputs.
- Validation seat: 98% confidence that the prior full-suite evidence was stale,
  94% that symmetry parity coverage was narrow, and 90% that topology evidence
  needed a current rerun. The current full runtime suite now passes; broader
  parity and generated mirrored-composite topology tests remain desirable.
- Editor-boundary seat: 97% confidence on snapshot authority, 99% on the
  one-sample crash, and 95% on documentation overstatement. Runtime authority
  is repaired, while SceneView lifecycle work remains under TSK-0098.

The dissent recorded by the seats is preserved: the earlier `56/56` focused
result was not sufficient to close the slice because it did not cover the
zero-segment Body case or prove snapshot immutability.

## Task Disposition

- **TSK-0014 / CC-014:** updated with the symmetry evaluator implementation and
  the remaining Unity parity gate.
- **TSK-0095 / CC-091:** updated because this is the concrete body-appearance
  snapshot-authority closure identified by the active task.
- **TSK-0008 / CC-008:** updated with the quantified per-vertex allocation and
  loop removal as focused performance evidence.
- **TSK-0094 / CC-090:** updated with the unresolved `ResolvedShape` fallback
  duplication and capsule-height inconsistency.

No duplicate task was created. Audit #14 is a corroboration and mechanism-level
extension of the existing CC-091 concern, not a separate architecture track.

## Validation

- `dotnet build .\\ProceduralCreature.Tests.Runtime.csproj --no-restore`: passed,
  0 warnings, 0 errors.
- Added resolved-body versus definition-based body-sample parity coverage in
  `Assets/Scripts/Tests/Runtime/BodyVerticalGradientAppearanceTests.cs`.
- Unity `6000.0.35f1`, PlayMode, focused
  `BodyVerticalGradientAppearanceTests`: 48 passed, 0 failed, 0 skipped.
- Unity `6000.0.35f1`, PlayMode, assembly
  `ProceduralCreature.Tests.Runtime`: 437 passed, 0 failed, 0 skipped, against
  the current working tree.
- The focused and full runs include the one-sample and empty-Body tests added
  in this wave. Unity also reported one existing persistent-allocation leak
  warning; no compiler errors were reported.

## Residual Risk

The shape fallback rule still has multiple interpretation points. A follow-up
must choose the canonical expansion helper and explicitly decide whether a
legacy capsule with no authored height uses `PrimarySize` or the existing `1f`
default, then validate serialization and generated-field parity.

Remaining evidence gates are mutation-after-snapshot tests for Body and part
appearance plus `Forward`, symmetry/reference grid parity with both culling
modes, generated mirrored-composite topology and determinism coverage, managed
compatibility-path performance evidence, and the editor lifecycle checks in
TSK-0098. These gaps do not invalidate the current runtime pass, but they do
prevent closing the related tasks.
