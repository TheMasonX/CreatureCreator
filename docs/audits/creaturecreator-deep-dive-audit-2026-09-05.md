# CreatureCreator Deep-Dive Audit — 2026-09-05

**Scope:** Independent read-only review of `themasonx/creaturecreator` (`main`,
fixed point `20392e2` — "Remove obsolete production extraction oracle",
2026-09-05T05:57:30Z). ~29,300 lines of C# across 155 files. Cross-referenced
against `docs/tasks/active-tasks.md` (67 open `CC-###` records), the archive,
and the 42 prior `docs/audits/` documents (most recent:
`creaturecreator-deep-dive-audit-2026-09-04.md`) plus the
`2026-09-05-post-pr1-code-health-animation-rigging-handoff.md` next-round plan.

**Method note:** This project runs a mature, near-continuous audit process.
Per its own convention, this report does not re-litigate findings already
owned by an open task or already covered by the 2026-09-04 deep dive — it
only records deltas: definitive answers to questions that report/handoff
left open, confirmations that specific claims still hold against the current
tree, one correction to a previously-tracked issue (now fixed, verified by
tracing the actual call graph rather than trusting its doc comment), and one
new dead-code finding. Every line below is evidence I read directly.

---

## Executive Summary

The 2026-09-04 audit's H1/H2/M1/M2/L1 findings are all **still present and
unfixed** in the current tree — confirmed by direct re-check, not assumed.
Nothing new of that size turned up on a second pass; the remaining
uninspected surface (`CreatureEditorWindow.cs` viewport/drag code, placement
authoring, serialization) is unusually disciplined: no `TODO`/`FIXME`, no new
empty/broad catches, no scattered epsilon literals that aren't already
deliberate local degeneracy guards.

This report's contribution is narrow and concrete:

1. **B2c answered** (`2026-09-05` handoff, Track B2c): `SdfOperation.ConsumerUnionIndex`
   is confirmed dead — written at 5 call sites, read nowhere except a test
   equality assertion. Handoff already says "remove it if unused"; this is
   that inventory, done.
2. **B2d evidence**: the two anonymous influence-radius epsilon sites the
   handoff asks about, with exact line numbers.
3. **Correction to prior audit memory**: the CC-029/CC-018 "Add Part on a
   limb places the child at the limb root, not its tip" issue recorded in
   earlier session notes is **no longer accurate** — verified by tracing
   `CreaturePartWorldTransformResolver` directly, not by trusting the
   `AddNewPart` doc comment's claim. The child-at-tip frame is now correctly
   applied. No corresponding CC-### needs reopening.
4. Confirmation that H2 (mirror-matrix duplication) is still exactly as
   described — `MirrorUtility` remains the least-referenced of four
   identical `ReflectAcrossX` definitions.

No new CC-### ticket is recommended. Everything below fits inside existing
task scope or closes an explicitly open question from the current handoff.

---

## Findings

### F1 (New). `SdfOperation.ConsumerUnionIndex` is dead — answers open question B2c

**Confidence:** Confirmed (exhaustive case-insensitive repo-wide grep, including
non-`.cs` shader/compute sources — no hits outside the 5 sites below).

| Site | Evidence |
| --- | --- |
| Declaration | `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs:33` (`public int ConsumerUnionIndex;`) |
| Default init | `SdfProgram.cs:51` (`SdfOperation.Primitive`, sets `-1`) |
| Write (reset) | `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs:149` (`SetWorldAabb`, sets `-1`) |
| Write (assign) | `SdfProgramBuilder.cs:157` (`SetConsumer`) |
| Write call sites | `SdfProgramBuilder.cs:445, 500, 597, 693, 732` (5 call sites across whole-creature, mirrored, and Body-node compilation) |
| Only read | `Assets/Scripts/Tests/Runtime/SdfProgramBuilderTests.cs:100` — `Assert.AreEqual(whole.ConsumerUnionIndex, part.ConsumerUnionIndex)`, a parity check between two write paths, not a functional consumer |

No sampling, culling, or gradient-estimation code (Burst or managed) reads
this field. It is plumbed through every `SdfOperation` and set at 5 call
sites in the compiler for a consuming optimization that does not exist in
the current tree.

**Recommendation:** Per the handoff's own B2c instruction ("remove it if
unused; otherwise document and test the actual optimization consuming it") —
remove the field, `SetConsumer`, its 5 call sites, and the parity assertion
in `SdfProgramBuilderTests.cs:100`. This is a pure deletion, not a behavior
change (the field is write-only). Owner: CC-014 / CC-090 (B2c is explicitly
scoped there already).

### F2 (Evidence for open question). Anonymous influence-radius epsilon — two sites, B2d

The handoff's B2d asks whether "the anonymous epsilon is numerical padding or
proof margin" without naming sites. Both live in the same file:

| Site | Code |
| --- | --- |
| `SdfProgramBuilder.cs:183` | `return maxBlend + 1e-4f;` |
| `SdfProgramBuilder.cs:274` | `float childInfluence = operation.Parameters.x + 1e-4f;` |

Same literal (`1e-4f`) in both places, structurally suggesting one shared
semantic constant rather than two independent numerical choices — but I did
not trace whether both call sites feed the same downstream proof (culling
envelope vs. gradient estimation), which is exactly what B2d's naming
decision depends on. Flagging as the concrete starting point rather than
re-deciding it here.

**Confidence:** Confirmed (both literals located and quoted directly).
**Owner:** CC-014 / CC-090 per the handoff's existing B2d scoping.

### F3 (Correction — resolved, not a defect). CC-029/CC-018 "Add Part on a limb duplicates at the root, not the tip" is fixed

An earlier audit session recorded (and this session's prior-context memory
still carried) that `AddNewPart` in `CreatureEditorWindow.cs`, when invoked
with a Limb part selected, produced a child parented at the limb's root
joint rather than its tip, because
`CreaturePartWorldTransformResolver` had "no terminal-joint awareness."

Re-tracing the current implementation directly (not trusting either the old
note or the current code's own doc comment) shows this is now resolved:

- `CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace`
  (`Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs:265-273`)
  composes `Matrix4x4.Translate(ResolvedLimb.Resolve(p.Limb).TerminalSocket)`
  for every ancestor limb in the chain except the resolved part itself.
- `ResolveChildFrameToCreatureSpace`
  (same file, ~line 300) extends a limb part's own frame by its
  `TerminalSocket` specifically for the frame a *child* is authored in.
- `AddNewPart` (`CreatureEditorWindow.cs:950-969`) creates the new part at
  `TransformData.Identity` and relies on this resolver behavior — its doc
  comment's claim ("a new child's identity local transform already means
  'at the limb tip'") checks out against the resolver code, not just against
  the comment's own assertion.

**Confidence:** Confirmed by call-graph trace, both for the ancestor-chain
path and the direct-child path.
**Action:** None needed in the codebase. Recorded here only because a stale
"still broken" belief was carried in prior audit context and should not
propagate further — no CC-### references this as open, so nothing to
correct in the tracker itself.

### F4 (Confirmation, unfixed). H2 mirror-matrix duplication — re-verified against current tree

Re-ran the 2026-09-04 audit's H2 grep against the `20392e2` snapshot:

| Site | Still independent? |
| --- | --- |
| `MirrorUtility.cs:27` (`ReflectAcrossX`, canonical) | — |
| `SdfProgramBuilder.cs:57` (`CreatureMirrorAcrossX`) | Yes, still independent |
| `SemanticBoneResolver.cs:29` (`ReflectAcrossX`) | Yes, still independent |
| `CreatureMeshGenerator.cs:33` (`ReflectAcrossX`) | Yes, still independent |
| `SkeletonInferrer.cs:73` | Delegates to `SemanticBoneResolver.ReflectAcrossX` (not `MirrorUtility`) |

Identical to the 2026-09-04 finding — `MirrorUtility` is still the
least-referenced of four identical matrix definitions. No regression, no
progress. Restated here only to confirm currency for whoever picks up A5a
next, since the fix is a pure reference swap and cheap to schedule first as
the 09-04 audit already recommended.

**Confidence:** Confirmed. **Owner:** CC-090 (A5a), unchanged.

---

## Scope Not Re-covered

Consistent with the project's delta-audit convention:

- Did not re-derive H1 (shape-fallback cascade), M1 (quaternion quantize
  divergence), M2 (`IDnaSerializer` shallow module), or L1 (`CanonicalJsonWriter`
  doc drift) from the 2026-09-04 audit — spot-checked M1's two call sites only
  (`TransformData.cs:72`, `DefinitionCanonicalizer.cs:247`) and confirmed both
  still present verbatim.
- Did not do a full line-by-line pass of `CreatureEditorWindow.cs` (3,165
  lines, `CC-094` decomposition target) beyond the placement/drag and
  part-tree sections read for F3. No new defects found in the sections read.
- Did not run the Unity test suite, Burst compiler, or `dotnet build` —
  static reading only, per this project's own audit convention.
- Did not investigate Track A/B/C items already scheduled with explicit
  acceptance criteria in the 2026-09-05 handoff (A0–A7, B0–B1, C0–C7) beyond
  B2c/B2d above — those already have a concrete plan and don't need a second
  independent inventory.

## Existing Task / Handoff Impact

| Task / Section | Impact |
| --- | --- |
| Handoff Track B2c | **Closed by evidence.** `ConsumerUnionIndex` confirmed unused; recommend deleting it in the same slice as B2a/B2b rather than re-inventorying. |
| Handoff Track B2d | **Narrowed.** Both anonymous-epsilon sites identified (`SdfProgramBuilder.cs:183, 274`); still open whether they're one shared constant or two — that decision, not the location, is the remaining work. |
| CC-090 (A5a mirror wiring) | No change — H2 confirmed still open, still the cheapest first slice per 2026-09-04's own recommendation. |
| CC-018 / CC-029 | No ticket action — prior "root vs. tip" concern verified resolved in current code; nothing to reopen. |

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| F1 — `ConsumerUnionIndex` dead field | Confirmed |
| F2 — B2d epsilon sites located | Confirmed (location); shared-vs-distinct semantics still open |
| F3 — CC-029/CC-018 tip-placement now correct | Confirmed |
| F4 — H2 mirror duplication still unfixed | Confirmed |
