# CreatureCreator Audit Synthesis

**Date:** 2026-08-30
**Mode:** Full reconciliation
**Scope:** Sixteen supplied audits dated 2026-08-25 through 2026-08-30, current CC task records, active task index, and targeted source checks.
**Fixed point:** `1e1a57569a4e66897d04bcb7d45ecce43cc24b09`.
**Code changes:** None. This record changes task documentation only.

## Executive Summary

The audits agree on one primary mechanism: derived creature state has more than one owner. The strongest corrective action is to finish one resolved snapshot boundary, migrate consumers, and delete compatibility paths. The current board also contains historical phase tickets that should not continue as parallel architecture.

This synthesis accepts five work groups, retains fixed findings as provenance only, and archives three superseded architecture tickets. No new P0 finding was supported. Accepted work is four P1 groups and one P2 cleanup group.

## Peer-Review Addendum

The 2026-08-30 peer review found no unowned P1/P2 mechanism and created no new
CC key. Four omitted dispositions were added to existing owners:

- The missing `LimbChain` invariant for `Limb`, `Leg`, and `Arm` parts is an
	extension of CC-036. The demo fixture must be retyped or the fallback must be
	documented and tested.
- Runtime-owned `PartType` classification, including `IsLimbChainType`, is an
	extension of CC-090. The Editor must consume the Runtime predicate.
- Non-throwing `TryResolve` paths for validator-only envelope checks are an
	extension of CC-089.
- The single legacy-shape fallback rule and the `PrimarySize` independence
	regression belong to CC-043 and CC-088, not a separate task.

The review also corrected two misleading ticket filenames. The files now named
`CC-047-fastnoise2-dllimport-restore.md` and
`CC-048-editor-warning-cleanup.md` already contained keys `CC-047` and `CC-048`.
The active index has 91 rows, 93 keyed ticket files exist, and no duplicate YAML
keys remain. The two extra keyed files are the historical split records for
CC-056A and CC-056B, not duplicate keys.

The code audit's minor capsule fallback and type-blind parameter observations
remain specification questions under CC-043. They do not justify separate P3
tasks. Unity execution was not required for this documentation-only review.

## Accepted Findings

| ID | Priority | Disposition | Finding and evidence | Task |
| --- | --- | --- | --- | --- |
| F-01 | P1 | Update and supersede overlapping architecture | `CreaturePartWorldTransformResolver`, `SkeletonInferrer`, and `SdfProgramBuilder` still re-derive parent, terminal, body-sample, shape, and geometry semantics. `ResolvedBody` and `ResolvedLimb` are useful but do not yet form one creature snapshot. | CC-087 |
| F-02 | P1 | Extend existing migration | Nearest-body-sample bone binding, raw terminal-joint lookup, and repeated frame-chain resolution remain representation-dependent. The remedy is semantic attachment identity, not a better nearest-point heuristic. | CC-087 |
| F-03 | P1 | Create validation boundary task | `HasParentCycle`, malformed part lists, and clone/canonicalization paths have inconsistent invalid-input behavior. Validator claims report-only behavior, but the audit evidence identifies throwing dictionary and null-entry paths. | CC-089 |
| F-04 | P1 | Extend existing schema/SDF work | `PrimarySize` fallback remains in current-schema generation, and `SdfProgramBuilder` still interprets morphology instead of compiling a small resolved geometry representation. | CC-088; CC-043 |
| F-05 | P1 | Extend existing performance and migration work | Managed SDF removal, portable appearance, and compiler metadata cleanup remain coupled. The managed path must be reference-only before deletion, with parity evidence. | CC-088; CC-045 |
| F-06 | P2 | Extend existing frame/morphology work | `ResolvedBody` and `ResolvedLimb` duplicate polyline metrics. `BodyFrameResolver` can recompute all frames for a point query. A concrete `ResolvedPolyline` and immutable frame snapshot should own these invariants. | CC-087; CC-022 |
| F-07 | P2 | Create bounded cleanup | Repeated `IsFinite`, curve cloning/equality, quantization, mirror-point, and hierarchy-index mechanics should use small concrete Common utilities. Do not create adapter interfaces or a service hierarchy. | CC-090 |
| F-08 | P2 | Create bounded cleanup | `CreatureMeshGenerator` combines validation, field generation, extraction, appearance, asset placement, symmetry, and assembly. Split internal stages while retaining one public orchestration entry point. | CC-091 |
| F-09 | P2 | Extend existing schema task | Quantization can create invalid duplicate times, and canonicalization must not repair malformed thickness keys. | CC-054 |

## Fixed, Stale, or Rejected Claims

- Mirrored mesh triangle winding is fixed and has regression evidence in the 2026-08-23 handoff. Do not create a duplicate task.
- CC-049, CC-050, CC-051, CC-056A, CC-056B, CC-070, CC-071, CC-074, CC-075, CC-076, CC-082, CC-083, and CC-084 have recorded completion evidence. Their residual architecture is linked to follow-up tasks, not reopened as duplicate fixes.
- The audit claim that CC-083's validator rule is absent is corrected. The supplied code audit identifies a test fixture that cannot express a null parent. Keep CC-083 as historical completion, and verify its test fixture only if a future validation run regresses.
- The sibling-order strategy finding is low impact. It is recorded under CC-090 as a deletion-or-real-use decision. No separate P3 task is created.
- Appearance nearest-part selection, non-uniform SDF scale, face-only Asymptotic Decider handling, and fan triangulation remain documented simplifications. They are not new findings in this reconciliation.
- No supplied audit established a reproducible P0 issue.

## Standards Assessment

The task board follows the CC-keyed Markdown convention, but several tickets describe completed phases as active architecture. New work must preserve the Runtime/Editor boundary, report validation failures without repair, and keep DNA authoritative. P3 cleanup remains in the report unless it forms a bounded P2 task.

## Specification Assessment

The intended specification is one pipeline:

```text
Persisted DNA -> migration -> validation -> resolved creature -> SDF, skeleton, bounds, editor
```

The current implementation meets this direction for resolved Body and limb geometry, but not for all semantic identity, attachment, SDF compilation, and hierarchy consumers.

## Task Disposition

- **CC-087:** canonical resolved-creature snapshot, hierarchy, semantic attachments, frames, and world transforms. Supersedes the unfinished architecture intent in CC-006, CC-009, and CC-056.
- **CC-088:** SDF backend and legacy-shape exit. Extends CC-043 and CC-045. Includes `PrimarySize` removal from current-schema generation and managed-path deletion gates.
- **CC-089:** total malformed-definition validation and clone/canonicalization boundary. Preserves the corrected CC-082 and CC-083 evidence rather than reopening their completed tickets.
- **CC-090:** concrete Common utility and tolerance consolidation. Includes mirror primitive centralization, hierarchy index mechanics, finite checks, curve helpers, quantization helpers, and the sibling-order decision.
- **CC-091:** internal generation pipeline decomposition and immutable artifact assembly. Keeps the public generator API stable.
- **CC-054:** remains active and receives the canonicalization invariant from F-09.
- **CC-055:** remains a prerequisite for changing centerline or sampling semantics. It must define representation-independent attachment identity before CC-087 closes.

## Source Ledger

All sixteen supplied audit files were directly read. They all inspect the fixed point above or cite it as their baseline; no newer commit was identified in the reports.

1. `creaturecreator-code-audit-2026-08-25.md`
2. `creaturecreator-delta-audit-2026-08-25.md`
3. `creaturecreator-delta-audit-2-2026-08-25.md`
4. `creaturecreator-delta-audit-3-synthesis-2026-08-25.md`
5. `creaturecreator-delta-audit-4-2026-08-25.md`
6. `creaturecreator-delta-audit-5-2026-08-25.md`
7. `creaturecreator-delta-audit-6-2026-08-25.md`
8. `creaturecreator-delta-audit-7-2026-08-25.md`
9. `creaturecreator-delta-audit-8-2026-08-25.md`
10. `creaturecreator-delta-audit-9-2026-08-25.md`
11. `creaturecreator-deep-dive-audit-26-08-25-07-47-00.md`
12. `creaturecreator-delta-audit-26-08-25-14-57-00.md`
13. `creaturecreator-delta-audit-26-08-28.md`
14. `creaturecreator-consolidation-legacy-exit-audit-26-08-29.md`
15. `creaturecreator-consolidation-audit-26-08-29-18-42-00.md`
16. `creaturecreator-utility-consolidation-audit-26-08-30.md`

Targeted source checks included `CreaturePartWorldTransformResolver`, `MirrorUtility`, `CurveAdapter`, `MarchingCubesExtractor`, `DefinitionValidator`, `ResolvedBody`, `ResolvedLimb`, and the existing CC-043, CC-045, CC-054, CC-056A, and CC-056B records. Unity execution was not required because this synthesis changed no runtime code.

## Open Evidence Gaps

- No focused Unity run was performed for the unresolved findings because this operation only changed Markdown task records.
- The current Unity runtime test discovery limitation remains documented in existing task records.
- CC-087 must prove sampling-density-invariant semantic binding and deterministic snapshot identity.
- CC-088 must prove managed/reference and portable production parity before deleting managed production APIs.
- CC-089 must run malformed-definition tests, including null entries, duplicate IDs, missing parents, and clone behavior.

## Next Evidence

Implement CC-087 in the smallest resolved-snapshot slice, then run focused Unity morphology, resolver, skeleton, and SDF parity tests before starting CC-088.
