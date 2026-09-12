# CreatureCreator — Code Health, Duplication & Architecture Consolidation Audit

**Report ID:** `CC-AUDIT-HEALTH-20260912-4D6F21A8`
**Suite:** `CC-AUDIT-SUITE-20260912-9F42C6D1`
**Branch base audited:** `b9b0fa0cfce4cdc0e3e6c2f409a760f4cf887a99`
**Primary sources:** current runtime/editor architecture, `.github/skills`, ADRs, recent audit corpus, TSK-0095/0098/0104/0132/0156 and related records

## Executive assessment

The repository has already removed several classes of duplicated mechanics. The current consolidation opportunity is therefore not broad “DRY” work; it is to consolidate **conceptual contracts that currently have multiple implementations or names**.

The codebase should resist the temptation to abstract every similarly shaped method. The highest-value consolidation points are identity/revision, gesture lifetime, generation configuration, skeleton compatibility, pose buffers, and ownership.

## Findings

### CH-01 — Skeleton compatibility has multiple semantic names for one contract

**Severity:** P2  
**Confidence:** 96%  
**Owner:** TSK-0203 / skeleton contract

Rig build captures a snapshot, pose application checks `HasSameBoneOrder`, and renderer binding performs another capture plus compatibility check.

The checks serve distinct boundaries, so eliminating one entirely is not recommended. They should, however, share one named compatibility predicate and one signature representation.

### CH-02 — Revision identity should replace scattered fingerprints

**Severity:** P2  
**Confidence:** 97%  
**Owner:** TSK-0095/0098/0104

Editor placement already has a body fingerprint. Scheduler uses sequence numbers. Rig caches a validated pose skeleton reference. Renderer relies on structural equality.

These solve different local problems but are all variants of “is this artifact still compatible with the current source?”

Introduce a layered identity:

```text
AuthoringRevision
GenerationRevision
SkeletonRevision
PresentationRevision
```

Each derived artifact records the minimum revision that invalidates it.

### CH-03 — Gesture/session code is now sufficiently duplicated to justify a shared primitive

**Severity:** P2  
**Confidence:** 98%  
**Owner:** TSK-0098

Body sample drag and limb joint drag follow essentially the same lifecycle. Placement drag follows nearly the same model with a surface-frame calculation inserted.

This is a safe consolidation because the behavior is already intentionally standardized.

### CH-04 — Default/fallback policies are more dangerous than repeated small helpers

**Severity:** P2  
**Confidence:** 97%  
**Owner:** data/config/binding owners

Repeated fallback patterns appear more architecturally risky than repeated utility methods:

- missing morphology radius → default;
- invalid scalar configuration → clamp;
- absent palette/material → null/default;
- terminal rotation → rest rotation.

Each fallback changes semantics. They need explicit policy ownership and diagnostics more than they need generic code.

### CH-05 — Public types contain Unity-oriented names at some boundaries while core math is pure

**Severity:** P3  
**Confidence:** 93%  
**Owner:** animation architecture

The pure math separation is good, but interfaces such as `PosedSkeleton` and `BoneSegmentInfluence` still use Unity `Vector3` because the project is Unity-first.

This is not a smell to “fix” automatically. The project should retain Unity value types in deterministic pure logic unless portability outside Unity becomes a requirement. Introducing a second Vector3 abstraction would be harmful bloat.

### CH-06 — Mutable builder-side `Bone`/`Skeleton` is the remaining major legacy escape hatch

**Severity:** P2  
**Confidence:** 95%  
**Owner:** TSK-0156 lineage

The published runtime path is increasingly snapshot-based, but builder-side mutation remains available for inference/construction.

This is acceptable as a bounded construction phase. The eventual target is:

```text
mutable builder graph
    → validate
    → immutable SkeletonSnapshot
```

rather than exposing mutable skeletons farther downstream.

### CH-07 — Read-only wrappers should not be mistaken for immutable values

**Severity:** P2  
**Confidence:** 97%  
**Owner:** TSK-0095

`IReadOnly*` APIs prevent many accidental writes but do not make nested objects immutable. This distinction repeatedly appears in the repository's design language.

Document the difference between:

- read-only view;
- detached ownership;
- immutable value object.

Use the strongest term that the code actually guarantees.

### CH-08 — Historical compatibility shims should continue to be deleted aggressively

**Severity:** P2  
**Confidence:** 97%  
**Owner:** architecture maintainers

The repository has already removed old `MainMesh`-style shims and retired CC markdown as active authority. Preserve the greenfield preference: once a migration is accepted and validated, remove the old pathway rather than carrying it indefinitely.

### CH-09 — The project has a healthy “single owner per mechanism” trend

**Severity:** positive architecture signal  
**Confidence:** 96%

Current examples include centralized mirror math, quaternion canonicalization, one resolved morphology model, shared generation config, and deterministic skeleton indexing.

New abstractions should reinforce this: one owner per semantic rule, even where multiple adapters consume it.

### CH-10 — Avoid generic “utility” buckets for domain policy

**Severity:** P2  
**Confidence:** 94%  
**Owner:** all maintainers

The repository has a natural temptation to add more helpers to `Common`. Keep domain semantics in the domain owning them. Shared `Common` should contain genuinely cross-cutting primitives such as finite checks/canonicalization, not morphology or animation policy.

## Consolidation map

| Concept | Current multiplicity | Recommended owner |
|---|---|---|
| Skeleton identity | multiple checks | `SkeletonCompatibility`/signature |
| Editor gesture lifecycle | 3+ implementations | shared editor gesture session |
| Generation defaults | config + request overrides | effective config/signature |
| Pose state | position pose + rotation cache | indexed `PoseBuffer` |
| Object ownership | subsystem-local | presentation transaction/owner graph |
| Shape fallback | historical multiple sites | canonical morphology policy |
| Validation state | prose/comments | structured task validation record |

## What not to consolidate

Do not merge pure LBS, FABRIK, morphology resolution, and Unity adapters into one “animation utility.” Their current separation is one of the repository's strengths.

Do not merge editor and runtime palette logic by adding editor dependencies to runtime. The current shared runtime-safe asset type is the correct boundary.

Do not abstract every test fixture into a framework. Readable fixture code is valuable in a correctness-heavy procedural generator.

## Conclusion

The codebase is in a favorable consolidation phase. The correct next step is to remove **duplicate policy**, not merely duplicate lines. A few named contracts—revision identity, compatibility, pose buffering, gesture lifetime, and ownership—can eliminate a large amount of future drift while keeping the code simple.
