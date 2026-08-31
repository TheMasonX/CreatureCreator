# CreatureCreator — Delta Audit #2 (2026-08-25, later pass)

**Commit checked:** `1e1a575` — re-verified via `git fetch origin main` immediately before this pass; local and remote HEAD are identical to the previous two reports. **Still zero new commits.** This continues the deep dive into files not yet read: `Editor/BodyEditSolver.cs` and `Editor/BodySplineAuthoring.cs` (the two files behind CC-016's Body-drag authoring tools).

One real bug this time, not just a maintainability note.

---

## 1. Bug — `MinSpacingSqr` is a squared-magnitude epsilon (`1e-10`) but 5 of its 6 call sites compare it against *linear* distances, silently defeating five "degenerate spline" guards

`BodySplineAuthoring.cs:43`:
```csharp
private const float MinSpacingSqr = 1e-10f;
```

The name (`...Sqr`) and the value (`1e-10`, i.e. `(1e-5)²`) both signal "compare this against a **squared** magnitude." Exactly one of its six call sites does that correctly:

```csharp
// line 583 — correct: compared against .sqrMagnitude
return direction.sqrMagnitude <= MinSpacingSqr ? fallback : direction.normalized;
```

The other five compare it directly against **linear** distances — sums or averages of `Vector3.Distance(...)` / `.magnitude` calls, which are not squared:

```csharp
// line 88  (AppendSample)          — spacing = tailDelta.magnitude
// line 137 (PrependSample)         — spacing = headDelta.magnitude   [same pattern]
if (spacing <= MinSpacingSqr) { ... }

// line 373 (ResampleEvenChords)    — totalLength = arc[source.Length - 1] (sum of chord distances)
if (totalLength <= MinSpacingSqr) return null;

// line 454 (SpaceEvenly-family)    — totalLength = sum of Vector3.Distance(...)
if (totalLength <= MinSpacingSqr) return;

// line 537 (DragSampleEvenly)      — linkLength = totalLength / (count - 1)
if (linkLength <= MinSpacingSqr) { /* "Degenerate coincident spline; no meaningful chain to bend." */ }
```

**Effect:** each of these five guards is supposed to catch "this spline/segment has effectively collapsed to a point" and take a safe fallback path instead of running the real math on a near-zero-length input. Because the threshold is `1e-10` instead of something like `1e-5`, none of them fire until the samples are *ten million times* closer together than the constant's own name and sibling usage (line 583) imply they should. A spline whose samples have drifted to within, say, `1e-6` units of each other — visually and functionally coincident, exactly the case each comment describes ("no meaningful chain to bend", degenerate tail direction, etc.) — sails past every one of these checks and proceeds into the real computation on a near-zero-length chain:

- `AppendSample`/`PrependSample` (lines 88, 137): the "recover a fallback direction because the tail segment has zero length" branch doesn't trigger; a near-zero `tailDelta` gets normalized and used as a real direction, amplifying whatever tiny numerical noise is in that near-coincident pair.
- `ResampleEvenChords` (line 373): proceeds to bisection-search a chord step size `d = totalLength / (targetCount - 1)` on a near-zero `totalLength`, walking near-coincident points instead of returning `null` (its documented degenerate-input signal).
- `DragSampleEvenly` (line 537): skips the explicit, commented "degenerate coincident spline; no meaningful chain to bend" shortcut and instead runs a full FABRIK sub-chain solve with `linkLength` on the order of `1e-6..1e-9` — numerically live but not what the code's own comment says should happen.

None of this needs adversarial input to reach — repeated `SpaceEvenly` / drag operations, or a pathological but not malicious sequence of edits, can plausibly walk samples this close together, and there is currently no test that exercises it: `Tests/Editor/BodySplineAuthoringTests.cs` has zero references to "degenerate," "coincident," or any near-zero-length scenario, so this has no regression coverage in either direction.

**Recommendation:** this reads like a copy-paste of the constant from its one correct (`sqrMagnitude`) use site into five other guards that needed a linearly-scaled epsilon instead. Split it into two constants — e.g. keep `MinSpacingSqr = 1e-10f` for the one `sqrMagnitude` comparison at line 583, and add `MinSpacing = 1e-5f` (or whatever the design actually intends) for the five linear-distance guards — and add one test per guard with samples clustered inside the old effective (non-)threshold to prove each one now fires.

---

## 2. Everything else checked in this pass — clean

- **`BodyEditSolver.cs`** (full read, 532 lines) — no findings. Deterministic, snapshot-based (never drifts frame-to-frame), each stage individually documented with its own rationale, no UnityEditor dependency (so it's EditMode-testable as claimed), and the one thing I checked for — an `if (count == 0)` branch inside `BuildResult` that looks unreachable given both public entry points early-return `BodyEditResult.Empty` before ever calling `BuildResult` — is genuinely dead code but harmless (a redundant assignment of `minRatio` to a value it already defaults to), not worth its own ticket.
- **`BodySplineAuthoring.cs`**, everything besides §1 — the even-spacing contract, the FABRIK-baseline vs. `BodyEditSolver` split, and the "validator reports, authoring repairs" division of responsibility are all consistent with what CC-016's own ticket and the class's doc comment describe.

---

## Summary table

| # | Finding | Type | Severity |
|---|---|---|---|
| 1 | `MinSpacingSqr` (1e-10, squared-magnitude scale) compared against linear distances at 5 of 6 call sites, defeating degenerate-spline guards in `AppendSample`, `PrependSample`, `ResampleEvenChords`, the `SpaceEvenly` family, and `DragSampleEvenly` | Bug — unit/scale mismatch, zero test coverage either way | Medium — no crash observed at the sites read, but silently skips documented safety fallbacks on a plausible (not adversarial) input |
