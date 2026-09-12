# CreatureCreator — Data, Configuration & Serialization Audit

**Report ID:** `CC-AUDIT-DATA-20260912-7AC31E90`
**Suite:** `CC-AUDIT-SUITE-20260912-9F42C6D1`
**Branch base audited:** `8623b6e629e246597224820c1ac59ebe0e192f57`
**Primary sources:** `CreatureDefinition`, resolved morphology/snapshot types, `GeneratedCreatureData`, serialization/canonicalization code, `CreatureGenerationConfig`, `CreatureMeshPalette`, `CreatureMaterialPalette`, ADR-003/006/007, TSK-0095/0076
**Unity execution:** unavailable

## Executive assessment

The data model has improved substantially through the introduction of resolved immutable-looking snapshots and canonicalization. The remaining architectural issue is that “immutable” is often a property of the container API rather than the entire object graph.

The other recurring problem is dual authority: raw authoring DNA remains available alongside resolved snapshots and generated presentation data. That is appropriate at system boundaries, but unsafe when both are passed deep into the same subsystem because a future change can accidentally read from the wrong representation.

Configuration/palette consolidation is conceptually correct: ADR-006 requires one runtime-safe `CreatureMeshPalette` and one shared `CreatureGenerationConfig`. The outstanding concern is operational proof and revision semantics, not another palette class.

## Findings

### DC-01 — `GeneratedCreatureData` is only shallowly immutable

**Severity:** P1/P2  
**Confidence:** 97%  
**Owner:** TSK-0095

Read-only properties and detached arrays do not guarantee that a published generation result is immutable when the result still references mutable definition/appearance/list objects.

This is particularly significant because generation crosses a thread boundary. Producer-side mutation after publication is conceptually a race even if no concurrent writes happen to the same CLR field.

Recommended contract:

```text
mutable authoring model
   → canonicalization
   → immutable published snapshot
   → generation
   → immutable generated data
   → presentation
```

A generation result should never retain the editor's live authoring graph.

### DC-02 — `ResolvedCreatureSnapshot` must state ownership guarantees per field

**Severity:** P1/P2  
**Confidence:** 97%  
**Owner:** TSK-0095

The resolved snapshot is the intended derivation authority, but “snapshot” is not enough as a guarantee. Arrays, curves, appearance structures, and nested records need explicit detachment semantics.

Create a field-level ownership table:

| Field kind | Published form | Mutable after publication? |
|---|---|---|
| scalar | value | no |
| vector/quaternion | value | no |
| array/list | detached array/list or immutable wrapper | no |
| curve/program | immutable value representation | no |
| Unity object | reference only at presentation edge | outside pure snapshot |

### DC-03 — Raw/resolved dual representation is still an escape hatch

**Severity:** P2  
**Confidence:** 97%  
**Owner:** TSK-0095

`CreatureRuntimePreview` and other boundaries still have access to both raw definition and resolved snapshot.

This is justified when an operation genuinely needs both, but it should be rare. Passing both down several layers makes authority ambiguous.

Recommendation: generated subsystems consume the resolved model only. Raw DNA should remain in authoring/serialization boundaries and in diagnostics that explicitly need provenance.

### DC-04 — Canonicalization must remain idempotent and revisioned

**Severity:** P2  
**Confidence:** 96%  
**Owner:** TSK-0095

Historical tests already established parity between resolving raw canonical-shaped DNA and resolving its canonicalized form. Preserve that property.

The next step is to define a canonicalizer version or schema revision whenever canonicalization semantics change. Otherwise cached/generated assets can be indistinguishable across revisions.

### DC-05 — Legacy shape fallback semantics remain a duplication hotspot

**Severity:** P2  
**Confidence:** 96%  
**Owner:** morphology/config consolidation family

Earlier audits identified the same effective shape-size fallback semantics in deserialize/runtime canonicalize/editor locations. Even if the exact code has shifted, this remains a category to guard against.

There should be exactly one policy function for “effective dimensions for a shape,” with serialization migration producing explicit canonical fields before runtime consumers execute.

Do not fix this by creating another helper in a third layer. Search all fallback semantics and route every consumer through one canonical source.

### DC-06 — `CreatureGenerationConfig` has a good central shape but scalar clamping can hide invalid asset authoring

**Severity:** P2  
**Confidence:** 91%  
**Owner:** TSK-0076

`DefaultVoxelsPerUnit` returns `Mathf.Max(1f, defaultVoxelsPerUnit)`. This makes runtime behavior safe but masks an invalid serialized value rather than surfacing it.

For user-authored project assets, validation should report that the serialized value is below the allowed minimum. Silent coercion is fine only if it is an intentional compatibility policy.

Prefer:

- editor validation/error for invalid asset data;
- runtime `Effective*` clamping only as a final defensive guard.

### DC-07 — Configuration inheritance versus per-request overrides needs a complete signature contract

**Severity:** P2  
**Confidence:** 95%  
**Owner:** TSK-0076 / TSK-0008

ADR-006 correctly allows request-specific quality overrides. The missing question is what constitutes the effective configuration for determinism.

Define:

```text
EffectiveGenerationConfig = asset defaults + request overrides
GenerationSignature = hash(effective config + canonical DNA + generator revision)
```

Without this, two requests that share DNA can generate different meshes while appearing equivalent to caches, tests, or logs.

### DC-08 — Palette assets solve type duplication but not missing-key policy

**Severity:** P2  
**Confidence:** 95%  
**Owner:** TSK-0076

A shared `CreatureMeshPalette` removes the old editor/runtime asset-type split. A separate policy still governs what happens when a key is absent.

The accepted direction is explicit failure rather than silent geometry substitution. Make the error include:

- missing key;
- part/source ID;
- whether the request came from editor or runtime;
- palette asset identity.

This is more actionable than a generic “mesh not found.”

### DC-09 — Config assignment persistence is a project/editor integration contract

**Severity:** P2  
**Confidence:** 92%  
**Owner:** TSK-0076

The editor loads a `CreatureGenerationConfig` asset path through `EditorPrefs`, while the runtime component stores a serialized reference. Those are different persistence models.

This is reasonable because the editor's chosen active configuration can be user-local while a runtime prefab can own a project asset reference. The distinction should be explicit.

### DC-10 — Serialization should not serialize derived data that can drift from DNA

**Severity:** P2  
**Confidence:** 96%  
**Owner:** TSK-0095 / ADR-007

ADR-007 correctly establishes resolved morphology as a derivation source and says derived state is not serialized. Preserve that strongly.

Any future temptation to cache skeletons, generated radii, or resolved attachment frames inside DNA files should be treated as a correctness smell unless accompanied by an explicit invalidation/version system.

### DC-11 — Object identity should not be inferred from display names

**Severity:** P2  
**Confidence:** 96%  
**Owner:** data model / editor

Stable IDs are already central to part/bone identity. Display names, GameObject names, and palette labels must remain presentation only.

The same rule should apply to cache keys and diagnostics correlation IDs: use stable generated IDs or explicit request/revision identifiers, not human-facing names.

### DC-12 — Configuration reference graphs need cycle/ownership assumptions documented

**Severity:** P3  
**Confidence:** 88%  
**Owner:** TSK-0076

A config can reference palettes, palettes reference mesh/material assets, and runtime/editor objects reference the config. That is a healthy direction, but resource ownership is different from reference reachability.

Document that asset references are non-owned dependencies: consumers must not `Destroy` project assets during preview cleanup.

## Required invariants

1. Raw DNA is mutable and authoring-owned.
2. Canonical DNA is detached from transient editor state.
3. Resolved snapshots are immutable object graphs.
4. Generated data contains no live authoring references.
5. Configuration assets are shared dependencies, not runtime-owned objects.
6. Generation signatures include all effective inputs that affect output.
7. Presentation code cannot mutate the source model through result references.

## Exclusions

Do not reopen the already-resolved `IDnaSerializer` interface-shape finding. Keep serialization abstraction focused and do not add a larger framework without a demonstrated need.

## Conclusion

The next data-layer improvement is not more DTOs. It is a strict publish boundary: detached immutable resolved state in, deterministic generated data out, Unity assets only at the presentation edge. That will simultaneously improve async correctness, caching viability, and testability.
