# CreatureCreator — Exhaustive Deep-Dive Code Audit

**Audit ID:** `CCA-20260825-CD01BE4669460E7D`
**Repository:** `TheMasonX/CreatureCreator`
**Audited tip:** `1e1a57569a4e66897d04bcb7d45ecce43cc24b09`
**Tip commit:** `Harden resolved morphology snapshots and anchor validation`
**Date:** 2026-08-25
**Scope:** repository structure, runtime definition/morphology/generation/skeleton/serialization paths, tests, ADRs, audit history, and all tracked `CC-###` work relevant to the audited seams.

## Executive assessment

The repository has made substantial architectural progress since the preceding audit, especially around the resolved-morphology migration. `ResolvedBody` and `ResolvedLimb` now provide genuinely useful immutable derived snapshots, SDF and skeleton consumers have migrated substantially, semantic attachment validation has started, and the task board is explicitly tracking several pre-existing failures.

However, the migration has **not yet reached architectural closure**. The most important finding is that the codebase now contains a canonical morphology contract on paper, but several high-value consumers still bypass it and derive attachment/placement semantics directly from DNA. This is more dangerous than ordinary duplication because the project is approaching animation, mesh binding, and interactive surface attachment—the exact stages where independent derivations will turn into persistent semantic drift.

The current state should therefore be treated as:

> **Resolved geometry is real, but resolved morphology is not yet the single source of semantic truth.**

I found no reason to introduce a large generic framework. The better path is to finish a small set of explicit seams, remove duplicated derivation, strengthen invalid-input contracts, and then use a single end-to-end verification gate before expanding animation/binding features.

---

# 1. Highest-priority findings

## P1-01 — `CreaturePartWorldTransformResolver` still re-derives limb terminal geometry outside `ResolvedLimb`

**Severity:** P1 architectural correctness

**Evidence:** `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`

The canonical resolver currently composes child-at-tip placement using:

```csharp
p.Limb.Joints[p.Limb.Joints.Count - 1].Position
```

and `ResolveChildFrameToCreatureSpace` repeats the same raw-joint terminal lookup.

This directly conflicts with the intent of ADR-007 / CC-056A: resolved morphology is supposed to be the single derivation source for generation subsystems, and consumers should not re-derive geometry from authored data.

Today the value is numerically equivalent because `ResolvedLimb` intentionally preserves the authored joint polyline. That equivalence is temporary. The moment CC-055 introduces smoothing/resampling or CC-056B gives the terminal socket stronger semantic meaning, the transform resolver becomes a second geometry interpreter.

### Why this matters

Child placement is not a cosmetic consumer. It is a foundational semantic transform used by SDF generation, skeleton inference, editor placement, bounds validation, and future attachment resolution. A divergence here propagates through the entire creature.

### Correction

Extend `CC-056B` so child-at-tip frame resolution consumes `ResolvedLimb.TerminalSocket` rather than `LimbChain.Joints[^1]`. Add parity tests proving:

- raw-DNA and resolved placement are identical for current polyline v1;
- future resolved-terminal semantics are consumed at one seam;
- editor child placement and generation use the same frame.

Do not add another resolver alongside the existing one.

---

## P1-02 — `SkeletonInferrer` still contains legacy attachment derivation that bypasses the resolved semantic layer

**Severity:** P1 architectural correctness

**Evidence:** `Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs`

Two separate bypasses remain:

1. child-of-limb parent bone IDs are calculated from `parentPart.Limb.Joints.Count - 2`;
2. Body-rooted attachment resolves the nearest raw Body sample by Euclidean distance in `ResolveBodyParentBoneId`.

The second path is the more consequential defect. It is explicitly identified by the existing CC-056A residual notes and CC-076, but it should be treated as a semantic migration blocker rather than ordinary backlog.

Current behavior effectively says:

```text
authored limb root position
    -> nearest current body sample
    -> bone ID
```

That is a heuristic. It does not preserve authored semantic attachment identity.

### Failure mode

A body edit can move the nearest sample even though the intended attachment point has not changed semantically. That can silently change the skeleton hierarchy after regeneration.

### Correction

Make `CC-056B` establish a canonical attachment object, including at minimum:

```text
Body segment/sample identity
normalized longitudinal t
radial coordinate or body-frame offset
surface offset
roll/orientation convention
```

Then make `CC-076` the only service that maps that semantic attachment to a bone. `SkeletonInferrer` should call the service, not search the body.

`CC-076` should remain a separate seam, but it must no longer permit a fallback implementation to quietly become authoritative.

---

## P1-03 — The validator is not actually total on malformed definitions

**Severity:** P1 correctness

The task board correctly tracks `CC-082`, but the underlying problem is broader than one `ToDictionary` call.

`CreatureDefinition.HasParentCycle()` uses:

```csharp
Parts.ToDictionary(p => p.Id, p => p)
```

so duplicate part IDs cause an exception before validation can report the duplicate-ID issue. This is the known `CC-082` failure.

More importantly, `DefinitionValidator.ValidateParentsAndCycles()` also constructs an ID set with:

```csharp
foreach (CreaturePart part in definition.Parts)
    idsById.Add(part.Id);
```

without a null-part guard. A malformed definition containing a null element can therefore throw rather than return validation issues.

### Contract problem

The validator describes itself as report-only and defensive. A malformed model must not turn validation itself into a crash path.

### Correction

Do not patch only the one dictionary call. Introduce one small tolerant indexing primitive for validation, conceptually:

```text
PartIndex
  unique/duplicate classification
  first/representative lookup
  missing-parent lookup
```

The index must preserve duplicate information rather than forcing an invalid definition into a `Dictionary<string, CreaturePart>`.

Then update `HasParentCycle()` so it can operate on invalid definitions without throwing. This should make `CC-082` a structural repair rather than a one-line exception suppression.

---

## P1-04 — `CC-083` is not merely a missing test; the validator has the wrong semantic code path

**Severity:** P1 correctness / diagnostics

Current parent validation reports `ParentId == null` as `InvalidBodyParent`. The existing task `CC-083` correctly identifies that a non-Body part with no parent should be represented as a missing-parent/structural-parent error instead.

This matters because the diagnostic is part of the schema contract: callers use validation codes to decide whether an input is malformed, attachable, or rooted incorrectly.

### Correction

Define the structural rule explicitly:

```text
Body-rooted part      -> ParentId == BodyId or explicit Body attachment semantics
Non-rooted normal part -> ParentId required
Body itself            -> implicit root, not a CreaturePart parent case
```

Then give invalid parent state a single, semantically correct validation code. Keep `CC-083`; do not fold it into a generic "parent validation cleanup" ticket.

---

## P1-05 — `CreatureDefinition.Clone()` and `DefinitionCanonicalizer` have stronger invalid-input assumptions than their documented contracts

**Severity:** P1/P2 boundary robustness

`CreatureDefinition.Clone()` does:

```csharp
Parts = Parts.Select(p => p.Clone()).ToList()
```

with no null-element defense.

`DefinitionCanonicalizer.Canonicalize()` subsequently iterates the cloned parts assuming all entries are non-null.

The canonicalizer documentation explicitly describes invalid input as a programmer-error case that should produce `DomainException`, not a random `NullReferenceException`. Today some malformed definitions fail lower in the stack with the wrong exception type.

### Correction

Strengthen the boundary contract instead of adding scattered null checks:

- `CreatureDefinition.Clone()` should either reject null entries with a domain-specific exception or deliberately preserve them for diagnostics; choose one policy and document it.
- `DefinitionCanonicalizer` should convert malformed model structure into `DomainException`, not framework exceptions.
- Keep canonicalization a non-repair operation.

This is especially important as editor scratch copies become more heavily used during direct manipulation.

---

# 2. Resolved morphology layer: what is good, and what is still missing

## P1-06 — `ResolvedBody` and `ResolvedLimb` still duplicate the same polyline metric algorithm

**Severity:** P2 design / maintainability, P1 if extended to more centerline consumers

Both types independently implement essentially the same:

- segment-length calculation;
- total-length accumulation;
- normalized cumulative arc length;
- degenerate zero-length handling;
- terminal pinning to exactly `1`.

The duplication is currently small, but it is exactly the sort of "almost identical geometry rule" that caused the earlier architecture to drift.

### Correction

Extract only the invariant numeric operation, not a generic morphology framework. A small internal helper such as `PolylineMetrics.Compute(...)` can produce:

```text
segment lengths
cumulative normalized arc length
nondimensional total length
```

`ResolvedBody` remains responsible for body-specific radius data; `ResolvedLimb` remains responsible for thickness.

This preserves the explicit domain types while eliminating another future divergence point.

---

## P1-07 — `CC-056A` is marked Done even though its stated "tangent/frame" contract is not actually contained in `ResolvedBody`

**Severity:** P2 architectural contract drift

The ticket scope says `ResolvedBody` includes:

- samples;
- centerline;
- tangent/frame;
- normalized arc length;
- radius.

The implementation instead stores geometry metrics and leaves frame derivation in `BodyFrameResolver`. `BodyFrameResolver` now accepts `ResolvedBody`, which is good, but it still recomputes the transport frame each call.

The implementation is therefore better than the old architecture, but the ticket wording overstates what the resolved object actually owns.

### Correction

Do one of two things explicitly:

**Preferred:** redefine `ResolvedBody` as the canonical geometry snapshot and let a deterministic `BodyFrameResolver` remain the canonical frame derivation service, with one explicit cache/lifetime policy.

**Alternative:** if frame data is truly part of resolved morphology, add it to the snapshot and guarantee it is generated exactly once.

Do not leave the ticket saying the model owns something it does not actually own.

---

## P1-08 — Body appearance still derives directly from authored sample data

**Severity:** P2 semantic consistency

The CC-056A implementation notes explicitly leave `BodyVerticalGradientSampler` and `PartAppearanceSampler` iterating authored samples because appearance was declared out of scope.

That is reasonable as a migration staging decision, but the distinction needs to be made explicit:

```text
geometry centerline / spacing semantics -> ResolvedBody
appearance sampling domain               -> currently authored sample list
```

As soon as CC-055 changes centerline/sampling fidelity, the visual appearance can cease to align with the actual generated body geometry.

### Correction

Create a follow-up task to make appearance sampling consume the same normalized arc-length domain exposed by `ResolvedBody`. This does not require adding gradient state to the resolved snapshot today.

Suggested new work: **CC-085 — Migrate body appearance sampling to resolved morphology arc length**.

---

# 3. Geometry / SDF design debt

## P2-01 — Two distinct blend policies are currently encoded as unrelated `0.5f` constants

**Evidence:** `SdfProgramBuilder.cs`

The code defines:

```csharp
BodySampleBlendFactor = 0.5f
LimbSampleBlendFactor = 0.5f
```

They currently happen to be identical, but live as separate constants. This is a smell because it hides whether the equality is intentional policy or coincidence.

There are also two different notions of "blend" in the pipeline:

- smooth union between neighboring body/limb metaballs;
- the part-to-creature union controlled by `Shape.SmoothBlendRadius` or `LimbChain.BlendRadius`.

The names make this distinction clearer than before, but the underlying policy is still not centralized.

### Correction

Replace the duplicate `0.5f` constants with an explicit morphology sampling/blending policy, or—if the factor is deliberately experimental—document it as a calibration constant with tests that pin its numerical intent.

Do **not** make this another public authoring field unless Spore-like authoring behavior requires it.

---

## P2-02 — `CC-039` is now stale/partially superseded

The task board still contains:

> CC-039 — Limb metaball smooth blend radius as an authored value

while the implementation already has `LimbChain.BlendRadius`, and `CC-049` explicitly established that as the authoritative replacement for inert `Shape.SmoothBlendRadius`.

This is exactly the sort of task duplication the audit should eliminate.

### Correction

Retire or rewrite `CC-039` as a policy/test task, e.g.:

> **Define and verify limb internal metaball smoothing policy**

The user-facing authored blend radius belongs to `CC-049`; what remains unresolved is internal sample-to-sample smoothing semantics.

---

## P2-03 — `SdfProgramBuilder` contains compatibility fallback logic that blurs the schema migration boundary

The primitive mapping still carries legacy fallback behavior such as:

```text
Radius <= 0 -> legacy PrimarySize
CapsuleHeight <= 0 -> 1
EllipsoidRadii invalid -> legacy PrimarySize
BoxHalfExtents invalid -> legacy PrimarySize
```

This may be necessary during migration, but it means the generator is doing schema interpretation in addition to compilation.

### Correction

Keep legacy interpretation in one canonical normalization/canonicalization layer. The production compiler should consume already-canonical shape semantics.

This is particularly important for `CC-045`: removing the legacy managed SDF is much cleaner if the portable compiler is not simultaneously acting as a compatibility parser.

---

# 4. Attachment and transform architecture

## P1-09 — Attachment semantics still have two sources of truth: transform hierarchy and reserved `ParentAttachment`

`CreaturePartWorldTransformResolver` explicitly describes `ParentAttachment` as "reserved-but-inert". That is a sane migration step, but it means the schema already contains semantic attachment data that the authoritative transform path currently ignores.

The dangerous part is not that it is inert; the dangerous part is the possibility of future consumers reading it directly instead of going through the canonical resolver.

### Correction

`CC-056B` should define one `ResolvedAttachment` contract and make every attachment-consuming subsystem call it. Until then, keep `ParentAttachment` deliberately writeable only through its canonical editor command path and keep validation strict.

Add a regression test that proves no generation/skeleton/mesh consumer reads `ParentAttachment` directly.

---

## P2-04 — `CreaturePartWorldTransformResolver` does repeated parent-chain lookup with fresh allocations

Every resolution builds a `List<CreaturePart>` and a `HashSet<string>` and repeatedly calls `FindPart`, which itself linearly scans `Parts`.

This is acceptable at current creature sizes, but it becomes an avoidable O(depth × part-count) pattern when many consumers repeatedly resolve many parts.

### Correction

Do not introduce a permanent mutable transform cache. Instead, let the future resolved-morphology phase construct one transient, deterministic part index per resolution pass or per `ResolvedCreature` build.

This naturally belongs next to `CC-056B`, not as a separate optimization subsystem.

---

# 5. Primitive obsession / weak domain boundaries

## P2-05 — String IDs remain overloaded across parentage, bone identity, diagnostics, and attachment lookup

Examples include:

```text
ParentId : string
Part.Id : string
bone IDs : string
Body bone IDs : synthesized string concatenations
```

Strings are reasonable for serialization, but internally they are increasingly carrying different concepts.

### Risk

A future animation/binding feature can accidentally treat a part ID as a bone ID or a synthesized terminal-bone string as an actual semantic attachment identity.

### Correction

Do not redesign the serialized schema around opaque identifier classes. Instead introduce small runtime semantic types where they buy real safety:

```text
PartId
BoneId
BodySampleId
```

or at minimum centralize construction/parsing of synthesized bone IDs.

The first target should be `CC-076`, because that is where semantic mapping becomes explicit.

---

## P2-06 — `PartType` is still carrying category semantics that will not scale cleanly into animation capabilities

The previous audit already suggested separating broad anatomical category from capabilities. The task board still has CC-010 open for semantic animation queries.

Do not expand `PartType` into a giant enum as locomotion and animation arrive.

Preferred shape:

```text
PartType = editor/anatomy classification
Resolved capabilities = derived morphology facts
```

This avoids another round of schema migration when animation queries need concepts such as support, manipulator, mouth, sensor, etc.

---

# 6. Determinism and hidden assumptions

## P2-07 — Nearest-sample attachment needs an explicit tie/semantic policy before it is removed

`ResolveBodyParentBoneId` uses `<` when comparing squared distance, so equal-distance ties keep the first body sample by authoring order.

That is deterministic, but it is an **arbitrary** semantic rule.

The correct fix is not to add a different tie-breaker. The correct fix is to retire nearest-sample semantics behind `BodySurfaceAnchor` / `CC-056B`.

Until then, document the behavior as a transitional heuristic so it is not mistaken for part of the morphology contract.

---

## P2-08 — Mirroring policy is explicit but distributed across several systems

The mirror convention is duplicated conceptually in:

- SDF compilation;
- skeleton inference;
- mesh handling;
- future bone resolution.

The code is currently consistent enough, which is good, but the same "reflection across creature X=0" rule is being encoded in multiple places.

### Correction

Keep the existing explicit mirror behavior, but centralize the mathematical operation in a small runtime utility and make higher-level systems depend on it. Do not build a generic transformation graph.

The next central consumer should be `CC-076` so mirrored semantic bones are resolved using the same identity convention as mirrored geometry.

---

# 7. Exception handling / brittle pathways

## P2-09 — Broad `DomainException` catches inside validation can hide newly introduced resolver defects

`DefinitionValidator.ValidateResolvedEnvelope` intentionally catches `DomainException` around resolution and skips the affected check because structural issues are expected to have been reported elsewhere.

That policy is defensible, but a broad catch has a cost: if a resolver gains a new domain error unrelated to already-reported structural invalidity, the validator will silently suppress it.

### Correction

Make the failure classification explicit. Options, in order of preference:

1. validate prerequisites before resolving and only catch the known invalid-shape conditions;
2. introduce a small `TryResolve` API for validation-only paths;
3. if retaining exceptions, document and test the exact expected failure classes.

The current "catch anything DomainException and continue" pattern should not expand.

---

## P2-10 — Invalid-input behavior is inconsistent across modules

Some components:

- throw `DomainException`;
- return empty/default outputs;
- silently skip invalid geometry;
- rely on the caller having validated first.

That is workable during greenfield construction, but the contract needs to be explicit per layer.

### Recommended policy

```text
DefinitionValidator
    total/report-only; never throws for data invalidity

Canonicalizer
    throws DomainException for invalid structural input; never repairs

Resolved morphology
    strict direct API; throws for impossible source state

Generation compiler
    strict; assumes validated/canonical input

Validation-only wrappers
    TryResolve / diagnostic path; no broad exception swallowing
```

This would make error handling predictable without introducing a large result-type hierarchy.

---

# 8. Task board corrections and extensions

## Tasks that should remain / be elevated

| Task | Current state | Audit action |
|---|---|---|
| **CC-056B** | In Progress | **P1 critical path.** Finish semantic attachment frame contract and migrate child-at-tip placement. |
| **CC-076** | Backlog | **Keep P1.** Make it the only part-to-bone mapping seam after 056B. |
| **CC-082** | Backlog | **Elevate to P1 until validator is total.** Fix root cause in validation/indexing, not a local catch. |
| **CC-083** | Backlog | **P1/P2.** Correct validation semantics/code, not just expected test output. |
| **CC-084** | Backlog | **Keep P2.** Serialization correctness should be cleared before the end-to-end gate. |
| **CC-081** | Backlog | **Elevate to P1 gate.** Make it the canonical verification run for morphology foundation. |
| **CC-079** | Backlog | **Keep P2.** Needed to make body geometry/frame assumptions robust. |
| **CC-080** | Backlog | **P3 cleanup.** Keep separate from correctness work. |
| **CC-045** | In Progress | Finish only after the end-to-end evidence path exists; keep managed SDF as test/reference only. |
| **CC-052** | In Progress | Continue, but require CC-076 before semantic runtime binding becomes authoritative. |
| **CC-073** | Backlog | Keep blocked behind 056B/076. Do not invent a separate binding map. |
| **CC-069** | In Progress | Finish adapter-level work, but do not let `CreatureRig` become the semantic pose model. |

## Tasks that need correction

### CC-039 — stale/superseded

The authored limb blend value is already represented by `LimbChain.BlendRadius` via CC-049. Rewrite the ticket around internal metaball smoothing policy or close it as superseded.

### CC-055 — expand slightly

Keep the centerline decision small, but require that whichever geometry policy is chosen becomes a property of `ResolvedBody` / `ResolvedLimb`, not a new independent derivation in samplers or generators.

### CC-050 — tighten acceptance wording

The current implementation checks origin/joint/attachment positions against the bounds. That is not full generated-volume containment. The task should explicitly distinguish:

```text
resolved origin envelope validation
vs.
full field/mesh clipping containment
```

The latter should remain a separate future concern unless the product actually requires clipping guarantees.

### CC-056A — correct the contract text

Either add frame data to `ResolvedBody` or change the ticket to say that frame derivation is owned by `BodyFrameResolver` consuming `ResolvedBody`. The current wording mixes the two models.

### CC-004 / CC-006 / CC-015 / CC-016 / CC-017 — mutation-path fragmentation risk

These tasks split authoring behavior across multiple feature tickets. They should all converge on one mutation service/command path. The editor may have many tools, but DNA writes should not have many subtly different normalization/validation rules.

---

# 9. Recommended new tasks

## CC-085 — Migrate appearance sampling to resolved morphology arc length

Use `ResolvedBody.NormalizedArcLengthAtSample` / resolved centerline semantics as the shared longitudinal domain for body appearance sampling.

## CC-086 — Canonical resolved child-at-tip frame

Migrate `CreaturePartWorldTransformResolver` to `ResolvedLimb.TerminalSocket` and make child placement, editor placement, bounds validation, and generation prove parity.

## CC-087 — Validator totality and tolerant part indexing

Create one validation-safe part index that preserves duplicate/null state and remove all dictionary-based throw paths from validation.

## CC-088 — Shared polyline metrics helper

Consolidate segment-length and normalized-arc-length calculation used by `ResolvedBody` and `ResolvedLimb` without introducing a generic morphology framework.

## CC-089 — Explicit internal metaball blend policy

Replace the duplicate hardcoded `0.5f` policy constants with one named, tested policy. Keep the author-facing `LimbChain.BlendRadius` semantics separate.

---

# 10. Recommended sequencing

The repository should resist continuing to add animation/locomotion/binding features until the following sequence is complete.

### Phase A — correctness floor

1. CC-082
2. CC-083
3. CC-084
4. CC-079
5. validator totality regression suite

### Phase B — finish resolved morphology

1. CC-056B
2. CC-086
3. CC-088
4. migrate all attachment/placement consumers

### Phase C — semantic skeleton seam

1. CC-076
2. remove nearest-sample attachment from `SkeletonInferrer`
3. prove original/mirrored/list-order deterministic binding

### Phase D — verification gate

1. CC-081 canonical end-to-end run
2. definition validation
3. resolved morphology
4. SDF generation
5. mesh extraction
6. skeleton inference
7. rig adapter
8. serialization round-trip

### Phase E — only then

Continue CC-073, generalized animation queries, locomotion, and geometry binding.

---

# 11. Architecture target after cleanup

The clean architecture should converge toward:

```text
Authoritative DNA
    |
    +--> Validator --------------------------+
    |                                        |
    +--> Canonicalizer                       |
                                             v
                                      Resolved Creature Morphology
                                      +-----------------------------+
                                      | Body geometry / frames      |
                                      | Limb geometry / thickness   |
                                      | Part frames                 |
                                      | Semantic attachments       |
                                      | Mirror semantics            |
                                      +-------------+---------------+
                                                    |
                 +----------------+-----------------+-----------------+
                 |                |                 |                 |
                 v                v                 v                 v
               SDF             Skeleton         Mesh binding       Editor
                 |                |                 |                 |
                 +----------------+-----------------+-----------------+
                                  |
                                  v
                              Runtime adapters
                              (Unity mesh/rig/etc.)
```

The critical property is not the names. It is that **geometry and semantic attachment facts are derived once and consumed many times**.

---

# 12. What I would explicitly avoid

Do not introduce:

- a generic "component resolver" framework;
- a universal morphology object graph with dozens of interfaces;
- mutable cached transforms hanging off `CreatureDefinition`;
- mesh-derived semantic attachment state;
- animation-specific bone selection logic outside `CC-076`;
- more fallback behavior in the portable SDF compiler;
- additional feature tickets that re-implement existing resolved-morphology concepts.

The repository is healthiest when it uses a few explicit value-oriented contracts rather than abstracting every current variation.

---

# 13. Verification status and audit limitations

This audit was performed against the GitHub repository at commit `1e1a57569a4e66897d04bcb7d45ecce43cc24b09`, including source inspection, code search, task/ADR review, recent commit history, and comparison against the preceding audit.

The repository's own documented Unity evidence shows the current resolved-body/limb focused suites passing, while the full PlayMode suite still has the five tracked pre-existing failures associated with CC-082/083/084. Those claims are repository evidence; this audit environment did not have a local Unity editor/runtime available to independently execute the complete Unity suite.

No recommendation above assumes a test passed merely because the task says so. Where runtime evidence comes only from task/handoff documentation, it is identified as repository-reported evidence rather than independently reproduced evidence.

---

# 14. Final assessment

**Overall:** healthy and improving, but at a critical architectural transition point.

The project has successfully escaped several legacy traps already. The next legacy system to remove is not the managed SDF itself; it is the deeper pattern of **each subsystem interpreting creature DNA independently**.

The decisive next move is to finish `CC-056B`, migrate child-at-tip and attachment semantics onto resolved data, repair validator totality, then lock the whole path with `CC-081`.

Once those seams are closed, the later animation/locomotion/geometry-binding work can be built on stable semantics instead of layering more heuristics onto transitional representations.

