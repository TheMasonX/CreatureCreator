# ADR-006: Shared Generation Configuration

- Status: Accepted
- Date: 2026-08-24
- Related: CC-031, CC-028, CC-072

## Decision

Use one runtime-safe `CreatureMeshPalette` asset type and one
`CreatureGenerationConfig` asset for shared generation defaults and palette
references. The editor and runtime preview may apply per-request quality or
sampling overrides, but they resolve mesh and material assets through the same
configuration references.

Serialized configuration contains asset references and scalar policy values. It
does not contain delegates, editor window state, selection, Undo state, or other
transient request data. Resolver delegates remain constructed by the consuming
adapter and are passed to `CreatureMeshGenerator`.

## Consequences

- A mesh key has one project-wide palette asset type and one lookup contract.
- The editor picker and runtime component can reference the same asset.
- Runtime code has no dependency on `UnityEditor`.
- Existing editor-only palette assets require migration in projects that used the
  retired duplicate type.
- Full editor/runtime quality parity is semantic, not necessarily identical in
  voxel resolution.
