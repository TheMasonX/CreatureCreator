# CreatureCreator Audit Synthesis, 2026-09-04

## Executive Summary

This full reconciliation covers the six supplied audits listed in the source ledger below. It preserves the 2026-09-03 audit wave as historical evidence and incorporates the independent 2026-09-04 deep-dive findings. This pass changes audit and task evidence records only. It does not change Runtime or Editor code.

The audits do not justify a new CC task family. They confirm the existing consolidation strategy and add a small set of concrete acceptance gates:

- `TSK-0067` / CC-063 and `TSK-0014` / CC-014 own a confirmed fast-culling regression risk. The evaluator and root sampling shortcut must consume `Cullable`, and gradient estimation must honor the archived `+inf` contract.
- `TSK-0095` / CC-091 remains the single owner for snapshot authority, generated-data immutability, SDF compiler correspondence, and generation-stage boundaries.
- `TSK-0094` / CC-090 remains Done only for its validated finite-check slice. Shape fallback, quaternion canonicalization, mirror call-site wiring, and named tolerance mechanics remain open under CC-090 and TSK-0105.
- `TSK-0093` / CC-089 remains InProgress for malformed-definition totality. The 9/3 null-`Parts` implementation and later PlayMode evidence are retained, but reserved-ID and non-throwing boundary coverage remain open.
- `TSK-0098` / CC-094 and `TSK-0104` remain the owners for editor decomposition, preview ownership, and async lifetime contracts.
- CC-042 remains the narrow owner for ClonePartAsChild documentation. The stale `CanonicalJsonWriter` schema comment is recorded as related documentation drift, not a new ticket.

The 2026-09-03 critical-review claims about an incorrect generation-budget oracle and undiscovered PlayMode tests are stale at this fixed point. The corrected oracle, full `ProceduralCreature.Tests.Runtime` PlayMode suite, and EditMode suite have since passed with 443/443 and 115/115 respectively. The original reports remain valuable because they explain the correction and the prior evidence gap.

No P0 finding was identified. The material current risks are three P1 correctness gates and several P2 consolidation or contract gaps. No task is marked Done by this report.

## Scope and Fixed Point

- **Mode:** Full reconciliation.
- **Date:** 2026-09-04.
- **Repository:** `d:/UnityProjects/CreatureCreator`.
- **Branch:** `main`.
- **Fixed point:** current repository state after the corrected runtime review and editor-decomposition review commits. The supplied 9/3 audits cite `171e4d31eb67db1a30aa4a6f3661508534931efb`.
- **Working tree:** existing user and repository changes were preserved. This pass adds this synthesis report and MemorySmith evidence comments only.
- **Code changes:** excluded.
- **Unity execution:** no new Unity execution was required for this record-only synthesis. The report records the latest supplied validation evidence and does not claim that this synthesis itself ran Unity tests.

## Source Ledger

| ID | Repository-relative source | Scope | Read |
| --- | --- | --- | --- |
| S01 | `docs/audits/creaturecreator-deep-dive-audit-26-09-03-17-31-00.md` | Runtime architecture, SDF culling, snapshot and ownership contracts | Yes |
| S02 | `docs/audits/creaturecreator-deep-dive-audit-2026-09-04.md` | Shape fallback, mirror wiring, quaternion canonicalization, serializer and documentation drift | Yes |
| S03 | `docs/audits/creaturecreator-critical-peer-review-2026-09-03.md` | Runtime-review oracle correction and PlayMode discovery gap | Yes |
| S04 | `docs/audits/creaturecreator-implementation-review-2026-09-03.md` | Runtime audit-wave completeness and task disposition | Yes |
| S05 | `docs/audits/creaturecreator-council-review-synthesis-pattern-coverage-2026-09-03.md` | Cross-path coverage and acceptance-gate expansion | Yes |
| S06 | `docs/audits/creaturecreator-audit-synthesis-2026-09-03.md` | Prior six-audit reconciliation and task ownership baseline | Yes |

Required repository contracts were read: `Assets/Scripts/README.md`, `.github/skills/task-tracker/SKILL.md`, `.github/skills/ste-technical-writing/SKILL.md`, `docs/tasks/active-tasks.md`, the matching CC tickets, archived CC-063/CC-064, and the relevant MemorySmith task records.

## Verification Method

External claims were checked against the cited source paths, the nearest task records, and the prior synthesis. Results use independent severity and confidence axes:

- **P1:** correctness, allocation, or architectural contract risk that can affect generated output or runtime reliability.
- **P2:** meaningful maintainability, testability, performance, or ownership risk.
- **P3:** documentation or low-impact cleanup.
- **High confidence:** direct source evidence plus matching contract or validation evidence.
- **Medium confidence:** direct source evidence with a missing reproduction or policy decision.

The 2026-09-04 audit was a read-only source review and did not run Unity or `dotnet build`. The later repository handoff supplied the corrected Unity evidence used below: full Runtime PlayMode `443/443` and EditMode `115/115`.

## Accepted Findings and Durable Ownership

| Finding | Severity | Result | Classification | Durable owner | Disposition |
| --- | --- | --- | --- | --- | --- |
| F-01: SDF evaluator culls by bounds without requiring `SdfOperation.Cullable` | P1 | Confirmed | Correction / regression | TSK-0014 / CC-014, with CC-063 historical gate | Add evaluator, subtree, and root-shortcut parity tests. Do not remove the metadata. |
| F-02: Root `DensityGrid` shortcut repeats the culling omission | P1 | Confirmed | Extension | TSK-0014 / CC-014 | Require root cullability and test ellipsoid and composite-root cases. |
| F-03: `DensityGrid.EstimateGradient` subtracts `+inf` samples | P1 | Confirmed | Extension / regression | TSK-0065 / CC-061, with CC-064 historical contract | Add finite, one-sided, and invalid-gradient policy tests. Never return NaN to winding logic. |
| F-04: Public native buffers are writable despite read-only contracts | P2 | Confirmed | Extension | TSK-0095 / CC-091 | Expose read-only native views and retain private mutable job handles. Test the public boundary. |
| F-05: Whole-creature and individual-part SDF compilation duplicate primitive emission | P2 | Confirmed | Extension | TSK-0095 / CC-091 plus TSK-0105 | Extract one concrete emission helper after snapshot correspondence is defined. Do not add an IR framework. |
| F-06: Snapshot construction still permits raw hierarchy re-resolution | P2 | Confirmed | Corroboration / extension | TSK-0095 / CC-091 | Inventory remaining `FindPart`, parent walks, and `ResolvedLimb.Resolve` calls. Use one existing resolved context. |
| F-07: `(CreaturePart, SdfProgram)` is a live-DNA/generated-program mismatch | P2 | Confirmed | Extension | TSK-0095 / CC-091 | Replace internal correspondence with resolved part identity or snapshot-owned data. Preserve compatibility wrappers only at outer boundaries. |
| F-08: SDF operation metadata and culling proof data lack a clear immutable boundary | P2 | Partially confirmed | Extension | TSK-0095 / CC-091, with TSK-0014 | Inventory `ConsumerUnionIndex` and choose removal or tested runtime use before adding more metadata. |
| F-09: Global influence radius and anonymous `1e-4f` culling margin are coarse and unnamed | P2 | Confirmed | Extension | TSK-0008 / CC-008 and TSK-0014 / CC-014 | Fix culling correctness first, then measure and name the tolerance or derive subtree margins. |
| F-10: Legacy shape fallback is copied across deserializer, canonicalizer, resolver, and editor | P2 | Confirmed | Corroboration / extension | CC-043 / CC-090 | Decide capsule legacy semantics, then centralize the mapping and parity-test all sites. |
| F-11: Quaternion normalization/quantization order differs and one path lacks a degenerate guard | P2 | Strong evidence | Net-new mechanism within existing owner | CC-090 / TSK-0105 | Add a near-degenerate attachment test and share the guarded helper only if semantics match. |
| F-12: `MirrorUtility` exists but several callers duplicate reflection matrices | P2 | Confirmed | Correction | TSK-0105, with CC-014/CC-059/CC-052 policy ownership | Wire existing math into callers. Keep symmetry, binding, and mesh policy in their owners. |
| F-13: `IDnaSerializer` is a shallow, inconsistently used seam | P2 | Confirmed | Defer pending policy | CC-090 | Decide whether a second serializer is planned. Do not create a singleton merely to hide duplicate fields. |
| F-14: `CanonicalJsonWriter` schema example omits current shape and mesh fields | P3 | Confirmed | Extension | CC-042-related documentation cleanup | Preserve as explicit doc drift. It does not indicate a reader/writer mismatch. Do not create a separate P3 ticket. |
| F-15: Malformed-definition coverage extends beyond validator code | P1/P2 | Confirmed | Corroboration | TSK-0093 / CC-089 | Keep null-`Parts` defensive behavior and reserved-ID, non-throwing, and report-only gates open. |
| F-16: Editor and async ownership findings remain open | P1/P2 | Confirmed | Corroboration | TSK-0098 / CC-094 and TSK-0104 | Preserve request identity, bounded work, generated-object disposal, gesture ownership, and domain-reload gates. |
| F-17: 9/3 test oracle and discovery findings | P1 process risk | Corrected | Correction | TSK-0093 / TSK-0095 evidence history | The wrong constant was corrected and the later fully qualified test run passed. Do not reopen as an active defect. |

## Standards Assessment

The repository still supports the documented architecture:

- `CreatureDefinition` remains authoritative DNA.
- Runtime generation uses resolved data in the main path and does not acquire editor dependencies.
- `DefinitionValidator` remains report-only and does not silently repair definitions.
- Canonicalization remains the owner of deterministic quantization and ordering.
- Negative SDF values remain inside and positive values remain outside.
- The existing task strategy favors concrete helpers and bounded decomposition over generic service frameworks.
- The corrected Unity evidence is now strong enough to treat the 9/3 discovery problem as historical rather than an active gate.

Standards risks remain at the following boundaries:

- Culling proof metadata must be consumed by every culling shortcut.
- The archived `+inf` field contract must include gradient and winding consumers, not only interpolation and appearance.
- A resolved snapshot must prevent downstream raw-DNA reinterpretation and mutable live-object correspondence.
- Public generated buffers must not contradict their read-only documentation.
- Completed utility tasks must not be described as owning unresolved mirror, shape, or quantization work.

## Specification Assessment

These questions remain product or schema decisions, not implementation facts:

- Whether legacy `CapsuleHeight` maps to `PrimarySize`, an intentional unit height, or a migration-only default.
- Whether `IDnaSerializer` is a real future format seam or should be removed.
- Whether the X symmetry plane remains the permanent product contract.
- Whether the generation budget is defined in cells, corners, or total allocation. The corrected current math is pinned, but the product meaning remains under CC-091.
- Whether cancellation interrupts active worker computation or only invalidates its result.
- Whether domain reload clears and regenerates accepted preview objects.
- Whether revision identity remains canonical-JSON based or gains a direct semantic hash.

## Task Disposition

| Durable record | Decision | Rationale |
| --- | --- | --- |
| TSK-0014 / CC-014 | Keep InProgress | Owns portable SDF evaluator and sampling correctness. Add `Cullable` consumption and ellipsoid parity gates. |
| TSK-0065 / CC-061 | Keep Backlog | Owns extraction quality and gradient/winding evidence independently of editor interaction. |
| TSK-0093 / CC-089 | Keep InProgress | Null-`Parts` behavior is implemented and validated, but reserved-ID and full malformed-consumer contracts remain open. |
| TSK-0094 / CC-090 | Keep Done for finite-check scope | The 9/4 report corrects the stale “finite checks remain duplicated” claim. Other utility families are not closed by that task. |
| TSK-0095 / CC-091 | Keep InProgress | Owns snapshot authority, immutable generated inputs, program correspondence, stage boundaries, and grid semantics. |
| TSK-0098 / CC-094 | Keep InProgress | Owns editor decomposition and interaction ownership. |
| TSK-0104 | Keep Backlog | Owns bounded asynchronous preview work and generated Unity-object lifetime. |
| TSK-0105 | Keep Backlog | Owns keyed lookup, geometry mechanics, mirror wiring, and only mechanically identical helpers. |
| CC-042 | Keep Backlog | The ClonePartAsChild comment remains a separate documented cleanup. Canonical JSON comment drift is related but not a reason to widen the task silently. |
| CC-063 / CC-064 | Preserve archived Done history, add regression evidence to live owners | The archived implementation evidence remains useful, but the new source findings require a current regression gate. |

No new CC key was created. No task was marked Done from source inspection alone.

## Fixed, Duplicate, Rejected, and Unresolved Claims

### Corrected or fixed at this point

- The 9/3 generation-budget assertion was corrected from `16_972_609` to `16_974_593`, matching `(256 + 1)^3` corner samples.
- The later fully qualified Runtime PlayMode run passed `443/443`; EditMode passed `115/115`. The 9/3 “zero discovered tests” result is retained as a historical infrastructure observation, not current validation.
- Snapshot-owned body appearance, bounds, generation settings, symmetry, and mesh correspondence remain supported by the prior task evidence.
- TSK-0094’s finite-check slice remains valid and is not reopened by the 9/4 duplication claims.
- The archived CC-063/CC-064 interpolation, appearance, and finite-field evidence remains valid for the covered cases.

### Duplicate or consolidated

- SDF culling, non-finite gradient handling, and extraction winding are one fast-field correctness track, with evaluator ownership under CC-014 and extraction evidence under CC-061.
- Shape fallback, quaternion quantization, mirror wiring, and palette/geometry helpers are utility mechanics only where their semantics match. Domain policy remains in CC-043, CC-052, CC-059, and CC-091.
- Snapshot hierarchy traversal, generated correspondence, compiler duplication, and native buffer exposure are one generation-boundary track under CC-091.
- Preview scheduler, result correlation, Unity-object lifetime, and editor coordination remain one async/editor boundary across TSK-0104 and CC-094.

### Rejected or not promoted

- No new CC ticket was created for the serializer interface, canonical-writer documentation, or each individual utility duplication.
- No architecture replacement, generic IR, service hierarchy, or serializer singleton was endorsed.
- No healthy primary snapshot path was reopened because the 9/3 audit initially found a malformed fallback concern.
- No completed finite-check, async foundation, or corrected Unity test evidence was invalidated without new mechanism evidence.

### Unresolved

- SDF culling and gradient fixes require implementation and focused Unity parity/topology tests. This synthesis does not claim those fixes are complete.
- The remaining raw hierarchy and live-correspondence paths need a complete call-site inventory before CC-091 can close.
- The shape fallback and serializer decisions need explicit schema/product decisions.
- Editor SceneView, generated-object disposal, and domain-reload behavior still require real Unity evidence under CC-094/TSK-0104.

## Next Evidence

1. CC-014: add an ellipsoid counterexample proving reference, evaluator, subtree, and root-grid paths agree when `Cullable` is false.
2. CC-061: add finite, one-sided, and invalid gradient tests at `+inf` boundaries and rerun topology/winding fixtures.
3. CC-091: expose read-only generated buffers, remove live `CreaturePart` correspondence from internal generation data, and inventory remaining raw hierarchy resolution.
4. CC-043/CC-090: decide legacy capsule semantics and add cross-boundary shape fallback parity tests.
5. CC-090/TSK-0105: add the near-degenerate quaternion fixture and wire existing `MirrorUtility` callers without moving domain policy.
6. TSK-0093: retain malformed-definition tests for null `Parts`, reserved `BodyId`, non-throwing envelope checks, and report-only behavior.
7. CC-094/TSK-0104: run the queued-work, result-identity, generated-object replacement, gesture, collider, and domain-reload gates.
8. CC-081: rerun the canonical end-to-end morphology fixture after boundary changes.

## Validation of This Record

- Supplied audit inventory: six of six files exist and were read.
- MemorySmith ownership query: completed for TSK-0093, TSK-0094, TSK-0095, TSK-0098, TSK-0104, TSK-0105, TSK-0014, TSK-0065, and TSK-0008.
- Existing Unity evidence: corrected Runtime PlayMode `443/443`; EditMode `115/115`; no new Unity run was performed for this report.
- New task creation: none. No CC key was reused or duplicated.
- Historical record validation: pending immediately after this edit via `task_validate.py --strict` and `git diff --check`.

## Assumptions and Uninspected Artifacts

The 9/4 deep-dive audit was performed against a fetched `main` snapshot and did not independently run Unity or `dotnet build`. Its source findings are therefore recorded as verified static claims, while runtime behavior remains an acceptance-gate item. Unity logs, screenshots, external pages, and attachments not embedded in the six supplied Markdown files were not independently inspected.
