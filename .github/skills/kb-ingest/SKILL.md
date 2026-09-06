---
name: kb-ingest
description: |
  Ingest verified knowledge into the MemorySmith knowledge base (Data/Memories)
  as durable MemoryRecord JSON, linked into the record graph with cited,
  evidence-checked source links. Enforces "knowledge base, not belief base":
  every claim is source-verified and peer-reviewed before capture, confidence
  reflects evidence quality, and open questions are recorded, not hidden. Use
  for onboarding/architecture capture, learned-pattern capture, schema/ADR
  distillation, codebase map creation, or any durable-knowledge write to the
  CreatureCreator MemorySmith wiki. MemorySmith-only; never edits code or tasks.
argument-hint: 'Topic or slice to capture, such as the SDF generation layer'
user-invocable: true
disable-model-invocation: false
---

# MemorySmith Knowledge-Base Ingestion (kb-ingest)

## Outcome

Produce durable, linked MemoryRecord entries under `Data/Memories/` that let a
future agent reconstruct correct context from retrieval alone, without
re-researching the codebase. Every record must be:

- **Evidence-verified**: each material claim traces to a specific source file,
  test, ADR, or observed behavior the author actually opened.
- **Source-linked**: `SourceLinks[]` cite exact repository-relative paths (and
  lines when meaningful), resolving through the configured variable/root store.
- **Graph-linked**: `References[]`/`Conflicts[]`/`Relationships[]` connect each
  record to the records it builds on or disputes.
- **Peer-reviewed**: no record is written as authoritative until reviewed for
  unsupported, speculative, or stale claims.
- **Rigor-transparent**: confidence is a real number justified by evidence
  quality; uncertainty, gaps, and assumptions are stated, never silently
  resolved into false certainty.

The operating standard is **knowledge base, not belief base or hunch base**.
Never take a claim at face value. Verify it yourself. Do not promote a
plausible mechanism to confirmed without direct evidence.

## When to Use

Use this skill when the user asks to:

- capture codebase architecture, layer maps, or onboarding knowledge into the
  MemorySmith wiki;
- record a verified pattern, decision, invariant, or learned lesson;
- distill a schema, ADR, audit, or test surface into searchable memory;
- review or correct an existing memory record for accuracy and provenance;
- extend the record graph (cross-references, conflicts) for a topic.

Do **not** use it to track work items (use `task-tracker`), reconcile audits
into tasks (use `cc-audit-synthesis`), or change code (use `creature-workflow`).

## System and Repository Contracts

Read these before writing:

- `Data/Memories/README.md` for the store layout and intent.
- This workspace's wiki instance is `mcp_memorysmithwi_*` (service
  "MemorySmith - CreatureCreator Wiki", port 7916). Durable storage is JSON
  files under `Data/Memories/<Status>/`, one file per record named `<Id>.json`.
- `.github/skills/cc-audit-synthesis/SKILL.md` and `.github/skills/council/
  SKILL.md` for the peer-review and evidence standards used here.
- `.github/skills/task-tracker/SKILL.md` only if the capture should reference a
  task; knowledge capture itself never edits task records.
- `Assets/Scripts/README.md`, `docs/adr/`, and the cited source before claiming
  any CreatureCreator behavior.

CreatureCreator invariants that must remain true in any captured claim:

- `CreatureDefinition` is authoritative DNA; meshes, colors, skeletons, poses
  are derived and never written back.
- Runtime code (`ProceduralCreature.Runtime`) has no scene-object/editor-API/
  mutable-generated-state dependency; editor (`ProceduralCreature.Editor`)
  owns sessions, undo, previews, and editor lifecycle.
- `DefinitionValidator` reports and never repairs; `DefinitionCanonicalizer`
  owns quantization (4 dp) and stable ordering.
- SDF negative-inside / positive-outside. Symmetry stored once per flagged
  part; no cascading mirror.
- Record documented simplifications as facts, and flag anything that appears to
  violate an invariant as a conflict to resolve, not a silent override.

## MemoryRecord Schema (authoritative)

A record is one JSON file `Data/Memories/<Status>/<Id>.json`:

| Field | Meaning |
|---|---|
| `Id` | Stable slug, lowercase-hyphen, matches filename. |
| `Title` | Short, human-readable. |
| `Content` | Durable current-state facts and rules. |
| `Status` | int: `0` Unconsolidated, `1` Working, `2` Core, `3` Deprecated. |
| `Confidence` | double `0.0`-`1.0`; evidence quality, not impact. |
| `Tags` | include scoped tags plus topic tags. See **tag rules** below; `kind:*` is an enum and `kind:reference` is NOT valid. |
| `References` | `Id`s this record builds on or is linked to (graph edges). |
| `Conflicts` | `Id`s this record contradicts or supersedes. |
| `Relationships` | typed edges (relation, origin) for richer graph semantics. |
| `SourceLinks` | `[{Label, Uri, StartLine?, EndLine?}]`; `Uri` may use `%Variable%` tokens from the variable store. |
| `UsageCount` | `0` on create. |
| `LastUpdated` | ISO timestamp. |

Status folders: `Unconsolidated`, `Working`, `Core`, `Deprecated`. The running
service's state machine (`MemoryStateMachine`) may auto-demote a fresh
low-usage record from `Core` to `Working`; that is normal, not a defect.
Author high-confidence durable facts with intent for `Core` and let review +
usage promote them; use `Working` for provisional or lower-confidence content.

## Tag and JSON rules (learned, mandatory)

These rules come from real ingestion failures. Follow them exactly; ignoring
them silently corrupts or degrades records.

**Tag rules**
- `kind:` is an **enum with a fixed allowlist**. Allowed values only:
  `fact`, `rule`, `procedure`, `decision`, `plan`, `research`, `guide`,
  `concept`, `issue`, `example`, `index`.
  `kind:reference` is **NOT allowed** and raises a
  `tag.invalid_namespace_value` Warning. Use `kind:fact` for durable
  slice/mechanism records, `kind:index` for an onboarding index record that
  links to the slice records. Do not invent new `kind:*` values.
- `scope:`/`audience:` namespaces and plain topic tags are open (no strict
  allowlist here). Plain topic tags that are unlisted produce only an Info
  `tag.unknown_plain` diagnostic; that is acceptable.
- Keep the `kind:index` record minimal (a map + links); put the actual content
  in `kind:fact` layer records. Avoid making every record an index.

**JSON casing rules (critical)**
- The wiki deserializes records with **case-sensitive JSON** (System.Text.Json).
  Author every field in **PascalCase** exactly: `Id`, `Title`, `Content`,
  `Status`, `Confidence`, `Tags`, `References`, `Conflicts`, `Relationships`,
  `SourceLinks`, `UsageCount`, `LastUpdated`.
- `SourceLinks` must be an array of **objects** `{ "Label": "...", "Uri":
  "Assets/...", "StartLine": null, "EndLine": null }`. Never a bare string
  array and never camelCase keys.
- If you write camelCase (`id`, `sourceLinks`, `uri`) or string-array source
  links, the running service silently re-serializes and **empties** those
  fields (you see empty `SourceLinks` and `source.unresolved` warnings). This
  is the single most common authoring defect. Always emit PascalCase.
- After the service indexes a record it may rewrite the file canonically (add
  `Relationships[]`, prefix `SourceLink.Label` with `other:`, and move the file
  between status folders). Expect and accept that; do not re-litigate the move.
- Prefer a `Relationships[]` graph plus `References[]`; if you only maintain
  `References`, the service synthesizes typed `Relationships` from it.

## Procedure

### 1. Query Before You Write

Search the existing KB first so you do not duplicate coverage. The
[kb-query](../kb-query/SKILL.md) skill owns the full read-only retrieval
procedure (tool choices, lexical-only caveats, graph-following); here is the
short version:

- `mcp_memorysmithwi_memorysmith_search` / `_hybrid_search` for the topic.
- `_get` any near-match by `Id` to read its full content and references.

If an existing record already covers the mechanism, extend it with new
evidence and references rather than creating a duplicate. Record the 
duplicate-avoidance check in your notes.

### 2. Bound the Slice and Collect Evidence

Scope one coherent slice per pass. A proven division of labor is **one coarse
layer record + one finer mechanism record per layer**. Coarse layer records
(good targets): definition model, generation/morphology, skeleton/IK/pose,
editor/preview, tests/ops. Finer slice records (good targets): mutation/clone/
hierarchy + validation surface, serialization contract, SDF+extraction
internals, resolved-model/world-resolver seam, skeleton/IK/rig details, editor
authoring+preview state machine.

For large, multi-layer capture use the [subagent-swarm](../subagent-swarm/
SKILL.md) workflow (or a few read-only research subagents) so each track opens
its own slice in parallel and returns an evidence-verified draft. Subagents
must NOT write to the repo; they write drafts to a recovery workspace and the
coordinator reconciles, normalizes, and authors the final records. For the
slice, open directly:

- the cited source files (read the real file, not a summary);
- the nearest tests that assert the claimed behavior;
- the matching ADR under `docs/adr/`;
- the matching task record when a behavior is task-owned.

Record the repository state (branch or working-tree baseline) if behavior
could depend on it. Never cite a file you did not open.

### 3. Verify Each Material Claim

Treat every statement as unverified until checked against source. For each
material claim:

1. Open the cited file and trace the relevant symbol or path.
2. Read the nearest focused test to confirm the asserted behavior.
3. Read the ADR/handoff when the claim concerns a contract or boundary.
4. Capture exact repository-relative paths (and line references when useful).
5. Classify the claim as one of: `Confirmed`, `Partially confirmed`,
   `Inference`, `Unverified`, `Refuted`, or `Open question`.

Separate **observed fact**, **inferred risk**, and **open question** in the
draft. A `Confirmed` claim requires direct source or test evidence. An
`Inference` must be labeled as such and carry lower confidence. Never promote
an inference to a fact because it sounds plausible.

Confidence guidance: `>=0.9` only with direct source + test confirmation;
`0.7-0.9` well-sourced but partly inferred or from a stale doc; `<0.7`
mostly inferred or single-source. Explain non-trivial values in the record.

### 4. Peer-Review the Draft

Before writing as authoritative, review the draft for unsupported or stale
claims. For high-impact or cross-cutting captures, run a multi-seat review
following the [council](../council/SKILL.md) skill (default 3 seats). For
ordinary captures, apply the same scrutiny in-line:

- **Accuracy**: is every claim directly supported by evidence you opened?
- **Completeness**: are simplifications, invariants, and gaps captured?
- **Currency**: is the claim consistent with the current source and ADRs, or
  does it rely on a known-stale document?
- **Transparency**: are assumptions and open questions explicit?
- **Boundary integrity**: does any claim imply a runtime/editor, validator/
  canonicalizer, or authoritative/derived violation that was not verified?

Record the reviewer verdict and any dissent. Do not flatten disagreement; note
the evidence that would change the outcome.

### 5. Author the Record

Write one record per bounded slice or mechanism. Keep `Content` dense but
readable; prefer a self-contained block over many tiny records. Set:

- `Id` matching the intended filename (lowercase-hyphen).
- `Status` per the tier guidance above.
- `Tags` per the **tag rules** above: `kind:fact` (or `kind:index` for the
  onboarding index) plus `scope:onboarding`/`audience:agent` where appropriate
  plus topic tags. Never use `kind:reference`.
- `References` to the `Id`s it builds on (for example, an architecture record
  references each layer record; a layer record references the architecture and
  definition records). This is how the record is linked into the graph.
- `Conflicts` when the record corrects or supersedes an existing claim.
- `SourceLinks` as **PascalCase objects** `{Label, Uri}` with exact
  repository-relative paths (for example
  `Assets/Scripts/Runtime/Definition/CreatureDefinition.cs`) or `%Variable%`
  tokens if the variable store defines them. Cite the files you actually read.
- `Confidence` reflecting evidence quality per the rubric above.
- `LastUpdated` to the capture date.

When authoring from subagent drafts, **normalize every draft** to the exact
PascalCase schema and object-typed `SourceLinks` above; do not copy a draft
verbatim if it uses camelCase keys or string source links (see **JSON casing
rules**). Reconcile across drafts to remove duplication and confirm every
`References`/`Conflicts` target exists before writing.

If MCP `memory_create` is available and auto_accept is enabled, prefer it. When
tool writes are disabled (auto_accept off), author the JSON file directly in
the matching `Data/Memories/<Status>/` folder — the running service indexes it.
Either way the file must deserialize with the schema in this skill.

### 6. Validate

After writing, run focused read-only checks:

- the record is valid JSON and matches the schema (fields, types, Status int);
- the `Id` matches the filename;
- every `References`/`Conflicts` target resolves to an existing record `Id`;
- every `SourceLink.Uri` points at a file that exists and that you opened;
- the record is discoverable: `mcp_memorysmithwi_memorysmith_search` for a
  distinctive phrase returns it;
- no duplicate record was created;
- `git diff --check` passes.

Note (do not silently fix) the common diagnostics on authored records:

- `tag.unknown_plain` (Info): plain topic tags outside an allowlist. Harmless
  when no allowlist is configured; add one only if strict governance is wanted.
- `tag.invalid_namespace_value` (Warning): a namespaced tag uses a value the
  policy disallows — most commonly `kind:reference`. Fix the tag to an allowed
  `kind:` value (see **tag rules**), do not ignore.
- `source.unresolved` (Warning): `SourceLink.Uri` outside configured allowed
  source roots, or an empty Uri. If the Uri is empty the record was authored
  with camelCase/string source links — **repair to PascalCase objects**. If
  the Uri is a valid path but outside allowed roots, records still load and
  search; resolve by adding a `%Variable%` token or expanding allowed roots
  only if source navigation matters.

Do not claim semantic-search behavior: the ONNX model under `Data/Models/` may
be absent, so retrieval can be lexical-only. Verify what the instance actually
returns before asserting search quality.

### 7. Close the Loop

Report the records created or extended, the evidence reviewed, the reviewer
verdict, residual uncertainty, and the next capture step. Record any learning
about the ingestion process in repository memory so the process improves. Do
not commit or create a branch unless the user explicitly requests it.

## Completion Checklist

- [ ] Existing KB was queried and duplicates were avoided (extended, not copied).
- [ ] Every cited source file and its nearest test/ADR were actually opened.
- [ ] Every material claim has a verification verdict; inferences are labeled.
- [ ] `Confidence` reflects evidence quality and non-trivial values are explained.
- [ ] Record is graph-linked: `References`/`Conflicts` resolve to existing `Id`s.
- [ ] `SourceLinks` are **PascalCase objects** `{Label, Uri}` citing exact paths
      the author read (no camelCase, no string arrays).
- [ ] JSON is PascalCase (`Id`, `Title`, `Content`, ...) and deserializes.
- [ ] Tags use only allowed `kind:` values (`kind:fact`/`kind:index`; never
      `kind:reference`).
- [ ] Peer review ran (council seats for high-impact, in-line scrutiny otherwise)
      and dissent/uncertainty is preserved.
- [ ] Invariants and documented simplifications are stated as facts; violations
      are raised as conflicts, not silently overridden.
- [ ] Record is discoverable via search and passes the schema/`git diff --check`
      validation.
- [ ] Assumptions, gaps, and next evidence are recorded.

## Example Prompts

- `/kb-ingest capture the authoritative DNA and serialization layer as an onboarding record`
- `/kb-ingest verify and extend the existing generation/morphology record with current source evidence`
- `/kb-ingest peer-review my draft capture for the editor preview state model before I write it`
- `/kb-ingest reconcile the recorded invariants against the current ADRs and flag conflicts`
- `/kb-ingest run a finer-slice capture across the codebase using a subagent swarm`
- `/kb-ingest normalize a subagent draft and validate it against the schema before writing`
