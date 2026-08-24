---
id: creature-task-075
key: CC-075
title: Dispose TempJob samples array on the portable-sampling exception path
status: Done
type: Task
priority: P2
tags: [runtime, sdf, burst, nativearray, resource-lifetime]
dependsOn: []
related: [CC-014, CC-045, CC-062, CC-063, CC-064]
links:
  - Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs
  - Assets/Scripts/Tests/Runtime/DensityGridTests.cs
  - docs/audits/creaturecreator-audit-addendum-26-08-24.md

## Summary

`DensityGrid.SamplePortable` allocates `samples` as `Allocator.TempJob`
outside its `try` block and disposes it only after the batch loop. When any
job throws mid-loop (an out-of-range NativeArray read surfaces at
`handle.Complete()`), `samples` is never disposed. Unity reports the leak as a
leaked `Allocator.TempJob` allocation, usually on the next domain reload with
a misleading stack trace that points at allocation time rather than the throw
site. Raised as finding 3 in the 2026-08-24 audit addendum.

## Scope

- Move the final `samples` copy + dispose inside the existing `try`/`finally`
  so both `TempJob` allocations (`samples` and `scratchValues`) are disposed on
  every path, including exceptions.
- Add a regression test that forces a mid-loop job failure and asserts the
  exception path no longer leaks.

## Acceptance Criteria

- `SamplePortable` disposes both `TempJob` arrays when the batch loop throws.
- A test drives a deterministic mid-loop failure and asserts no leak warning.
- Happy-path sampling behavior and value parity are unchanged.

## Validation

- PlayMode `DensityGridTests.SamplePortable_InvalidRootIndex_ThrowsAndDisposesTemporaryAllocations`
  passes (1/1) with the fix.
- Focused SDF fixtures 24/24: `DensityGridTests` (7, incl. the new test),
  `SdfCullingModeTests` (4), `SdfNonFiniteFieldContractTests` (4),
  `SdfProgramBuilderLimbTests` (9) — happy-path sampling and value parity
  unchanged by the RootIndex guard.
- Full PlayMode run 380/385: the only 5 failures are the documented
  pre-existing ones (CC-025): the three validator `ToDictionary`
  duplicate-id throws, `Validate_RejectsPartWithNoParent`, and
  `RoundTrip_ReconstructsEquivalentDefinition` DisplayName assertion. None
  touch the SDF sampling path.
- Console clean after the runs: 0 errors, 0 warnings, no NativeArray leak
  message.

## Findings

Confirmed the audit's trace against the current tree: `scratchValues` was
protected by `finally`; `samples` was allocated before the `try` and disposed
only at the end of the method, so a throw in the loop skipped its dispose. The
other `Allocator.TempJob` allocations in the runtime assembly are the same two
in this method; the `Allocator.Temp` in `SdfProgramEvaluator.Evaluate` is
auto-reclaimed by the frame and out of scope here.

Two findings from implementation:

- **RootIndex guard added.** An initial test tried to force a mid-loop job
  failure with an out-of-range RootIndex, expecting `IndexOutOfRangeException`.
  It did not throw: this editor runs the Burst job without NativeArray safety
  checks, so the out-of-bounds read silently produced garbage. That is itself a
  latent defect — a malformed program either crashed (safety on) or returned
  garbage (Burst release). `SamplePortable` now fails fast with a
  `DomainException` for `RootIndex < 0 || RootIndex >= Operations.Length`
  (mirrors `SdfProgramEvaluator.Evaluate`), thrown inside the `try` so the
  disposal path is exercised deterministically.
- **Residual risk on the leak assertion.** `LogAssert.NoUnexpectedReceived()`
  only catches the TempJob leak warning if it surfaces in-band; the audit noted
  it typically fires on the next domain reload. The definitive guarantee is
  structural: both `samples` and `scratchValues` are disposed in the same
  `finally`. The test's primary regression value is the exception path + RootIndex
  guard; removing either fails the test.

## Blockers

None.

## Next Step

None — ticket complete. The `Allocator.Temp` in `SdfProgramEvaluator.Evaluate`
remains an intentional non-issue (frame-reclaimed).
