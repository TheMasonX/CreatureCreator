---
id: creature-task-033
key: CC-033
title: Register FastNoise2Bindings as a real git submodule
status: Done
type: Task
priority: P2
tags: [repo, submodule, build, tooling]
dependsOn: []
related: [CC-023]
links:
  - .gitmodules
  - Assets/Submodules/FastNoise2Bindings
---

## Summary

The parent repository tracks `Assets/Submodules/FastNoise2Bindings` as a gitlink
(mode 160000, commit `fae174e81092f02796347e776ad89c501d2686a8`) but had no
`.gitmodules` mapping, so git treated the folder as an embedded repository. Add
the missing `.gitmodules` entry so the folder is a real submodule and
`git submodule status` works.

## Scope

- Add `.gitmodules` with a single `[submodule "FastNoise2Bindings"]` entry.
- URL is the fork `https://github.com/TheMasonX/FastNoise2Bindings.git` because
  the pinned gitlink commit exists only on that fork's `master`, not on the
  upstream `Auburn/FastNoise2Bindings` repo.
- No changes to the submodule content. The working tree of the submodule keeps
  its intentional local edits (the `#if false` wrappers that disable the
  bindings, recorded in CC-023).

## Acceptance Criteria

- `git submodule status` reports the submodule without a mapping error.
- The submodule points at `fae174e81092f02796347e776ad89c501d2686a8` and has
  no leading `-` or `+` (checked out at the recorded commit).
- `.gitmodules` passes `git diff --check`.

## Validation

- `git submodule status` returns `fae174e... Assets/Submodules/FastNoise2Bindings (heads/master)`.
- `git status --short` shows `.gitmodules` as a new file ready to stage.
- `git diff --check` exits 0.

## Findings

- The parent gitlink was already in the index; only the mapping was missing.
- The fork's `master` tip equals the pinned commit, so a fresh clone from the
  fork URL can recreate the exact checked-out state.
- The submodule shows a dirty content flag at the parent level because of the
  intentional `#if false` edits in `CSharp/FastNoise2.cs`,
  `CSharp/FastNoiseNodeEditorIpc.cs`, and `CSharp/test/BitmapGenerator.cs`.

## Blockers

None. Unity validation is not applicable: this change is repository tooling only
and does not affect compiled code.

## Next Step

Stage `.gitmodules` and commit when the user requests it. The unrelated
untracked `Assets/Textures/Scales` files are not part of this change.
