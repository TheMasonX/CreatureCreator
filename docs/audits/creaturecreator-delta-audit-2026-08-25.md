# CreatureCreator — Delta Audit (2026-08-25)

**Commit checked:** `1e1a575` ("Harden resolved morphology snapshots and anchor validation") — verified via fresh `git clone` of `main` and a full `diff -rq` of `Assets/Scripts/` and `docs/` against the tree used for the prior report. **Result: zero changes.** There have been no commits since the previous audit, so there is nothing to report as a code delta in the "what changed" sense.

Given that, this pass continues the deep dive into areas the first report didn't touch — `Runtime/Skeleton`, `Runtime/Animation/Ik`, `Runtime/Appearance` — and is scoped as an addendum to `creaturecreator-code-audit-2026-08-25.md`, the same relationship the project's own `audit` → `audit-addendum` docs already use. One headline finding below; everything else checked came back clean.

---

## 1. The creature-space X-reflection matrix is redefined from scratch in four places — including by the ticket that exists specifically to centralize it

`Runtime/Skeleton/MirrorUtility.cs` was introduced by **CC-071** ("Fix mirrored limb bone rotation") with an unusually careful derivation comment: it proves *why* `S·M·S` (conjugation) is the mathematically correct way to mirror a full rigid transform, not just why `S·M` works for a point. That ticket's own **Findings** section is explicit about the bug it fixed: *"Mirrored limb forward and up axes now use a directly reflected proper rotation basis instead of extracting `Matrix4x4.rotation` from an improper reflected matrix."* In other words, this exact 3×3 diagonal `diag(-1,1,1)` matrix has already caused one shipped, ticketed bug from being handled inconsistently across call sites.

Despite that, the base reflection matrix `Matrix4x4.Scale(new Vector3(-1f, 1f, 1f))` is **independently declared as a private field in four separate files**, three of which don't reference `MirrorUtility` at all:

| File | Field | Convention used | References `MirrorUtility`? |
|---|---|---|---|
| `Skeleton/MirrorUtility.cs` | `ReflectAcrossX` (private) | conjugate `S·M·S`, exposed via `MirrorAcrossXPlane()` | — (this *is* the utility) |
| `Skeleton/SkeletonInferrer.cs:74` | `ReflectAcrossX` (private) | left-multiply `S·M` (for a joint offset, documented as deliberately *different* from the conjugate — see below) | Yes, for `BuildBone` only (line 131); the chain-mirror path at line 196 uses its own local field instead |
| `Morphology/Sdf/SdfProgramBuilder.cs:57` | `CreatureMirrorAcrossX` (private) | left-multiply `S·M` | No — redeclares the matrix and re-derives the same left-multiply justification in its own doc comment |
| `Generation/CreatureMeshGenerator.cs:33` | `ReflectAcrossX` (private) | left-multiply `S·M` | No — its doc comment literally says *"matching the convention SkeletonInferrer uses... and the SDF compiler's"*, i.e. the author already knew this needed to match three other places and duplicated the literal anyway |

To be clear: **the math checks out at every site I read** — `SdfProgramBuilder`'s left-multiply convention is exactly what CC-041 (Done) added a rotated-transform parity test to lock down, and `SkeletonInferrer`'s split between conjugate-for-origin-bones and left-multiply-for-joint-offsets is correctly reasoned and documented, not a mistake. This is not a live bug report. It's a maintainability/DRY finding: the one quantity in the codebase that has already caused a real bug, and that three separate doc comments describe as "matching the convention elsewhere," is not actually *sourced* from one place — it's sourced from four, kept in sync by comment-cross-referencing and developer discipline rather than the compiler. `MirrorUtility` only centralizes the *conjugate* operation (`MirrorAcrossXPlane`); the raw reflection matrix needed for the left-multiply use cases was never factored out, so each left-multiply site re-derived it.

**Minor companion finding, same file:** `SkeletonInferrer.AppendLimbBones` (line 197) mirrors the up-hint vector with an inline `Vector3.Scale(upHint, new Vector3(-1f, 1f, 1f))` — a *third* independent spelling of the same `(-1,1,1)` literal within this one file (the class already has a `ReflectAcrossX` field two lines away that isn't reused here, presumably because `Vector3.Scale` and `Matrix4x4 *` aren't drop-in interchangeable, but the constant itself could still be shared).

**Recommendation:** promote the raw reflection matrix to a `public static readonly Matrix4x4 ReflectionAcrossX` (or similar) on `MirrorUtility`, and have `SdfProgramBuilder`, `CreatureMeshGenerator`, and `SkeletonInferrer` reference it instead of declaring their own private copies — this is a same-assembly change (all three live in `ProceduralCreature.Runtime.asmdef`; there's no Editor/Runtime boundary blocking it the way there was for the `IsLimbChainType` finding in the first report). Since the values are compile-time identical today, this is a zero-behavior-change cleanup, not a risky refactor — the value in doing it is entirely about the *next* change to mirror semantics (e.g. if symmetry ever needs a configurable plane, or CC-036's anatomical work touches this area again) landing in one place instead of needing a four-file, comment-cross-reference-driven audit like this one to even find all the copies.

I did not find an existing ticket for this — CC-041 and the CC-071 findings both confirm the *math* is correct at each site but neither flags the duplication itself.

---

## 2. Everything else checked in this pass — clean, no findings

For completeness, since this was framed as a continuation of the deep dive:

- **`Runtime/Animation/Ik/BoneChain.cs`** — cycle-detection via a `HashSet<string>` visited-set that throws immediately inside the walk (unlike `CreatureDefinition.HasParentCycle`'s deferred `ToDictionary`, see prior report §1.1). Appropriate here specifically because this is a "should-never-happen" defensive check on an already-validated, already-inferred skeleton, not a report-only validation pass — correct use of exception-as-guard given the different contract.
- **`Runtime/Appearance/PartAppearanceSampler.cs`** — well-documented, including an explicit "KNOWN SIMPLIFICATION" callout for its own single-nearest-part approximation rather than blending at seams (flagged honestly by its own author, not hidden). Correctly guards the CC-064 non-finite/culled-sample contract before using a distance value. No findings.
- **`Runtime/Skeleton/MirrorUtility.cs` itself** — the math and its derivation comment are sound (verified independently, not just trusted from the docstring).

---

## Summary table

| # | Finding | Type | Severity |
|---|---|---|---|
| 1 | Creature-space X-reflection matrix redeclared in 4 files instead of sourced from `MirrorUtility` | Duplication / DRY, latent risk given prior bug history (CC-071) | Medium — no live bug, but highest-risk quantity in the codebase to leave unsourced |
| 1b | A third inline spelling of the same `(-1,1,1)` literal inside `SkeletonInferrer` itself | Minor duplication | Low |
