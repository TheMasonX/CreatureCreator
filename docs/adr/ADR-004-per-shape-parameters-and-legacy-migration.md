# ADR-004: Per-shape parameters and legacy migration

- Status: Accepted
- Date: 2026-08-23
- Ticket: CC-043
- Deciders: BeastMaster mode
- References:
  - `docs/tasks/tickets/CC-043-per-shape-parameters.md`
  - `Assets/Scripts/Runtime/Definition/ShapeDefinition.cs`
  - `Assets/Scripts/Runtime/Morphology/Sdf/PrimitiveNodes.cs`

## Context

`ShapeDefinition` currently stores one `PrimarySize` value for every shape.
Capsule length, ellipsoid axes, and box dimensions therefore depend on
non-uniform `Transform.Scale`. That model makes small parts difficult to author
and uses the approximate non-uniform `TransformNode` path for primitive size.

The DNA must describe the intended primitive dimensions directly. Existing v2
creature files must continue to load without a version bump.

## Decision

`ShapeDefinition` remains the authoritative shape record. Add explicit fields:

- Sphere: `Radius`.
- Capsule: `Axis`, `Radius`, and `Height`.
- Ellipsoid: `Radii` with three positive axis values.
- Box: `HalfExtents` with three positive axis values.
- All shapes retain `SmoothBlendRadius`.

`PrimarySize` remains a legacy read boundary during migration. New canonical
JSON writes the explicit shape fields and does not write `primarySize`.

Legacy JSON with only `primarySize` maps exactly as follows:

- Sphere radius = `PrimarySize`.
- Capsule axis = local Y, radius = `PrimarySize`, height = `1`.
- Ellipsoid radii = `(PrimarySize, PrimarySize, PrimarySize)`.
- Box half-extents = `(PrimarySize, PrimarySize, PrimarySize)`.

The legacy capsule `Transform.Scale.y` remains an authored transform until a
future migration converts old capsule transforms into the new height value.
The first implementation must preserve legacy world geometry during load.

Primitive SDF nodes receive explicit dimensions. Limb parts continue to ignore
`Shape`, because their geometry derives from `LimbChain`.

## Consequences

- New DNA expresses primitive dimensions without relying on non-uniform scale.
- Canonical JSON remains deterministic and has an additive migration path.
- Legacy files need explicit migration coverage for each primitive.
- The managed and portable SDF paths must use the same explicit parameters.
- Existing shape editing and generated geometry must remain unchanged until the
  new fields are wired through the editor and compiler.

## Validation

- Canonical JSON round-trips explicit parameters byte-for-byte.
- Legacy JSON loads into the documented explicit values.
- Managed and portable primitive evaluations agree for representative values.
- Existing limb and body generation tests remain green.
