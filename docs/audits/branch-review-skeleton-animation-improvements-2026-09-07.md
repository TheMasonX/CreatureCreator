# Review — `audit/skeleton-animation-improvements-2026-09-07`

**Base:** `main` @ `0f1a0a9` (my last progress report). **Branch HEAD:** `bc854ef`.
207 commits, 255 files touched overall; **127 files / +9,442 / -1,266 in
`Assets/Scripts`** once docs/audit noise is filtered out. Reviewed at the
net-diff and current-state level per your note, not commit-by-commit.

## Headline: this is a real feature landing, not just cleanup

The branch delivers the thing the roadmap has been building toward since C4:
**actual skinned-mesh animation.** New subsystems:

- **`Animation/Binding/`** — `ImplicitSurfaceWeightAuthoring.cs` (417 lines),
  `ImplicitSurfaceInfluenceDomainResolver.cs`, `MorphologyInfluenceRadiusBridge.cs`,
  `RigidMeshWeightAuthoring.cs`. This computes per-vertex bone weights for the
  welded implicit-surface mesh by measuring distance to bone *segment axes*
  (not bone centers), which is what makes bent limbs deform correctly instead
  of pivoting around a joint center.
- **`Animation/Skinned/`** — `CreatureSkinnedMeshRenderer.cs` and
  `SkinnedMeshBindingBuilder.cs` convert that authored data into Unity's
  actual `Mesh.boneWeights`/`Mesh.bindposes` and drive a real
  `SkinnedMeshRenderer`, replacing (for animated geometry) the old
  regenerate-the-whole-mesh-every-pose approach.
- **Editor tooling** — a working `RigBoneRotateTool` (an actual `EditorTool`,
  486-line `RigDebugView` scene overlay for inspecting bones/weights/rest
  transforms live).

I read `ImplicitSurfaceWeightAuthoring.cs` and `SkinnedMeshBindingBuilder.cs`
in full. Both are genuinely good code: pure functions with no hidden state,
deterministic tie-breaking explicitly justified in comments (weight
descending, then bone-index ascending, so vertex weighting doesn't depend on
segment insertion order), explicit finite/positive/total validation with
`DomainException` on every input path, and doc comments that explain *why*
a design choice was made (e.g., why radius isn't stored on `Bone`, why
bind-pose equals rest-pose and what that buys you) rather than just what the
code does. This is the same disciplined style the rest of the codebase has
had since my first audit, held up across a much bigger, more novel piece of
work. I'd call this the best-engineered subsystem in the project right now.

The delivery is validated, not just claimed: `TSK-0131` records 10/10 focused
PlayMode tests by name (bent-chain-binds-correct-segment,
mirrored-limb-binds-mirrored-bone, rest-pose round-trip, ordering
independence, etc.), and `TSK-0143` closed a real residual (SMR-level mirror
parity) with an orchestrator-independent re-run of the build and diff-check,
not just a self-report.

## What's still rough around this feature

- **`TSK-0167` — "generated Body skinning smear."** Rotating body/neck bones
  visibly stretches the mesh. Still `Backlog`. The task description is
  disciplined about not guessing the cause before diagnosis (radius/overlap
  vs. segment placement vs. bindpose vs. "another skinning contract" are all
  still open), which is the right instinct, but it means the headline
  feature has a known, visible, unresolved artifact today.
- **A second `TSK-0167`** — duplicate rig/SMR components getting created on
  repeated preview rebinds — is `Ready` but not started. Same root class of
  bug I flagged as "TSK-0122 fixed for the plain rig" a report ago; looks
  like the SkinnedMeshRenderer path needs the same ownership hardening.
- **`TSK-0048`** (the long-standing Critical-priority broken-ankle artifact)
  is still `InProgress`, unresolved across three of my reviews now. Worth a
  direct status check next round — it's easy for a long-open Critical item
  to become background noise.
- **`TSK-0140`** (finishing the `GeneratedCreature` legacy-exit / output
  contract) is `Blocked` on genuinely narrow remaining gaps (one test-coverage
  gap, per the latest comment) — close to done, not stuck.

## The one real process problem: task-board integrity, not code quality

I scanned all 172 `Data/Tasks/*.json` files on this branch (up from 126 on
`main`) and found:

- **Four duplicate task keys, seven extra files:** `TSK-0136` (2 files),
  `TSK-0166` (2), `TSK-0167` (3 — the smear bug, the segmentation-tuning
  task, and the duplicate-component bug all independently claim it), and
  `TSK-0168` (2). Each pair/triple has a genuinely different title and
  description — this isn't a copy-paste duplicate, it's an **ID collision**,
  almost certainly from parallel sessions each grabbing "next available
  number" from a view of the board that was already stale by the time they
  wrote their file. This matches what you said about the harness — one file
  at a time, likely across concurrent runs, with no lock on ID assignment.
- **One task record fails to parse:** `tsk-0156-separate-skeleton-builder-from-mutable-bone-model.json`
  has an unescaped control character in a description string (JSON error at
  line 5) and can't be loaded by a standard JSON reader. Any tooling that
  scans `Data/Tasks/*.json` (including future agents/audits) will silently
  skip this file or crash on it.

Context worth carrying forward: `TSK-0123` ("restore an enabled task-record
validation CI gate" — the thing that would mechanically catch exactly these
two problems) is marked **Rejected**, with a direct comment from you: *"I get
emails every commit... I do not want this to be reimplemented."* That's a
clear, standing decision — I'm not relitigating it. I'm flagging the two
concrete defects it would have caught, since they're real right now and will
keep quietly recurring at this rate of parallel task creation without some
form of check (even a cheap local script run occasionally, not necessarily
CI-with-emails, if you ever want one). Otherwise, expect more silent ID
collisions and unparseable records as the board keeps growing at ~1
duplicate/malformed file per ~40 new tasks, which is roughly this branch's
rate.

## Standing items, still true from last report

- **`CreatureEditorWindow.cs` is still not shrinking** — 3169 → 3180 lines on
  this branch. A7 remains scaffolding-built-but-not-cut-over.
- No other regressions found in the areas I re-checked (mirror math,
  quaternion quantize, legacy shape fallback all still hold at one
  implementation each; `ConsumerUnionIndex` stays removed).

## Bottom line

Strong branch. The skinned-mesh animation work is the real thing, well-built,
and honestly self-audited (the smear bug and duplicate-component bug are
tracked, not hidden). The task board grew a lot faster than my last review
and picked up genuine data-integrity cracks along the way — worth a cleanup
pass (rename the colliding files to fresh unused numbers, fix or regenerate
the malformed one) before they cause a real collision (two agents both
writing to `TSK-0167` believing they own different work).
