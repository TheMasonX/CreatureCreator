# CreatureCreator — Round 32: Confirming the Malformed-IR Finding, and a Clean Serialization Sweep

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `4465848`
**Context:** an extremely thorough new audit (`current-head-hidden-defects-and-consolidation-council-audit-26-09-12-19-56-00.md`) landed, opening `TSK-0206`–`TSK-0211` and covering essentially the full generation/appearance/preview pipeline in one pass — including a generalized version of Round 19/20's redundant-evaluation findings (`TSK-0207`, "share generated-vertex correspondence between appearance and binding," which subsumes both). Given the breadth already covered, this round did two things: independently verified the single highest-confidence new claim rather than taking it on faith, and swept `Serialization/` — the one major folder this whole series hadn't touched yet.

---

## Independently confirmed: the malformed-IR `default: return 0f` finding is real, plus one detail worth adding to `TSK-0206`'s design

The new audit's Finding #1 (99% confidence) says `SdfProgramEvaluator`'s operation dispatch silently returns `0f` for an unrecognized `SdfOperationType`, which is dangerous specifically because `0f` is a *meaningful* SDF value (exactly on the surface), so a corrupted/invalid operation manufactures a phantom surface instead of failing loudly. Checked directly rather than trusting the confidence number:

```csharp
// SdfProgram.cs:227 (EvaluateOperation) and :265 (EvaluateSubtree) — both:
case SdfOperationType.Empty: return float.PositiveInfinity;
default: return 0f;
```

Confirmed exactly as described, in both traversal methods. Worth adding one concrete detail to whatever `TSK-0206` design lands: the *primitive* evaluator, one level down, already gets this right —

```csharp
// SdfProgram.cs:309 (EvaluatePrimitive)
default: return float.PositiveInfinity;
```

So there's an existing, already-used, semantically-correct convention in the same file for "this operation doesn't apply here" (`+Infinity`, matching how `Empty` is handled in the two outer methods) sitting right next to the two places that use the dangerous `0f` instead. If `TSK-0206`'s validation boundary doesn't fully eliminate the need for a fallback value in the hot evaluator (e.g., Burst code that can't throw), `+Infinity` is the value already established elsewhere in this exact file as the safe "no surface" signal — worth using that instead of inventing a new sentinel, and worth fixing the inconsistency between `EvaluatePrimitive`'s convention and `EvaluateOperation`/`EvaluateSubtree`'s either way, independent of the validation-boundary work.

---

## Serialization sweep: clean

Read `JsonDnaSerializer.cs`, `CanonicalJsonWriter.cs`, `MiniJsonReader.cs`, and `DnaDeserializationException.cs` in full (1329 lines total) — the one major Runtime folder not yet examined this series. Consistently disciplined: every optional/malformed field path throws `DnaDeserializationException` with a specific message, no silent defaults for required data.

One thing specifically worth checking given this round's other finding and Round 27's standing "duplicate path" invariant: `ReadOptionalBodyAppearance` has a genuine legacy-format branch (`verticalOffset` scalar, pre-`CC-034`, alongside the current `verticalCurve`). This is **not** a violation of the Round 27 invariant — that invariant is about internal code paths competing to do the same current-format computation; this is file-format backward compatibility, which legitimately needs to persist as long as old save files might still be loaded. Confirmed it's correctly one-directional: grepped `CanonicalJsonWriter.cs` for any legacy-format emission and found none — the writer only ever produces the current format, the reader accepts both. That's the right shape for this kind of compatibility code, not a finding.

No bugs, no duplication, no performance concerns found in this folder.
