---
name: engineering-guardrails
description: |
  Apply strict maintainability and production-readiness checks to CreatureCreator
  code changes, refactors, and architecture decisions. Use when a change may
  duplicate logic, blur ownership, evade types, add speculative infrastructure,
  increase comprehension debt, or ship only the happy path.
argument-hint: 'Review the proposed change, owning abstraction, risks, and evidence'
user-invocable: false
disable-model-invocation: false
---

# Engineering Guardrails

Act as a staff-level reviewer. Optimize for systemic correctness and a small,
understandable design, not raw implementation speed. Apply these gates before
and during every non-trivial code change.

## Type integrity

- Read the actual type, interface, constructor, and serializer contract before
  creating an object or calling an API.
- Do not use blind casts, null-forgiving operators, reflection shortcuts, or
  fallback values to silence a compiler, analyzer, or test.
- Use an explicit cast only when the domain contract proves it is valid, and
  make the boundary and failure behavior visible.
- Construct complete strongly typed values. Do not pass partial anonymous data
  where a domain model is required.
- Treat compiler warnings and analyzer findings as evidence about a design gap,
  not noise to suppress.

## One source of truth

- Search for existing implementations, validators, resolvers, serializers,
  caches, and state before adding logic.
- Keep one canonical owner for each rule, state transition, derived value, and
  cleanup path. Reuse it or refactor it before copying it.
- Do not add a second DNA mutation path, derivation path, validation rule, or
  coordinate resolver.
- Assign ownership for creation, mutation, disposal, cancellation, persistence,
  and error reporting. Document the owner when the boundary is not obvious.
- Treat duplicate tasks, duplicate identifiers, and duplicate documentation as
  integrity defects. Link to the canonical record instead of creating another.

## CreatureCreator recurring traps

Task history identifies these local failure patterns. Check them explicitly:

- Keep `CreatureDefinition` as the authoring source and one immutable resolved
  snapshot as the runtime consumer boundary. Do not let SDF, skeleton, bounds,
  appearance, or editor consumers re-derive geometry, frames, attachments, or
  terminal identity from raw DNA. Evidence: TSK-0091, TSK-0092, TSK-0095.
- Keep generation stages concrete and separately owned. Do not let
  `CreatureMeshGenerator` become a god method or let `AppearanceBaker` rebuild
  programs that an earlier stage already produced. Evidence: TSK-0095.
- Keep `CreatureEditorWindow` as an orchestrator. Give preview generation,
  placement, cancellation, stale-result rejection, session state, and undo one
  clear owner each. Evidence: TSK-0098, TSK-0103, TSK-0104.
- Capture an async definition once, coalesce known-stale work, and apply output
  only after request identity, revision, and ownership checks. Dispose native
  buffers and generated Unity objects on success, failure, cancellation,
  replacement, and domain reload. Evidence: TSK-0079, TSK-0103, TSK-0104.
- Make malformed DNA total at validation and cloning boundaries. Duplicate IDs,
  null parts, missing parents, invalid roots, and non-finite values must produce
  defined issues or failures, not dictionary exceptions, garbage output, or
  silent repair. Evidence: TSK-0086, TSK-0093, TSK-0094.
- Preserve deterministic parity across managed and Burst paths, synchronous and
  asynchronous generation, mirrored transforms, canonical JSON, and repeated
  resolution. Test topology, colors, identity, ordering, and signs, not only
  compilation. Evidence: TSK-0043, TSK-0088, TSK-0091, TSK-0103.
- Consolidate shared mechanics only after a call-site inventory and behavior
  parity test. Keep policy in the concrete owner and do not create a generic
  hierarchy or utility layer without a verified repeated contract. Evidence:
  TSK-0094, TSK-0105.
- Treat Unity resource lifetime as part of correctness. Put every native
  allocation under a complete `try/finally` or an equivalent owner, including
  exception paths and early validation failures. Evidence: TSK-0079.
- Treat non-finite SDF values and culling as an explicit contract. Preserve the
  documented `+inf` outside/culled behavior and guard invalid program roots
  before Burst execution. Evidence: TSK-0066, TSK-0067, TSK-0068, TSK-0079.

## Intent and scope

- Before a change larger than a few lines, state the architectural intent, the
  owning abstraction, the smallest affected slice, and one disconfirming check.
- Prefer a local adapter or focused refactor over a new framework, abstraction,
  pipeline, or configuration layer.
- Do not pre-engineer speculative extension points, generalized policies, or
  unused abstractions. Defer them with a named follow-up when evidence requires.
- Keep diffs small enough for a reviewer to build a mental model. Split a broad
  change into vertical slices with executable checks.
- Preserve existing seams and documented simplifications unless the requirement
  explicitly changes them.

## Production-readiness gate

Do not stop at the visible happy path. For the changed boundary, check the
applicable cases:

- invalid input, empty input, missing references, and boundary values;
- failure, cancellation, retry, disposal, and domain reload behavior;
- deterministic output, repeat calls, and stale or out-of-order results;
- observability at meaningful state transitions and failures.

Use the repository's existing diagnostics and Unity console patterns. Do not add
per-voxel or per-frame logging to pure runtime generation. For operations with
side effects, make repeated execution safe or enforce one clear owner for the
lifecycle. Catch only failures that the code can handle, and preserve useful
context when reporting an expected failure.

## Completion gate

Before reporting completion, answer:

1. What existing code or contract owns this behavior?
2. Why is the new logic not duplicate logic?
3. Who owns mutation, cleanup, and failure reporting?
4. What production edge case did the focused check exercise?
5. What remains unverified, deferred, or intentionally simplified?

If any answer is unknown, stop and gather the missing evidence. Do not invent a
workaround or claim completion from compilation alone.
