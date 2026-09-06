---
name: kb-query
description: |
  Query the CreatureCreator MemorySmith knowledge base (Data/Memories) and its
  wiki/task tools to recover context, evidence, and source pointers without
  re-researching the codebase. Use for onboarding, before starting non-trivial
  work, to check for an existing record or task, to find the source file behind
  a memory or wiki claim, and to reconstruct durable context from retrieval
  alone. Read-only: never writes memories, pages, or tasks.
argument-hint: 'Topic, subsystem, record id, symbol, or file to look up in the knowledge base'
user-invocable: true
disable-model-invocation: false
---

# MemorySmith Knowledge-Base Query (kb-query)

## Outcome

Retrieve the right MemorySmith context fast: which durable memory records exist
for a topic, what they say, which source files/tests/ADRs back them, and which
task owns a behavior. The goal is to let an agent reconstruct correct context
from retrieval alone instead of re-deriving it from source.

## When to Use

Use this skill when you need to:

- onboard onto a subsystem (architecture, definition model, generation,
  morphology/SDF, mesh extraction, appearance, skeleton/IK/pose, editor/preview,
  tests/ops);
- check whether a memory record or MemorySmith task already exists for a topic
  before creating or changing one;
- answer "where is X implemented / how does Y behave / which ADR decides Z";
- find the source file behind a memory claim, wiki page, or search hit;
- gather the evidence and invariants relevant to a task before editing.

Read-only: this skill never creates, edits, or deletes memories, pages, or
tasks. To write durable knowledge use `kb-ingest`; to track work use
`task-tracker`.

## System and Repository Context

- The wiki instance for this workspace is the `mcp_memorysmithwi_*` server
  (service "MemorySmith - CreatureCreator Wiki", port 7916). Durable records
  live as JSON files under `Data/Memories/<Status>/`, pages under
  `Data/Pages/`, and tasks under `Data/Tasks/`.
- Retrieval may be **lexical-only**. The semantic (ONNX) embedding model under
  `Data/Models/` may be absent (`providerMode: lexical-fallback`), so
  `hybrid_search` falls back to lexical ranking. Treat "semantic" results as
  lexical unless the provider reports `kind: semantic`. Use distinctive
  keywords and exact identifiers when lexical-only.
- The code-search index under `Data/Graph/code-search` may be empty (0 files)
  until a build/index runs. Check `memorysmith_code_search_status` before
  relying on `memorysmith_code_search`.

## MemorySmith query tools

The `mcp_memorysmithwi_*` server exposes these query tools (used read-only):

| Tool | Use for |
| --- | --- |
| `memorysmith_search` | Lexical keyword search across memory records. |
| `memorysmith_hybrid_search` | Lexical (and, when the model is present, semantic) search. |
| `memorysmith_get` | Fetch one full record by `Id`. |
| `memorysmith_context_pack` | Build an agent-ready pack: search results + linked references/conflicts. |
| `memorysmith_find_by_source` | Back-map a source path/URL fragment to records that cite it. |
| `memorysmith_code_search` | Search indexed source-code chunks (only if the index is built). |
| `memorysmith_code_search_status` | Check whether the code-search index is built/current. |
| `memorysmith_task_list` / `memorysmith_task_get` | Find the task that owns a behavior (task-tracker tools). |

Result diagnostics you will see and can largely ignore for retrieval:

- `tag.unknown_plain` (Info): plain topic tags outside an allowlist; harmless.
- `source.unresolved` (Warning): a `SourceLink.Uri` is outside configured
  allowed source roots or empty. The record still loads and searches; follow the
  path in the record content or look the file up on disk.
- `tag.invalid_namespace_value` (Warning): a tag value is disallowed (e.g.
  `kind:reference`). It does not block retrieval.

## Procedure

### 1. Pick the entry tool by goal

- Topic or behavior → `memorysmith_hybrid_search` (or `memorysmith_search`) with
  a few distinctive keywords plus any known identifiers (class names, `CC-###`,
  `TSK-####`, subsystem nouns).
- Known record `Id` → `memorysmith_get`.
- Need a whole topic bundle fast (record + its references/conflicts) →
  `memorysmith_context_pack`.
- "Which memory cites this source file?" → `memorysmith_find_by_source` with a
  path fragment such as `CreatureDefinition.cs` or `Morphology/Sdf`.
- Find the owning work item → `memorysmith_task_list` / `memorysmith_task_get`.

### 2. Use distinctive, lexical-friendly queries

Because search may be lexical-only, prefer:

- exact identifiers: `CreatureMeshGenerator`, `SdfProgramBuilder`,
  `SkeletonInferrer`, `DefinitionValidator`, `FabrikSolver`;
- short keyword sets that appear in titles/tags/content, not long prose;
- when a broad query returns nothing, retry with a narrower exact term before
  concluding the record does not exist.

Review `matchReason` and the snippet before trusting a hit. Read the full record
with `memorysmith_get` when the snippet matches but you need details.

### 3. Follow the record graph

Each record is graph-linked. After a hit:

- read `References`/`Relationships` to reach the layer records it builds on
  (an architecture record references each layer; a layer record references the
  architecture and definition records);
- read `SourceLinks` for exact repository-relative paths and open the real file
  when you need line-level truth — a memory is a summary, never a substitute for
  source when behavior matters;
- read `Tags`/`Status` to judge tier (`2` Core = preferred authoritative,
  `1` Working = provisional) and topic.

### 4. Onboard onto a subsystem

To get up to speed on one slice fast:

1. `memorysmith_search` the subsystem noun plus `creaturecreator`
   (for example `creaturecreator skeleton ik pose`).
2. `memorysmith_get` the top layer record, then follow its `References` to the
   finer slice record.
3. Open the `SourceLinks` files to confirm current behavior; do not trust a
   memory that contradicts what the source now says — record the discrepancy as
   a follow-up (do not silently edit the record here).
4. `memorysmith_task_list` to find the task that owns the slice's next work.

The coarse layer records and their finer slice records are the natural
onboarding map. See the **kb-ingest** skill for the record-id conventions if you
need to add to this map.

### 5. Avoid duplicate creation

Before `kb-ingest` (write) or `task-tracker` (task create), always query first:

- a matching memory already exists → extend it, do not duplicate;
- a matching task exists → reuse it (check status) instead of creating another.

## Onboarding example (BeastMaster)

When starting non-trivial work, do this once per slice:

1. Read the nearest invariant/summary (for example
   `Assets/Scripts/README.md`) for the slice.
2. `memorysmith_search` the slice + `creaturecreator`; `memorysmith_get` the
   layer and finer records; follow `References`.
3. Open the `SourceLinks` files and the nearest tests/ADR before editing.
4. `memorysmith_task_list` for the owning `TSK-####`; add evidence there as you
   work (via `task-tracker`), not in this read-only query pass.

## Completion Checklist

- [ ] Queried with distinctive, lexical-friendly terms (not just long prose).
- [ ] Used `memorysmith_get` to read the full record behind a snippet match.
- [ ] Followed `References`/`Relationships` and `SourceLinks` to reach the layer
      record and real source files.
- [ ] Confirmed current behavior against source, not just the memory summary.
- [ ] Checked for an existing memory/task before creating (no duplicate).
- [ ] Did not write, edit, or delete any memory, page, or task (read-only).
- [ ] Reported which records were found and any gaps for follow-up.

## Example Prompts

- `/kb-query onboard me onto the morphology/mesh-extraction slice`
- `/kb-query does a memory record cover the authoritative DNA and serialization layer?`
- `/kb-query which source file backs the claim about mirrored limb bones`
- `/kb-query what MemorySmith task owns the editor preview state machine work`
- `/kb-query is there an existing record for CreatureMeshGenerator before I capture it`
