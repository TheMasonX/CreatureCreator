---
name: creature-workflow
description: |
  Run the standard CreatureCreator implementation loop for a bug, feature,
  refactor, editor change, or validation gap. Use when work needs task tracking,
  evidence capture, focused validation, or a handoff across runtime and editor.
argument-hint: 'Task, owning slice, acceptance criteria, and validation target'
user-invocable: false
disable-model-invocation: false
---

# CreatureCreator Work Cycle

## Outcome

Deliver a small, evidence-backed change with a current MemorySmith task, focused
validation, and explicit residual risk.

## Procedure

1. Read `Assets/Scripts/README.md`, the nearest source, focused tests, and the
   matching MemorySmith task. Read an ADR or audit only when the task depends on it.
2. Identify the owning abstraction. State one falsifiable implementation
   hypothesis and one cheap check that can disconfirm it.
3. Load [engineering-guardrails](../engineering-guardrails/SKILL.md) for code,
   refactor, or architecture work. Apply its type, ownership, duplication,
   scope, and production-readiness gates before editing.
4. Ensure the work has one MemorySmith task. Preserve direct user requirements
   verbatim under `## User Mandate` and use the `user-mandated` label.
5. Edit the smallest owning slice. Keep `CreatureDefinition` authoritative and
   avoid duplicate DNA mutation or derivation paths.
6. Run the narrowest executable validation immediately after the first edit.
   Repair the same slice and rerun it before broadening scope.
7. Run broader relevant checks when the focused check passes. Use
   `unity-validation` for Unity behavior, assemblies, serialization, generation,
   topology, appearance, skeleton, or IK.
8. Add implementation and validation evidence to the MemorySmith task. Record
   blockers, unavailable Unity checks, residual risk, and the next step.
9. Review the diff for unrelated changes, public API drift, duplication,
   ownership gaps, and missing edge-case tests.

## Boundaries

- Runtime code stays independent of scene objects, editor APIs, and mutable
  generated state.
- Editor code owns sessions, undo, previews, scene handles, and lifecycle.
- `DefinitionValidator` reports invalid DNA. It does not repair definitions.
- `DefinitionCanonicalizer` owns quantization and stable ordering at mutation
  and serialization boundaries.
- Preserve documented simplifications unless the user requests a replacement.
- Never claim Unity behavior from source inspection alone.
- Do not edit `Data/Tasks/*.json` or create new historical `docs/tasks/` tickets.

## Handoff

Report changed files, commands and results, evidence, blockers, residual risk,
and the next step. Do not commit or create branches unless the user requests it.
