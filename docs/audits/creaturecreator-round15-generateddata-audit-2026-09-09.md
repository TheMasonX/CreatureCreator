# CreatureCreator — Round 15: `GeneratedCreatureData` Test Breakage & Contract Gaps

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `d0489472d370ee7cd23af0ffd9ae4f92ac6e13cd` (unchanged since Round 14 — no new commits on the branch)
**Scope this round:** the just-landed `GeneratedCreatureData` hardening work (commits `d9afe51`, `8f4e50a`, `cf5c1ba`, `7b00bbb` — "harden constructor invariants," "add contract regression fixture," visible at the top of the branch's commit log going into this session) plus a structural check of `Assets/Scripts/Runtime/Generation/`.
**Why this area:** it's the most recently-touched code on the branch (landed immediately before the animation-roadmap-audit commit that closed out Round 14's starting point), and "harden constructor invariants" work is exactly the kind of change most likely to leave a sibling test file out of sync — which is what happened.

---

## Finding 1 (P1 — confirmed broken test): `GeneratedCreatureDataTests.cs` will fail on the next test run

Two test files exist side by side for the same type:

- `Assets/Scripts/Tests/Runtime/GeneratedCreatureDataContractTests.cs` — added by the recent hardening commit (`8f4e50a`), correctly reflects the current constructor.
- `Assets/Scripts/Tests/Runtime/GeneratedCreatureDataTests.cs` — the older file, last touched by an adjacent-but-different commit (`f2468a3`/`254ffea`, "generated data ownership regression"), **not updated for the new constructor guards**.

The current constructor (`Generation/GeneratedCreatureData.cs:20-38`) checks its five parameters in this order and throws `ArgumentNullException` for any null:

```csharp
if (definition == null) throw new ArgumentNullException(nameof(definition));
if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
if (meshResult == null) throw new ArgumentNullException(nameof(meshResult));
if (colors == null) throw new ArgumentNullException(nameof(colors));
if (topologyReport == null) throw new ArgumentNullException(nameof(topologyReport));
```

`GeneratedCreatureDataTests.cs` has three tests, all now broken:

1. **`Constructor_DefensivelyCopiesColors_AndExposesReadOnlyView`** (lines 12-32) — calls the constructor with `snapshot: null, meshResult: null` and a real `colors` array, with **no `Assert.Throws` wrapper**, expecting to reach an assertion about the colors copy. It now throws `ArgumentNullException("snapshot")` on the very first null check and fails outright before any assertion runs.
2. **`Constructor_DefensivelyCopiesDefinitionOwnership`** (lines 34-54) — same shape, same failure: `snapshot: null, meshResult: null`, no exception wrapper, fails on the `snapshot` check.
3. **`Constructor_NullColors_ThrowsDomainException`** (lines 56-65) — passes a real `definition`, but `snapshot: null` too, so it also throws on `snapshot` first, not on `colors` as the test name and body intend; and even if it reached the colors check, it asserts `Assert.Throws<DomainException>`, while the actual thrown type is `ArgumentNullException` — **not** a subtype of `DomainException` (`DomainException : Exception` directly; `ArgumentNullException : ArgumentException : SystemException : Exception` — unrelated hierarchies), so the assertion would fail on type mismatch even independent of the ordering issue.

All three tests currently fail if this test assembly is actually run. Recommend either deleting `GeneratedCreatureDataTests.cs` (its coverage is superseded by `GeneratedCreatureDataContractTests.cs`, which correctly exercises all five null-argument paths and the colors-defensive-copy behavior) or updating its three tests to supply valid `snapshot`/`meshResult` values, matching how the new contract test builds a `valid` instance via `CreatureMeshGenerator.GenerateData(...)` first. Given the contract test already covers the same ground more completely, deletion is the smaller, cleaner fix.

---

## Finding 2 (P3 — convention drift, needs an explicit decision): `ArgumentNullException` vs. this project's `DomainException` convention

`Common/DomainException.cs`'s own doc comment states its scope directly: *"Thrown only for programmer errors — contract violations that indicate a bug in calling code... Example correct use: calling a solver with a null chain reference."* A null constructor argument is exactly that scenario, and it's how the other 42 files in `Assets/Scripts/Runtime` that guard against null handle it — `DomainException`, consistently.

`GeneratedCreatureData`'s just-hardened constructor is one of only four files in `Runtime` using the BCL's `ArgumentNullException` instead (`GeneratedCreatureData.cs`, `GenerationDiagnostics.cs`, `CreatureGenerationScheduler.cs`, `Common/CollectionCloneUtility.cs`). The other three are arguably more defensible as generic/infrastructure-adjacent code; `GeneratedCreatureData` is a core domain handoff type documented in the exact same "immutable domain result" language as everything else that *does* use `DomainException`. The stale test file's expectation (`Assert.Throws<DomainException>`) is circumstantial evidence that the *original* intended contract for this constructor was the project's own convention, and the hardening pass introduced `ArgumentNullException` instead without that being a deliberate, recorded decision.

Not asking for a revert — either choice is defensible — but recommend making it an explicit, recorded decision (a one-line note on the owning task) rather than something that drifted silently during a hardening pass, since it's exactly the kind of small inconsistency that erodes the value of having a documented exception-type convention at all.

---

## Finding 3 (P3 — real but currently-unexploited contract gap): `GeneratedCreatureData`'s immutability claim doesn't hold for `MeshResult`

The class doc comment says: *"Immutable-by-convention. Mutable definition/array inputs supplied by a producer are defensively copied so consumers cannot mutate the generated result through this object."* This is true for `Definition` (deep-cloned) and `Colors` (array-cloned into a `ReadOnlyCollection`) — but **not** for `MeshResult`.

`MeshResult` is stored by direct reference with no copy (`Generation/GeneratedCreatureData.cs:35`: `MeshResult = meshResult;`). Its type, `Morphology/Extraction/MeshExtractionResult.cs`, exposes `Positions`/`Triangles` as `public List<Vector3> { get; }` / `public List<int> { get; }` — the getter returns the live, mutable list, not a read-only view — plus a public, freely re-callable `ComputeAngleWeightedNormals()` mutator (lines 16-17, 47). Any consumer holding a `GeneratedCreatureData` — an Editor script, a test, a future runtime feature — can do `data.MeshResult.Positions.Add(...)` or call `data.MeshResult.ComputeAngleWeightedNormals()` again and mutate what the type's own contract promises is a fixed, already-validated result.

I traced the internal generation pipeline (`CreatureMeshGenerator.GenerateData`, `Generation/CreatureMeshGenerator.cs:44-57`) specifically to check whether this is exploited today: `BakeAppearance` (which calls `ComputeAngleWeightedNormals()` via `AppearanceBaker`) runs **before** `GeneratedCreatureData` is constructed, so nothing internal mutates `MeshResult` after handoff — this is not a live bug today. It is, however, a real gap between what the doc comment promises and what the type actually enforces, and it's the same "Mesh lifetime ownership" shape this project's own audit history has already flagged once (`TSK-0098` comment thread references "F-215 Mesh lifetime ownership" as `TSK-0104`'s scope). Worth a small, contained fix: either wrap `MeshResult` in a genuinely read-only adapter at the `GeneratedCreatureData` boundary, or narrow the doc comment to state plainly that `MeshResult` is handed off by reference and callers must not mutate it — whichever the team judges cheaper. Flagging it now, while it's a documentation/contract gap rather than a manifested bug, is exactly the point of doing this before something outside the pipeline starts holding onto `GeneratedCreatureData` for longer (which is the direction the animation-roadmap audit is already pushing the project).

---

## Note: not a broader pattern

Before flagging Finding 1 as a "recurring `*Tests.cs`/`*ContractTests.cs` split" pattern, I checked the other two places this naming pair exists (`MeshExtractionResultTests.cs`/`...ContractTests.cs` and `AppearanceBakerTests.cs`/`...ContractTests.cs`). Both pairs are consistent with their current source (both correctly expect `DomainException`, matching `MeshExtractionResult.ValidateTopology()` and `AppearanceBaker.Bake`'s actual current behavior) — so this is a one-off breakage specific to the `GeneratedCreatureData` hardening pass, not a systemic issue with the `*ContractTests.cs` convention itself.

---

## Recommended actions

1. **Immediate** — delete or fix `GeneratedCreatureDataTests.cs`'s three now-broken tests before the next Unity EditMode run reports red; `GeneratedCreatureDataContractTests.cs` already supersedes its coverage.
2. **Small decision, not urgent** — record whether `GeneratedCreatureData`'s null guards are deliberately `ArgumentNullException` or should be brought in line with the project's dominant `DomainException` convention; apply the decision to the three siblings that share the same choice if it changes.
3. **Small, contained fix, not urgent** — either make `GeneratedCreatureData.MeshResult` genuinely immutable at the handoff boundary, or narrow its doc comment so it stops promising a guarantee the type doesn't enforce.

No new `TSK-####` is strictly required for #1 (it's a direct, obvious test fix); #2 and #3 are small enough to fold into whichever task is already tracking `TSK-0095`'s remaining residue or `TSK-0104`'s generated-object-ownership scope, rather than opening new task records for them.
