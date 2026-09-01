# CreatureCreator — Delta Audit #11: Reconciliation Review, Part 2 (2026-08-31)

**Commit checked:** `df47a9f` — unchanged since delta audit #10, re-verified via `git fetch`.
**Scope this pass:** continuing the reconciliation sweep from delta audit #10 over the findings not yet checked — original report §3 and §6, and delta audit #4's two secondary findings.

---

## 1. Original report §3 (`PartType.Limb`/no-chain validation gap) — captured with unusual precision

`CC-036` ("Anatomical limb parent validation") is nominally about a different, related rule (Hand-must-be-under-Arm, Foot-must-be-under-Leg), but its Scope section explicitly folds in the exact gap I found:

> *"Extend the same validation pass to define the reverse invariant: `Limb`, `Leg`, and `Arm` parts must either carry a `LimbChain` or use an explicitly documented supported fallback. **Retype the demo head fixture if it is a primitive rather than a joint chain.**"*

That last sentence is my exact, specific recommendation from the original report (§3) — not a generic "add validation," but the precise fix for the exact fixture (`CreatureRuntimePreview.CreateDemoDefinition`'s mislabeled head) I traced by hand. Correctly captured, still open (Backlog).

## 2. CC-088 — the largest, most structurally complex finding in the whole series, implemented in exactly the sequence I recommended

This deserves its own callout beyond delta audit #10's brief mention, because it's the best evidence yet of deep engagement rather than surface synthesis. Delta audit #3 (§A.1, A.2, A.4) made three linked claims:
- `SdfProgramBuilder` is a second morphology engine because it never holds a `ResolvedLimb`, only ever constructs one transiently inside `LimbMetaballSampler`.
- `PrimarySize` is read directly inside `SdfProgramBuilder` at six call sites, not just three sites elsewhere.
- Deleting `LimbMetaballSampler.Sample(LimbChain)` is **not safe** until `SdfProgramBuilder` is restructured to receive resolved geometry first — the two aren't independent parallel tasks, the second is a hard prerequisite for the first.

CC-088's Findings section, now archived Done: *"The portable compiler now receives one `ResolvedCreatureSnapshot`... Current-schema authored sphere dimensions are independent of `PrimarySize`... The compiler no longer reads `PrimarySize`. Limb union blend radius is resolved with the limb snapshot, **and the raw `Sample(LimbChain)` overload is removed**."* The ordering in that sentence — resolved-snapshot consumption first, overload removal after — matches the dependency I flagged exactly. Verified with a dedicated regression test named directly after the bug class (`CompilePortable_CurrentSchemaSphere_IgnoresLegacyPrimarySize`) and 85/85 on the broader PlayMode selection. This is the single most faithful implementation-to-finding match in the whole audit series.

## 3. Original report §6 minor items — genuinely still open, and still low priority

Two small items from the original report's "smaller items worth a line each" section remain unaddressed in the current code, and neither has a ticket:

- **`DefinitionCanonicalizer.CanonicalizeShape`'s `CapsuleHeight` fallback is still a hardcoded `1f`**, not `legacySize` like every other field in that block — confirmed still present at `DefinitionCanonicalizer.cs:115` (`if (shape.CapsuleHeight <= 0f) shape.CapsuleHeight = 1f;`) even after CC-088's migration. This makes sense — CC-088 was scoped around removing `PrimarySize` reads from the *compiler*, not auditing every fallback value inside the canonicalizer itself, so this one small inconsistency wasn't in its blast radius.
- **`ShapeDefinition.HasValidParameters()` is still type-blind** — confirmed still requiring `Radius > 0f && CapsuleHeight > 0f` unconditionally regardless of `Type`, at `ShapeDefinition.cs:61`.

Both are exactly as low-severity as I originally scored them ("worth a comment at minimum," not a functional bug given every real construction path already populates all four fields) — I'm not elevating them, just confirming they're still sitting there, genuinely unaddressed rather than fixed-but-unlinked. Worth a one-line mention next time `ShapeDefinition`/`DefinitionCanonicalizer` gets touched for something else, not worth a standalone ticket on their own.

## 4. Delta audit #4's two secondary findings — not referenced anywhere, one is plausibly covered incidentally

- **`FindBodySample`'s LINQ `.First()` exception-type inconsistency** (throws `InvalidOperationException` instead of the codebase's consistent `DomainException` convention) — no ticket references it, and it's still present in `CreatureEditorWindow.cs`. Genuinely dropped, though it's the lowest-severity finding in the entire series (never observed to fire) — reasonable to leave off the board entirely rather than open a ticket for it.
- **`CurrentBodySpacing`'s local re-derivation of `ResolvedBody`'s segment-length math** — no ticket names it directly, but `CC-087`'s acceptance criteria include *"Geometry, skeleton, bounds, and editor placement use the same resolved frame and world transform,"* which is adjacent enough that this specific duplication would likely get swept up as a side effect once editor code consumes `ResolvedBody` for placement generally. Not a confirmed capture, but not a clean drop either — worth checking back once CC-087 lands to see if `CurrentBodySpacing` got naturally subsumed or is still sitting there afterward.

---

## Running total across both reconciliation passes

| Severity | Status | Count |
|---|---|---|
| Major finding, precisely implemented | Done, verified against archived notes | CC-082, CC-083, CC-084, CC-088, CC-076 |
| Major finding, faithfully scoped | Correctly captured, still open | CC-036 (§3), CC-089 (§5), CC-090 (consolidation series) |
| Structural finding, not captured | **Genuine gap** | `CreatureEditorWindow.cs` decomposition (delta #4), `BoxSdfNode` validation gap (delta #8) |
| Minor finding, not captured | Low-priority, reasonable to leave unticketed | `CapsuleHeight` fallback, type-blind `HasValidParameters`, `FindBodySample` exception type |
| Minor finding, plausibly incidental capture | Worth re-checking after CC-087 lands | `CurrentBodySpacing` re-derivation |

The pattern holds from delta audit #10: everything that was a **precise, single-root-cause finding** — no matter how structurally deep (CC-088 is the biggest evidence of this) — made it through synthesis intact. What gets lost is **small, standalone, single-file observations that don't anchor a theme** — the two real gaps from #10, and now the smaller items here, are exactly that shape. That's a useful thing to know about how to hand off future audit batches: findings with their own natural ticket boundary survive synthesis; footnote-sized findings need to be explicitly pulled into an existing ticket's scope line by name, or they fall through.
