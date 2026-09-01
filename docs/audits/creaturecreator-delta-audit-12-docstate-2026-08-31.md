# CreatureCreator — Delta Audit #12: Doc-State vs Repo-State (2026-08-31)

**Commit checked:** `df47a9f` — unchanged since delta audits #10-11.
**Scope this pass:** per your request, specifically hunting doc-state-vs-repo-state mismatches, given the large CC-087/CC-088 migration that just landed. Method: pick a claim directly out of an archived "Done" ticket's Findings section, then grep the actual current code to check it's still true exactly as stated, not just true-ish.

---

## 1. Verified true: the nearest-Body-sample linear scan is genuinely gone, not just rerouted in prose

CC-087's archived notes claim: *"Body-parent bone binding now resolves sample IDs and positions through `ResolvedBody`... instead of scanning raw `BodySample` instances."* Checked directly — `SkeletonInferrer.cs` no longer contains the `nearestIndex`/`nearestDistance` linear-scan pattern from delta audit #3 (§A.5) at all; grepping for it returns nothing. This is a real deletion, not a rename — the code that produced the density-dependent bone-binding risk I originally flagged is actually gone from the file. Good confirmation, no action needed.

## 2. A genuine doc-precision gap: CC-088's Findings text overclaims relative to its own Scope

CC-088's Scope and Acceptance Criteria are careful and consistent throughout: *"Remove `PrimarySize` fallback from valid **current-schema generation**,"* *"No normal **production** generation path evaluates managed `ISdfNode`,"* *"Keep managed SDF code only for explicit reference parity until evidence permits deletion."* — every one of these correctly scopes the `PrimarySize` removal to the portable/production compile path, explicitly carving out the managed `ISdfNode`-tree path as intentionally untouched (that's CC-045's job, still "In Progress").

But its **Findings** section states, flatly: *"The compiler no longer reads `PrimarySize`."* No qualifier. Read on its own — which is exactly how a future audit, a new contributor, or a reconciliation pass like this one would encounter it — that sentence claims something broader than what actually happened.

**Checked against current code:** `SdfProgramBuilder.cs` still contains four `PrimarySize` reads, at lines 820, 824, 828, 834 — inside `CompilePrimitive`, called from `CompilePart` → the public `Compile(CreatureDefinition)` entry point (the managed `ISdfNode`-tree compiler). The portable path, `CompilePortable(CreatureDefinition)`, is a fully separate ~340-line method in the same file and genuinely has zero `PrimarySize` references — so the claim is **true for the path CC-088 actually targeted**, and the code is not regressed or buggy. This is a wording gap, not a functional one, and it's already covered by an existing ticket: CC-045 ("Remove the legacy managed SDF from production generation," still In Progress) explicitly owns deleting the entire `Compile()`/managed path, which will take these four `PrimarySize` reads with it when it lands.

**Why flag a wording-only gap:** this is precisely the failure mode a "verify docs match repo" pass exists to catch — a technically-narrow claim, written in a way that reads as broad, in a **Done** ticket. Someone who trusts the Findings text at face value and later greps `SdfProgramBuilder.cs` for `PrimarySize` (as I just did) will find four hits and reasonably wonder whether CC-088 actually shipped, or regressed, or was only partially done. It wasn't any of those — it did exactly what it said in the Scope — but the Findings prose doesn't carry the same qualifier the rest of the ticket is careful about. A one-word fix (*"The **portable** compiler no longer reads `PrimarySize`"*) would close this cleanly and keep this exact confusion from recurring the next time someone reconciles this ticket against the code.

---

## Summary table

| # | Check | Result |
|---|---|---|
| 1 | CC-087's claim that nearest-Body-sample scanning is replaced by `ResolvedBody` | **Verified true** — the linear scan is fully deleted from `SkeletonInferrer.cs`, not just superseded in comments |
| 2 | CC-088's Findings claim "the compiler no longer reads `PrimarySize`" | **True only for the portable path** (`CompilePortable`); the managed path (`Compile`) still has 4 reads, correctly scoped to CC-045's still-open work — but the Findings sentence doesn't carry the qualifier its own Scope/Acceptance Criteria consistently use, creating a real doc/repo reading-mismatch risk even though the underlying implementation is correct |
