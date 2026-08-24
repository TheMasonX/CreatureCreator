# Audit Synthesis: Contract Hardening

**Date:** 2026-08-23
**Sources:** Latest Commit-Round Delta Audit and Further Audit `CCA-9E7D4B2F1A6C8D03`
**Baseline reviewed:** `237c818a055fdc9469511d442dd1a29d022a85ca`

## Outcome

Both audits agree that the semantic Body/Limb model and multi-item output are
moving in the correct direction. The next work should harden contracts before
adding more geometry, materials, animation, or locomotion features.

## Confirmed Open Findings

- **CC-049:** Limb SDF composition reads `Shape.SmoothBlendRadius`, although
  Shape is inert for limb geometry.
- **CC-050:** Bounds validation checks parent-local transforms and local limb
  coordinates instead of the resolved creature-space geometry envelope.
- **CC-051:** `Transform`, `ParentAttachment`, child-at-tip frames, and
  `GeometryAttachment` do not yet have one documented placement authority.
- **CC-052:** Mesh geometry is baked into creature-space vertices before exact rig
  binding exists, and mirrored binding identity is not explicit.
- **CC-053:** The editor renders multiple geometry items, but source selection,
  visibility, regeneration persistence, and mirror mapping need focused coverage.
- **CC-054:** Thickness-key quantization can create duplicate times.
- **CC-055:** Limb centerline smoothing and generation-aware sampling remain
  undecided. The sampler currently uses fixed `0.1f` spacing.

## Confirmed Fixed or Stale Findings

- Child-at-tip semantics were reworked. The shared resolver inserts every
  ancestor limb terminal-joint translation, and current resolver and skeleton
  tests cover the behavior.
- CC-031 editor mesh-item rendering and palette wiring are present after pass 2.
  The remaining editor gap is interaction and source mapping, tracked by CC-053.
- Mirrored mesh triangle winding was corrected in the current generator and has
  a passing regression test. Do not reopen this as new work.
- Mesh vertex-color parity was implemented in CC-031 pass 3. Shader multiplication
  remains a separate residual risk documented by CC-031.
- CC-040 clears stale limb data on type changes and validates stale in-memory
  combinations.
- CC-014 active-cell classification already removed the redundant full-volume
  classification during contour resolution. Performance work remains separate.
- CC-047 and CC-048 replace the duplicate registry keys that were previously
  used by the FastNoise and warning/UI cleanup tickets.

## Scope and Process Decisions

- Keep CC-031 closed for the implemented pass. Do not add a third nullable
  geometry source before a typed composition design exists.
- Use CC-051 as the placement contract dependency for CC-050 and CC-052.
- Keep CC-007 and CC-009 ahead of further geometry expansion where their
  attachment and morphology contracts are required.
- Treat sibling ordering as a product decision, not a reason to add more
  ordering strategies. Existing alphabetical presentation remains provisional.
- Runtime test evidence remains partly manual because the MCP runner does not
  discover the runtime test assembly. Do not claim CI-level coverage until a
  reliable automated invocation exists.

## Validation

The synthesis was checked against current source, ADR-002, active task keys,
and the CC-018/CC-031 validation notes. No runtime code was changed.

## Next Step

Start with CC-049 or CC-051. Resolve the placement and geometry-source
contracts before implementing CC-050, CC-052, or additional geometry types.
