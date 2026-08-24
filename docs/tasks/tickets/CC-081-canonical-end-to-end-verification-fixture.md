---
id: creature-task-081
key: CC-081
title: One canonical end-to-end morphology verification run
status: Backlog
type: Task
priority: P2
tags: [validation, tests, process]
dependsOn: []
related: [CC-056A, CC-056B, CC-061]
links:
  - Assets/Scripts/Tests/Runtime/
  - docs/audits/creaturecreator-audit-26-08-24-11-48-00.md

## Summary

Test evidence is fragmented across focused runtime runs, broader runs with
baseline failures, initialization timeouts, and editor-only manual checks.
Before the morphology foundation is called stable, establish one canonical
verification command/run that proves the full chain for one canonical dino
fixture plus several adversarial fixtures.

## Scope

One run that proves:
```text
Definition -> Morphology -> SDF -> Mesh -> Skeleton -> Rig -> serialization
```
- One canonical dino fixture plus adversarial fixtures (mirrored, child-at-tip,
  limb chains, mesh-asset items, non-finite inputs).
- Deterministic topology + value assertions at the SDF/mesh boundary.
- Recorded command and expected pass set so new work has a single regression
  gate instead of a loose collection of runs.

## Acceptance Criteria

- The canonical run is documented with its exact command/filter and expected
  pass count.
- The five documented pre-existing PlayMode failures are explicitly separated
  from regressions.
- A new contributor can run one command to see the whole chain green.

## Validation

The run itself is the validation: all chain stages pass on the dino + adversarial
fixtures.

## Findings

The 2026-08-24 delta audit (§13) flags that focused runs, baseline failures, and
timeouts make it hard to prove the foundation is stable. This ticket consolidates
that evidence into one gate.

## Blockers

None; can proceed in parallel with CC-056A migration.

## Next Step

Define the fixture set and the exact NUnit filter, add any missing chain-stage
asserts, and record the command in the README validation section.
