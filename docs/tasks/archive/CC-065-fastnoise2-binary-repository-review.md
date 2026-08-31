---
id: creature-task-065
key: CC-065
title: FastNoise2 binary / submodule repository review gate
status: Done
type: Review
priority: P1
tags: [repo-hygiene, dependency, submodule, review-gate]
dependsOn: []
related: [CC-033, CC-034]
links:
  - Assets/Includes/FastNoise2/bin/
  - Assets/Submodules/FastNoise2Bindings
  - docs/tasks/handoffs/2026-08-24-audit-revision-fast-preview-and-contract-synthesis.md

## Summary

Commit `393087c` ("Add FastNoise2 binaries and editor metadata to Unity project") tracked
`Assets/Includes/FastNoise2/bin/*` (FastNoise.dll, FastNoiseD.dll, NodeEditor.exe,
NodeEditor.ini, NodeEditorIpc.dll, NodeGraph.ini + .meta) even though the CC-045 handoff
explicitly gated any FastNoise2 commit on human review. The same dependency now exists as
both a submodule (`Assets/Submodules/FastNoise2Bindings`) and tracked binaries, which is a
"local != submodule" / "editor != runtime" risk. This is a human review gate, not a blind
deletion.

## Review questions

1. Are these binaries supposed to be tracked in this repository?
2. Are they required for runtime/CI?
3. Are they already represented by the FastNoise2 submodule?
4. Are they redistributable under the required licenses?
5. Why are NodeEditor.exe and .ini configuration committed?
6. Are platform-specific binaries needed?
7. Should these instead be generated/downloaded during setup?
8. Do they duplicate submodule contents?

## Guardrails

- Do not delete blindly; the binaries may be intentional and required for Unity
  integration.
- Establish one clear dependency authority for FastNoise2 (submodule vs tracked binaries,
  not both).
- Keep `.gitignore`/setup consistent with the chosen policy.

## Resolution (2026-08-24) — HUMAN REVIEW COMPLETE

Outcome: **KEEP** (permission granted). The repository owner explicitly approved the
FastNoise2 submodule inclusion on 2026-08-24. The submodule
(`Assets/Submodules/FastNoise2Bindings` @ `32c2546`, `TheMasonX/FastNoise2Bindings`) is
registered in `.gitmodules` and is NOT used by any runtime path, but it compiles and
integrates without issue (CC-047 restored the DllImport P/Invoke). The tracked binaries
under `Assets/Includes/FastNoise2/bin/*` (committed in `393087c`) are accepted under the
same permission and are not git-ignored.

Review questions 1-8 remain recorded above as documentation. With permission granted,
none of them block further work. If the user later wants the license / platform-set /
setup-time-generation questions formalized (e.g. a `LICENSE`/`NOTICE` note or a setup
download script), that is a separate follow-up, not a gate on the current repo state.

## Next Step

None (gate passed). Optionally formalize FastNoise2 licensing/setup documentation in a
future hygiene pass; otherwise continue with the CC-051 -> CC-007 -> CC-064 sequence.
