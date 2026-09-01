# CreatureCreator — Delta Audit #10: Reconciliation Review (2026-08-31)

**Commit checked:** `df47a9f` ("Migrate task tracking to archive tools and consolidate resolved morphology consumers") — 3 new commits since the last audited tip (`1e1a575`), 133 files changed, +11397/-620. This is the first pass where implementation has actually started; prior passes (#1-9) all found the tip unchanged.

**Purpose of this pass, per your request:** not hunting for new code smells — checking whether the findings from the original report and delta audits #1-9 were **correctly interpreted and captured** in the resulting task board (CC-082 through CC-091, plus the archive/supersession pass). Method: traced each of my prior findings forward through the repo's own audit chain (there's an intermediate `creaturecreator-utility-consolidation-audit-26-08-30.md`, 930 lines, sitting between my reports and the current tickets) to the actual ticket text and, where implementation already happened, to the archived "Done" notes and their test evidence.

**Headline result: mixed, but for a good reason.** Everything traceable to a *precise* finding — an exact line, an exact root cause — was captured and implemented with excellent fidelity, in some cases fixed exactly as I specified down to the chosen approach. Two of my more structural findings did not make it into any ticket at all, and I can show exactly where they should have landed and didn't.

---

## Part A — Findings captured and implemented with high fidelity

### A.1 CC-082 / CC-083 / CC-084 — all three implemented exactly as diagnosed, not just "addressed"

These were my most precise findings (original report §1) — I didn't just describe symptoms, I named exact lines and, in CC-083's case, corrected the ticket's own diagnosis. All three are now archived as **Done**, with implementation notes I can check word-for-word against what I wrote:

- **CC-082:** archived note reads *"`CreatureDefinition.HasParentCycle` built `Parts.ToDictionary(p => p.Id, p => p)`, which threw `ArgumentException` on duplicate part Ids... Replaced the dictionary build with a tolerant first-wins lookup."* — exactly the root cause and exactly the fix shape I recommended (not reordering the validator's checks, which I explicitly warned would just move the crash rather than fix it).
- **CC-083:** archived note reads *"Root cause was a TEST helper masking the null parent: `ValidPart` coalesces a null `parentId` to `BodyId`... The validator's `ValidateParentsAndCycles` already reports `InvalidBodyParent`... fixed the test."* — this is my exact correction, adopted over the original ticket's own (wrong) diagnosis that the validator was at fault. This is the strongest evidence in this whole reconciliation pass that the correction was actually read and understood, not just filed: implementing it required *not* touching the file the original ticket named.
- **CC-084:** archived note reads *"Root cause: `CanonicalJsonWriter.WritePart` substituted the part Id when `DisplayName` was blank... Changed the write to `WriteNullableField(...)`, which emits `null` verbatim."* — the exact line I pinpointed, and the exact one of my two proposed options ("preserve intent: omit the key / leave it null" over "make the substitution a documented one-way migration").

All three: full PlayMode suite green (428/428) at time of archival. This is as clean a confirmation as an audit can get — three independent, falsifiable, line-level claims, all verified correct by someone actually making the change.

### A.2 CC-089 — correctly generalizes CC-082/083/084 plus the exception-as-control-flow finding

CC-089 ("Make malformed-definition validation and cloning total") folds the three fixes above into a broader validation-totality ticket, and separately captures original report §5 (exception-driven control flow in `ValidateResolvedEnvelope`) precisely: *"Prefer non-throwing `TryResolve` paths for validator-only resolved-envelope checks, so routine incomplete authoring data does not use exceptions for control flow."* Correct interpretation, correctly scoped as still-open work (still Backlog).

### A.3 CC-090 — a genuinely faithful synthesis of five separate reports

CC-090 ("Consolidate shared runtime utilities and tolerances") is the ticket most directly downstream of my consolidation-focused passes, and its scope list maps cleanly onto specific reports:

| CC-090 scope line | Source |
|---|---|
| "Centralize finite checks and named linear/squared degenerate tolerances" | Delta #6 (epsilon census), delta #7 (`IsFinite` triplication) |
| "`MinSpacingSqr` is compared only with squared magnitudes, or is removed in favor of a resolved metric" | Delta #2 (the bug) + delta #3 (fold into `ResolvedPolyline` rather than patch in place) |
| "Centralize mirror-point/reflection primitives in a dependency-neutral Common location" | Delta #1 + delta #9 — and "dependency-neutral" specifically reflects delta #9's correction that `MirrorUtility`'s home in `Runtime/Skeleton` was the wrong layer once `Morphology/Sdf` needed it too |
| "Move shared `PartType` classification, including `IsLimbChainType`, into the Runtime-owned contract" | Original report §4 (the Editor/Runtime asmdef-boundary duplication) |
| "Decide whether the unused sibling-order strategy is deleted or made production-configurable" | Delta #5 |

That "dependency-neutral Common location" phrase is worth calling out specifically — it means whoever wrote CC-090 didn't just read delta audit #1's headline finding, they read the follow-up correction in delta audit #9 that refined it. That's the level of fidelity this reconciliation was checking for, and it's there.

**One imprecision worth flagging back:** CC-090 phrases the sibling-order item as an open decision — *"decide whether... deleted or made production-configurable."* You told me directly the toolbar-toggle wiring is the plan; CC-090 as currently worded doesn't reflect that a decision was already made. Small, easy to fix (update the scope line to state the toggle direction rather than posing it as a choice), but worth mentioning since you asked specifically about correct capture.

### A.4 CC-076 / CC-056B — the nearest-body-sample-binding finding, correctly resolved via a different mechanism than I proposed, and now Done

Delta audit #3 (§A.5) flagged `SkeletonInferrer.ResolveBodyParentBoneId`'s linear nearest-neighbor scan as density-dependent bone binding. CC-076 (now archived, Done) resolves this not by patching the scan but by routing bone-socket resolution through CC-056B's canonical resolved-attachment layer instead — a better fix than a local patch would have been, since it removes the nearest-sample search as a category rather than tuning it. Correctly captured and correctly *improved on*, not just implemented literally.

---

## Part B — Two findings that did not make it into any ticket

I traced these specifically because their absence from the ticket-name grep was suspicious enough to double check against the raw archived audit files (confirming the findings themselves are still sitting, unreferenced, in `docs/audits/creaturecreator-delta-audit-4-2026-08-25.md` and `-8-2026-08-25.md`) rather than just poorly indexed.

### B.1 `CreatureEditorWindow.cs`'s god-class decomposition (delta audit #4) — no ticket references it

I searched every ticket (active and archived) for the specific class names I proposed (`BodyViewportController`, `PartInspectorPanel`, `PartHierarchyPanel`, `PreviewGenerationController`) and for the file/decomposition itself — zero matches anywhere except my own source report. The one ticket that could plausibly be mistaken for covering this, **CC-091 ("Establish concrete generation pipeline stage boundaries")**, is about a different file entirely — `CreatureMeshGenerator.Generate()`, the *runtime* generation pipeline, which the newer 08-30 audit independently and correctly flagged as its own "God-method" (its §14). That's a real, valid, separate finding — but it means `CreatureEditorWindow.cs`, the 2850-line, ~150-member **authoring UI** god class, currently has no ticket at all. It's referenced as a file link in ~25 feature tickets (anything that touches the editor mentions it, incidentally), but none of them propose decomposing it.

### B.2 `BoxSdfNode`'s missing NaN/Infinity check (delta audit #8) — no ticket references it

Searched for `BoxSdfNode`, `PrimitiveNodes.cs`, and "half-extent" across every ticket and audit doc. The only two hits (`CC-043`, `CC-067`) reference `BoxHalfExtents` as an ordinary field name in unrelated per-shape-parameter and SDF-bounds-visualization work — neither touches the constructor validation gap. `CC-089` covers validation totality, but exclusively at the DNA/`DefinitionValidator` layer (`CreatureDefinition`, `DefinitionCanonicalizer`), not at the `ISdfNode` primitive-constructor layer where this gap actually lives. This is a small, cheap fix (one shared `RequirePositiveFinite` helper call, four sites) that's fallen into a genuine gap between "DNA validation" tickets and "SDF compiler" tickets — it belongs to neither existing bucket cleanly, which may be exactly why it got dropped.

### Why these two specifically, and not others

Both are the kind of finding that's easy to lose in a synthesis pass because they don't fit either of the two "big theme" buckets everything else got sorted into — consolidation (mirror math, epsilons, adapters) and validation-totality (CC-082/083/084/089). They're each a one-off, standalone observation about a single file, and standalone single-file findings are exactly what a multi-pass synthesis is most likely to drop when it's organizing around themes rather than checking every prior finding off a list one at a time. That's a process observation, not a criticism of any specific pass — it's the predictable failure mode of "synthesize five audits into a coherent set of tickets," and it's exactly what this reconciliation pass exists to catch.

---

## Recommendation

Two small tickets to add, matching this repo's existing conventions:

- **A CC-092-equivalent** for the `CreatureEditorWindow.cs` decomposition, using delta audit #4's cluster map directly as the scope (it's already broken into a 5-step, independently-shippable extraction order) — link `docs/audits/creaturecreator-delta-audit-4-2026-08-25.md` directly rather than relying on it being re-derived from a future synthesis pass.
- **Fold the `BoxSdfNode` fix into CC-090** (it's a natural fit alongside the other small-utility extractions already scoped there — `RequirePositiveFinite` belongs right next to the `IsFinite` consolidation CC-090 already covers) rather than opening a separate ticket for one four-site fix.

---

## Summary table

| # | Finding | Verification result |
|---|---|---|
| A.1 | CC-082/083/084 | Implemented exactly as diagnosed; Done, test-verified |
| A.2 | Exception-as-control-flow (orig. report §5) | Correctly captured in CC-089, still open |
| A.3 | Mirror/epsilon/IsFinite/IsLimbChainType/sibling-order consolidation | Correctly, faithfully synthesized into CC-090, including a delta-#9-level correction; one phrasing imprecision on the sibling-order decision |
| A.4 | Nearest-body-sample binding (delta #3 §A.5) | Correctly resolved via CC-076/CC-056B, improved on rather than literally patched; Done |
| B.1 | `CreatureEditorWindow.cs` god-class decomposition (delta #4) | **Not captured in any ticket** |
| B.2 | `BoxSdfNode` NaN/Infinity validation gap (delta #8) | **Not captured in any ticket** |
