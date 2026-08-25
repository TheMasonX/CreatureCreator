# ADR-007: Resolved Morphology Model

- Status: Accepted
- Date: 2026-08-24
- Related: CC-056A, CC-056B, CC-018, CC-022, CC-051, CC-055, CC-076

## Decision

Derive one resolved geometry model from authoritative DNA and make every
generation subsystem consume it instead of re-deriving the same values
independently. `ResolvedLimb` (CC-056A, part A) is an immutable snapshot of a
`LimbChain` — joint positions, segment lengths, total length, normalized arc
length, thickness, and root/terminal sockets. A matching `ResolvedBody`
follows with the same contract for Body samples and frames.

Resolved state is derived, never serialized, and never written back into DNA
(ADR-001 §5). Joints and samples stay in the owning part's local morphology
frame; creature-space placement is a separate concern owned by
`CreaturePartWorldTransformResolver`. `Resolve` is pure and deterministic and
copies its input arrays, so later mutation of the source cannot change the
snapshot. Consumers that receive structurally invalid input (null joint, empty
chain) must stay total: they catch the resolve failure and skip, because the
validator reports structural errors before generation.

## Consequences

- Metaball sampling, skeleton inference, and resolved-envelope validation
  interpret each limb chain identically; segment lengths and arc length are
  computed once instead of per consumer.
- A consumer owns only its fidelity knob (for example, per-segment metaball
  spacing), never the geometry derivation.
- The migration surface is one public factory: `ResolvedLimb.Resolve`.
- The validator and skeleton inferrer keep their defensive, never-throw
  contracts by catching resolve failures on structurally broken input.
- The centerline is the authored polyline (v1) until CC-055 decides a smooth
  centerline.
- `ResolvedBody` mirrors the same contract for Body samples and frames.
