# ADR-004: Derive rig rotations from position-only poses

- Status: Accepted
- Date: 2026-08-24
- Ticket: CC-069
- References: CC-052, ADR-003, `PosedSkeleton`, `SkeletonInferrer`

## Context

`PosedSkeleton` stores immutable world positions. FABRIK changes positions and
must remain pure math. A runtime Transform rig also needs a rotation for each
bone, but a second mutable pose representation would create competing pose
paths.

## Decision

Keep `PosedSkeleton` position-only for V1. Add a pure resolver that derives each
bone rotation from the current posed positions and the rest `Skeleton`.

For a bone with a child, the direction is the child position minus the current
bone position. For a terminal bone, use its rest rotation. The resolver keeps
the rest rotation's up axis when it can, and uses a deterministic perpendicular
axis for parallel directions. Missing pose entries are rejected.

The resolver does not mutate the skeleton or pose. The runtime rig will consume
its positions and rotations in one later adapter. This decision does not deform
the welded implicit surface. CC-052 mesh-asset items can use their rest-space
binding descriptor; implicit geometry remains deferred.

## Consequences

- IK remains position-only and testable without Unity scene objects.
- Runtime Transform rotation is deterministic and derived from one pose snapshot.
- Terminal bones do not spin unexpectedly when their endpoint is not represented
  by a separate posed joint.
- A future animation format may add authored rotations, but it must replace this
  resolver at one explicit boundary.

## Validation

Runtime tests cover child-direction rotation, terminal rest fallback, missing
pose data, degenerate directions, and non-mutation.
