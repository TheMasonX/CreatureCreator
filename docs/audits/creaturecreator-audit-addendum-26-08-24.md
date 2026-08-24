# CreatureCreator — Audit Addendum (Latest State)

**Scope:** Fresh pull of `main`, HEAD newer than my prior pass (repo grew ~15MB → ~25MB; `active-tasks.md` grew CC-035 → CC-063 in the interim).
**Purpose:** (1) verify the five findings from my prior audit against current code, (2) avoid duplicating the extensive work already in `docs/audits/creaturecreator-further-audit-26-08-23-22-05-00.md` (862 lines, dated after my last pass — I read its table of contents to confirm scope before starting), (3) go deep on the one major area neither that audit nor mine had reached yet: the Burst-portable SDF execution path (`SdfProgram.cs`, `DensityGrid.SamplePortable`) added since my last pull under CC-014/CC-045/CC-062.

---

## 1. Status of prior findings — none addressed yet

All five are still present verbatim in the current tree:

| # | Finding | Status |
|---|---|---|
| 2.1 | `PartType.Body`/`Root` still live, unauthorable-but-selectable enum values | Unchanged |
| 2.2 | `PartType.Tail` has no editor authoring path | **Unchanged, and now better-evidenced.** The palette (`ValidV2PartTypes`) was touched again since my last pass — `PartType.Hand` was added (tracking CC-036) — but `Tail` still wasn't. That rules out "hasn't been touched since"; the array has been edited twice now without Tail being added either time. |
| 2.3 | `ValidationCode.DuplicateBodySampleId` reused for both actual duplicates and out-of-order IDs | Unchanged |
| 2.4 | No minimum absolute Body-spacing / degenerate-length check | Unchanged |
| 2.5 | `HasParentCycle`'s `if (current.ParentId == null) break;` is dead code under schema v2 | Unchanged |

None of these are urgent, but flagging that a repo audit doesn't automatically get read into the next work session — if these are worth fixing, they need a ticket, not just a document somewhere.

## 2. `further-audit`'s Finding 1 is confirmed still live and has spread

That audit flagged `SdfProgramBuilder.Compile()` reading `Shape.SmoothBlendRadius` for limb SDF composition despite `Shape` being explicitly documented as inert for limbs. Current code:

- `SdfProgramBuilder.cs:541` — `float blendRadius = compiled[i].Part.Shape.SmoothBlendRadius;` (the original managed path, unchanged)
- `SdfProgramBuilder.cs:357` — `Parameters = new float3(part.Shape.SmoothBlendRadius, 0f, 0f)` (the **new** Burst-portable `SdfOperation` construction, added since that audit ran)

The same inert-field dependency now exists in two places instead of one. Worth knowing before CC-045 ("remove the legacy managed SDF") lands — fixing this only in the path that's about to be deleted would leave it live in the path that survives.

## 3. New finding — `NativeArray<float> samples` leaks on any exception during portable sampling

**Where:** `Runtime/Morphology/Extraction/DensityGrid.cs`, `SamplePortable` (added since my last pass; not covered by any existing audit).

**Severity:** Medium. Not a correctness bug in the happy path — a resource-lifetime bug on the exception path, in Burst/job code where that's easy to lose track of.

```csharp
var samples = new NativeArray<float>(grid.SampleCount, Allocator.TempJob);   // ← allocated OUTSIDE the try
...
var scratchValues = new NativeArray<float>((int)scratchLength, Allocator.TempJob);
try
{
    for (int sampleStart = 0; sampleStart < grid.SampleCount; sampleStart += batchSize)
    {
        ...
        JobHandle handle = job.Schedule(sampleCount, 64);
        handle.Complete();                                                   // ← can throw
    }
}
finally
{
    scratchValues.Dispose();                                                 // ← only this gets cleaned up
}

for (int i = 0; i < grid._samples.Length; i++) grid._samples[i] = samples[i];
samples.Dispose();                                                           // ← never reached if the loop threw
return grid;
```

`scratchValues` is correctly protected by try/finally. `samples` is not — it's allocated before the `try` block and disposed only at the very end, after the loop. If `handle.Complete()` throws for any reason (an `IndexOutOfRangeException` from a Burst safety-check trip is the realistic case, given `ScratchValues` and `Samples` are both marked `[NativeDisableParallelForRestriction]` — meaning Unity's normal per-worker write-range safety check is deliberately turned off in favor of manual index math; if that math is ever wrong for a code path I haven't traced, or wrong for some future edit, Burst's remaining bounds checks would be what catches it), `samples` is never disposed. Unity's leak detector will report this as a leaked `Allocator.TempJob` allocation, typically surfacing on the next domain reload with a misleading-looking stack trace pointing at allocation time, not the actual throw site.

I confirmed the happy-path math is correct before flagging this (worth stating, since the interesting part of this finding is specifically that it's exception-path-only): `batchSize = PortableScratchValueBudget / operationCount`, `sampleCount = min(batchSize, remaining)` guarantees every `index` passed to `Execute` satisfies `index < batchSize`, so `valueOffset = index * operationCount` never exceeds the allocated `scratchLength`. The overflow guard (`scratchLength > int.MaxValue`) and the `scratchValues` reuse-across-batches comment (correct: `handle.Complete()` is synchronous, so no batch overlaps the next) are both already right. This is specifically and only about `samples`' lifetime on a throw.

**Fix:** wrap `samples.Dispose()` in the same `finally` (or nest a second try/finally, or move the `samples` allocation inside the outer try). Trivial fix; the value here is that it's very easy not to notice, because it never fires until something else goes wrong.

**Test gap:** I checked — no existing test (`DensityGridTests.cs`, `SdfCullingModeTests.cs`) exercises a thrown-exception path through `SamplePortable`. A test that forces a failure mid-loop (e.g. a program with a deliberately invalid `ConsumerUnionIndex`) and asserts no NativeArray leak warning would catch both this bug and any regression of it.

---

## 4. Net assessment

Nothing here is urgent — the leak only matters when something else is already failing, and the five carried-over findings were already low-priority. The main value of this pass is narrower than a full audit: confirming nothing regressed, and that the newest, least-audited code (the Burst path) got at least one real look before more work builds on top of it.
