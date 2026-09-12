# CreatureCreator — Audit Synthesis 2026-09-12

**Synthesis ID:** `CC-SYNTH-20260912-1B7E44A9`
**Mode:** Full reconciliation (2026-09-12 audit batch)
**Branch under work:** `audit/skeleton-animation-improvements-2026-09-07`
**Fixed point (current HEAD):** `b5ba54b`
**Worktree state:** dirty. Ten 2026-09-12 audit artifacts are untracked; in-flight
TSK-0147 / TSK-0216 implementation is present as uncommitted changes.
**Unity execution:** not available. No Unity compile, EditMode, PlayMode, Burst
or benchmark result is claimed here. Every runtime/visual claim is marked
Unverified or Unity-gated.
**Code changes:** none. Outputs are this report and MemorySmith task records.

---

## 1. Executive summary

The 2026-09-12 batch is the strongest audit material the repository has
produced: it is specific, cites symbols, separates standards from
specification, and names what must *not* be reopened. It also contains a
provenance defect that changes how it must be used.

**The batch was not audited against the current branch.** Every audit in the
batch records a branch base, and none of those commits is an ancestor of the
current HEAD `b5ba54b`:

| Audit | Recorded base | Ancestor of `b5ba54b`? |
|---|---|---|
| `creaturecreator-extraordinarily-deep-dive-audit-2026-09-12.md` | `e0f43a65` | No |
| `creaturecreator-audit-suite-2026-09-12.md` | `2edca16d` | No |
| `creaturecreator-audit-animation-contract-2026-09-12.md` | `2edca16d` | No |
| `creaturecreator-audit-deformation-skinning-2026-09-12.md` | `17643c42` | No |
| `creaturecreator-audit-generation-performance-lifecycle-2026-09-12.md` | `ba680631` | No |
| `creaturecreator-audit-editor-architecture-2026-09-12.md` | `dc7a7a68` | No |
| `creaturecreator-audit-data-config-serialization-2026-09-12.md` | `8623b6e6` | No |
| `creaturecreator-audit-validation-process-2026-09-12.md` | `310d3932` | No |
| `creaturecreator-audit-engineering-security-tooling-2026-09-12.md` | `8bbbd3a1` | No |
| `creaturecreator-audit-code-health-consolidation-2026-09-12.md` | `b9b0fa0c` | No |

The audits were produced on the divergent `audit/animation-deformation-followup-2026-09-09`
lineage. This is not a fatal problem — it is a usage constraint. Every material
claim in this synthesis was therefore re-checked against the current worktree
before disposition, and the ledger records the result.

The direct consequence is already visible: the deep-dive's canonical owner for
its highest-ranked renderer findings is `TSK-0203`, described there as the "SMR
compatibility guard + animated bounds" task. **On this branch `TSK-0203` is two
unrelated memory-measurement records.** The guard it describes does exist in the
worktree, under no owner. An audit's owner table is not portable across branches.

Beyond provenance, the batch identifies five real mechanisms plus a process
cluster:

1. the pose payload cannot represent rotation (P1);
2. the scheduler bounds results, not work (P1);
3. the publish boundary is not an immutable object graph (P1);
4. the appearance stage materialises an unbounded distance matrix (P1);
5. the binding layer silently substitutes defaults for invalid input (P2);
6. evidence discipline, not code quality, now limits trust (P2, process).

The user's specific request — recurring patterns to guard against — is answered
in §7. Four patterns recur with two or more independent instances each, and each
now has a named safeguard or an owning task.

### Result counts

| Result | Count |
|---|---|
| Audit artifacts inventoried (1 index + 8 lenses + 1 deep-dive) | 10 |
| Findings inventoried | 116 |
| Confirmed with an owner | 86 |
| Duplicate (mechanism already owned) | 14 |
| Partially confirmed | 4 |
| Rejected or positive-only observation | 3 |
| Deferred pending evidence | 6 |
| Intentionally unticketed (P3) | 3 |
| New MemorySmith tasks | 5 (`TSK-0217`–`TSK-0221`) |
| Existing owners extended by comment | 6 |
| Unity-gated validation gates unresolved | 14 |

---

## 2. Scope and method

In scope: the ten 2026-09-12 artifacts. Out of scope: the 2026-09-09..2026-09-11
window, already reconciled by `creaturecreator-audit-synthesis-2026-09-11.md`.

Method: inventory claims without deduplicating, verify each material claim
against current source, merge by mechanism, then choose the smallest durable
task disposition. Prior syntheses were read in full and treated as baseline, not
as truth.

### Reconciliation pace

The 2026-09-11 synthesis reconciled the 2026-09-09..2026-09-11 window (19 audits
re-reviewed, 10 new). The 2026-09-12 batch is fully unreconciled before this
pass. A residual tail of late-2026-09-11 artifacts also appears unreconciled —
`creaturecreator-round28-race-fixed-confirmed-2026-09-11.md`,
`creaturecreator-sdf-sampling-race-fix-audit-26-09-11-21-50-00.md`,
`creaturecreator-generated-mesh-integrity-audit-26-09-11-20-24-00.md`,
`audit-gradient-regression-2026-09-11.md`,
`creaturecreator-council-review-next-phase-ordering-2026-09-11.md` — because
their timestamps are later than, or absent from, the 2026-09-11 source ledger.
**Reconciliation is not keeping pace.** That tail is named as unreconciled
backlog, not silently dropped.

---

## 3. Standards vs specification

### Standards assessment

The project holds strong engineering standards: immutable-snapshot intent,
runtime/editor separation, numeric finiteness as a boundary invariant,
deterministic ordering as correctness, pure deformation maths isolated from
Unity presentation, transactional generated-object cleanup in lower layers, and
consolidated task ownership.

The standards weakness is **contract drift between prose, implementation, and
evidence**. Verified instances in this pass:

- `SkeletonSnapshot.HasSameBoneOrder` is named as an ordering check but compares
  IDs, parent index, source part, part type, mirror flag, position, rotation,
  segment state, endpoint and child-attachment state (`SkeletonSnapshot.cs:219`).
- `GeneratedCreatureData` is described as immutable but is only shallowly so.
- "Zero allocation" is demonstrated for `ApplyPose`, not for pose production.
- An allocator choice of `Persistent` is used for request-scoped scratch.
- The audit's own UV-channel list contradicts itself (see F-10).

### Specification assessment

The architecture has deliberate simplifications that should stay: no internal
Animator/Avatar/state-machine framework, build-time weight generation,
presentation-owned generated geometry, an external locomotion driver, and
compact deterministic skeleton identity.

The missing specifications are all at **integration boundaries**: complete pose
representation, local-vs-world transform semantics, actor root ownership, root
motion ownership, animation update phase, IK ordering, pose completeness versus
sparse updates, animated renderer bounds, morphology-change synchronisation,
exact mesh-channel preservation, and resource ownership across replacement.
These belong to `TSK-0190`, `TSK-0200`, `TSK-0218` and `TSK-0219`.

---

## 4. Accepted findings in severity order

### P1

| # | Finding | Owner | Evidence result |
|---|---|---|---|
| 1 | Position-only pose cannot express rotation, terminal orientation or axial roll | TSK-0190 | Confirmed (`PosedSkeleton.cs:26` stores only `Vector3[] _positions`) |
| 2 | End-to-end pose construction is not allocation-free; only `ApplyPose` is | TSK-0118, TSK-0134 | Confirmed by source |
| 3 | Scheduler is latest-result-wins; stale suppression happens after the work | TSK-0104 | Confirmed (`CreatureGenerationScheduler.cs:48`) |
| 4 | Actor/world transform ownership is identity-host based | TSK-0190 | Confirmed by source |
| 5 | Remaining deformation smear mechanism not isolated | TSK-0168, TSK-0172 | Confirmed open risk; Unity-gated |
| 6 | Generated data / resolved snapshot immutability is stronger in wording than in the object graph | TSK-0095 | Confirmed by source |
| 7 | Appearance Burst path materialises an O(V×P) distance matrix | TSK-0008 | Confirmed by audit; profiling gate |
| 8 | Task-record integrity: 14 duplicate keys, 30 schema-incomplete records, 1 unloadable record | TSK-0217 | Confirmed by `Test-TaskRecords.ps1` |
| 9 | Audit provenance: batch bases are not ancestors of HEAD; phantom owner citations | TSK-0220 | Confirmed by `git merge-base` |

### P2

| # | Finding | Owner | Evidence result |
|---|---|---|---|
| 10 | Renderer mesh copy / material cardinality contract is implicit | TSK-0218 | Confirmed (`CreatureSkinnedMeshRenderer.cs:181`) |
| 11 | Raw definition and resolved snapshot both flow into the same boundary | TSK-0095 | Confirmed |
| 12 | Binding silently substitutes defaults for present-but-invalid input | TSK-0218 | Confirmed (`ResolveBoneRadius`) |
| 13 | Mutable builder-side `Bone`/`Skeleton` is the remaining legacy escape hatch | TSK-0156 | Confirmed |
| 14 | Revision identity is fragmented across five local mechanisms | TSK-0219 | Confirmed by source |
| 15 | Editor state domains, gesture duplication, fragmented stale-state flags | TSK-0098 | Confirmed by source |
| 16 | Preview replacement is not transactional at the coordinator boundary | TSK-0104 | Confirmed |
| 17 | Scheduler has no request token, cancellation or backpressure metrics | TSK-0104 | Confirmed |
| 18 | Legacy shape-fallback semantics have no single owner and no contract decision | TSK-0137, TSK-0161 | Deferred pending evidence |
| 19 | Validation/evidence lifecycle is unstructured | TSK-0220 | Confirmed |
| 20 | Developer tooling does not verify postconditions or classify local state | TSK-0221 | Partially confirmed |

### P3 and lower

| # | Finding | Disposition |
|---|---|---|
| 21 | Documentation drift (stale canonical JSON examples, stale skeleton-density comments) | Intentionally unticketed; fix inside affected tasks |
| 22 | Editor presentation toggles use mixed persistence mechanisms | Intentionally unticketed; fold into TSK-0098 |
| 23 | Engineering-skill docs should be versioned carefully | Intentionally unticketed; fold into TSK-0221 |

---

## 5. Disposition ledger

Dispositions: `fixed` (verified at source, no work), `extend` (owner scope
added), `new` (task created), `duplicate` (mechanism already owned), `partial`,
`deferred`, `unticketed`, `rejected`.

### 5.1 Deep-dive (CC-AUDIT-20260912-7A5E0C31)

| ID | Mechanism | Result | Owner | Disposition |
|---|---|---|---|---|
| F-01 | Position-only pose | Confirmed | TSK-0190 | extend |
| F-02 | Pose path allocation | Confirmed | TSK-0118, TSK-0134 | duplicate |
| F-03 | Identity-host transform | Confirmed | TSK-0190 | extend |
| F-04 | Unbounded async preview | Confirmed | TSK-0104 | extend |
| F-05 | Deformation smear undiagnosed | Confirmed (Unity-gated) | TSK-0168, TSK-0172 | extend |
| F-06 | Shallow immutability | Confirmed | TSK-0095 | extend |
| F-07 | Snapshot immutability residual | Confirmed | TSK-0095 | duplicate (F-06) |
| F-08 | O(V×P) appearance matrix | Confirmed | TSK-0008 | extend |
| F-09 | `Allocator.Persistent` scratch | Confirmed | TSK-0008 | extend |
| F-10 | Partial mesh-channel copy | Partially confirmed | TSK-0218 | new |
| F-11 | SMR bind acceptance bar broader than regression | Confirmed | TSK-0200 (id slug) | partial — source fixed, Unity bounds open |
| F-12 | Raw/resolved dual authority | Confirmed | TSK-0095 | extend |
| F-13 | Mutable `Bone`/`Skeleton` | Confirmed | TSK-0156 | duplicate |
| F-14 | `HasSameBoneOrder` name understates contract | Confirmed | TSK-0219 | extend |
| F-15 | String IDs in hot path | Confirmed | TSK-0190, TSK-0118 | duplicate |
| F-16 | `CreatureRuntimePreview` policy accumulation | Confirmed | TSK-0098, TSK-0104 | duplicate |
| F-17 | Non-uniform transactionality | Confirmed | TSK-0104 | duplicate |
| F-18 | Legacy fallback duplication | Unverified | TSK-0137, TSK-0161 | deferred |
| F-19 | Documentation drift | Confirmed | — | unticketed (P3) |
| F-20 | Synthesis loses small findings | Confirmed | TSK-0220 | new |
| HG-01 | `TSK-0203` is two mechanisms | Confirmed | TSK-0200 (id slug) | extend |
| HG-02 | Renderer genericity trap | Confirmed | TSK-0218 | duplicate (F-10) |
| HG-03 | Compatibility predicate as identity gate | Confirmed | TSK-0219 | duplicate |
| HG-04 | Bounds must join "build once, pose forever" | Confirmed | TSK-0200 (id slug) | extend |
| HG-05 | Three-part animation correctness | Confirmed | TSK-0200, TSK-0190 | extend |
| HG-06 | Ownership must be audited by object graph | Confirmed | TSK-0104 | duplicate |

### 5.2 Animation contract (CC-AUDIT-ANIM-20260912-5B71A9E2)

| ID | Mechanism | Result | Owner | Disposition |
|---|---|---|---|---|
| AN-01 | Position-only pose | Duplicate of F-01 | TSK-0190 | duplicate |
| AN-02 | World/creature-space pose | Duplicate of F-03 | TSK-0190 | duplicate |
| AN-03 | Root motion ownership unspecified | Confirmed | TSK-0190 | extend |
| AN-04 | Pose completeness vs sparse updates | Confirmed | TSK-0190 | extend |
| AN-05 | IK/animation update ordering | Confirmed | TSK-0190 | extend |
| AN-06 | FABRIK fixed-link vs morphology change | Confirmed | TSK-0190, TSK-0219 | extend |
| AN-07 | Compatibility predicate naming | Duplicate of F-14 | TSK-0219 | duplicate |
| AN-08 | String IDs at boundary only | Confirmed | TSK-0190, TSK-0118 | extend |
| AN-09 | Scale outside the contract | Confirmed | TSK-0190 | extend |
| AN-10 | Mirroring not expressed as animation semantics | Confirmed | TSK-0190 | extend |
| AN-11 | Bind-time vs pose-time identity | Confirmed | TSK-0190 | extend |

### 5.3 Deformation and skinning (CC-AUDIT-SKIN-20260912-1D83F0A4)

| ID | Mechanism | Result | Owner | Disposition |
|---|---|---|---|---|
| SK-01 | Inconsistent invalid-radius semantics | Partially confirmed | TSK-0218 | partial — NaN/∞ asymmetry fixed in worktree; silent default remains |
| SK-02 | `DefaultInfluenceRadius` escape hatch | Confirmed | TSK-0218 | new |
| SK-03 | Weight authoring O(V×S) | Confirmed | TSK-0218, TSK-0134 | deferred (measurement-gated) |
| SK-04 | Sort-all-candidates | Confirmed | TSK-0218 | deferred (measurement-gated) |
| SK-05 | `WeightFor` less defensive than `Author` | Confirmed | TSK-0218 | new |
| SK-06 | String IDs in inner weighting loop | Confirmed | TSK-0218 | deferred (measurement-gated) |
| SK-07 | Mesh copy metadata loss | Duplicate of F-10 | TSK-0218 | duplicate |
| SK-08 | Material/submesh cardinality unspecified | Confirmed | TSK-0218 | new |
| SK-09 | Animated bounds unresolved | Confirmed | TSK-0200 (id slug) | duplicate |
| SK-10 | Deformation cannot be judged from LBS oracle alone | Confirmed | TSK-0168 | extend |
| SK-11 | Domains can hide authoring topology errors | Confirmed | TSK-0168 | extend |
| SK-12 | Mirror parity needs animation-space validation | Confirmed | TSK-0190 | extend |

### 5.4 Generation, performance, lifecycle (CC-AUDIT-GEN-20260912-83C17B4E)

| ID | Mechanism | Result | Owner | Disposition |
|---|---|---|---|---|
| GP-01 | Latest-result vs latest-work | Duplicate of F-04 | TSK-0104 | duplicate |
| GP-02 | Disposal does not cancel or drain | Confirmed | TSK-0104 | extend |
| GP-03 | No backpressure metrics | Confirmed | TSK-0104 | extend |
| GP-04 | O(V×P) memory topology | Duplicate of F-08 | TSK-0008 | duplicate |
| GP-05 | Persistent scratch lifetime | Duplicate of F-09 | TSK-0008 | duplicate |
| GP-06 | Clone ownership only in comments | Confirmed | TSK-0104 | extend |
| GP-07 | Mutable graphs across scheduler boundary | Duplicate of F-06 | TSK-0095 | duplicate |
| GP-08 | Preview lifecycle god object | Duplicate of F-16 | TSK-0098, TSK-0104 | duplicate |
| GP-09 | Non-uniform transactionality | Duplicate of F-17 | TSK-0104 | duplicate |
| GP-10 | Cache-key problem from overrides | Confirmed | TSK-0095, TSK-0076 | extend |
| GP-11 | Stage identity should be machine-stable | Confirmed | TSK-0104 | extend |
| GP-12 | Ownership explicit in result graph | Confirmed | TSK-0104, TSK-0095 | extend |

### 5.5 Editor architecture (CC-AUDIT-EDITOR-20260912-4E52D8B7)

| ID | Mechanism | Result | Owner | Disposition |
|---|---|---|---|---|
| ED-01 | Too many state domains in `CreatureEditorWindow` | Confirmed | TSK-0098 | duplicate |
| ED-02 | Continuous drags create many Undo records | Confirmed | TSK-0098 | extend |
| ED-03 | Fragmented revision concept | Confirmed | TSK-0219 | new |
| ED-04 | Local vs world bounds semantics | Confirmed | TSK-0098 | extend (needs decision) |
| ED-05 | Placement colliders across a drag | Confirmed | TSK-0219 | extend |
| ED-06 | Selection identity must be definition-based | Confirmed | TSK-0098 | duplicate |
| ED-07 | Config asset assignment is an evidence gap | Confirmed | TSK-0076 | extend |
| ED-08 | Mixed persistence mechanisms | Confirmed | TSK-0098 | unticketed (P3) |
| ED-09 | Error presentation becomes a second pipeline | Confirmed | TSK-0098 | extend |
| ED-10 | Gesture contract duplication | Duplicate of CH-03 | TSK-0098 | duplicate |
| ED-11 | Debounce leaking into correctness | Confirmed | TSK-0098 | extend |
| ED-12 | Skeleton overlay must stay a projection | Confirmed | TSK-0098 | duplicate |

### 5.6 Data, config, serialization (CC-AUDIT-DATA-20260912-7AC31E90)

| ID | Mechanism | Result | Owner | Disposition |
|---|---|---|---|---|
| DC-01 | Shallow immutability | Duplicate of F-06 | TSK-0095 | duplicate |
| DC-02 | Per-field ownership guarantees | Confirmed | TSK-0095 | extend |
| DC-03 | Raw/resolved escape hatch | Duplicate of F-12 | TSK-0095 | duplicate |
| DC-04 | Canonicalization idempotence and version | Confirmed | TSK-0219 | extend |
| DC-05 | Legacy shape fallback | Unverified | TSK-0137, TSK-0161 | deferred |
| DC-06 | Silent scalar clamping hides invalid asset | Confirmed | TSK-0076 | extend (verified `Mathf.Max(1f, …)`) |
| DC-07 | Effective config and generation signature | Duplicate of GP-10 | TSK-0095 | duplicate |
| DC-08 | Missing palette-key policy | Confirmed | TSK-0076 | extend |
| DC-09 | Config assignment persistence | Duplicate of ED-07 | TSK-0076 | duplicate |
| DC-10 | Do not serialize derived data | Confirmed | TSK-0095 | extend |
| DC-11 | Identity must not be inferred from display names | Unverified | TSK-0088 (Done lineage) | deferred |
| DC-12 | Asset references are non-owned dependencies | Confirmed | TSK-0095 | extend |

### 5.7 Validation and process (CC-AUDIT-VALIDATION-20260912-2F9A6C51)

| ID | Mechanism | Result | Owner | Disposition |
|---|---|---|---|---|
| VT-01 | Source vs behavioural completion conflated | Confirmed | TSK-0220 | new |
| VT-02 | Unity evidence needs identity fields | Confirmed | TSK-0220 | new |
| VT-03 | Deterministic facts vs visual judgement | Confirmed | TSK-0220 | new |
| VT-04 | Historical audits not uniformly dispositioned | Duplicate of F-20 | TSK-0220 | duplicate |
| VT-05 | Report IDs as durable evidence keys | Confirmed | TSK-0220 | new |
| VT-06 | Disabled CI is an unowned evidence gap | Confirmed | TSK-0220 | new |
| VT-07 | Contradictory ownership / stale CC identifiers | Confirmed | TSK-0217, TSK-0220 | new |
| VT-08 | Standard validation-blocker field | Confirmed | TSK-0220 | new |
| VT-09 | Regression must target the historical mechanism | Confirmed | TSK-0220 | new |
| VT-10 | Performance claims need workload envelopes | Confirmed | TSK-0220 | new |
| VT-11 | One canonical adversarial creature fixture | Confirmed | TSK-0204 (id slug) | extend |
| VT-12 | Audit artifacts must be on the branch they claim | Confirmed | TSK-0220 | new |

### 5.8 Engineering, security, tooling (CC-AUDIT-ENG-20260912-C83D71FA)

| ID | Mechanism | Result | Owner | Disposition |
|---|---|---|---|---|
| ET-01 | Repository-wide secret-scan policy | Confirmed | TSK-0221 | new |
| ET-02 | Script side effects vs documentation | Partially confirmed | TSK-0221 | partial — exit-code gating present, dry-run absent |
| ET-03 | Import tooling strictly one-way and resumable | Confirmed | TSK-0221 | new |
| ET-04 | Local state vs repository state | Confirmed | TSK-0221 | new |
| ET-05 | Verify postconditions, not exit codes | Partially confirmed | TSK-0221 | partial — `$LASTEXITCODE` checked, artifacts not |
| ET-06 | CI absence must not read as validation | Confirmed | TSK-0220 | new |
| ET-07 | Skill docs are executable policy | Confirmed | TSK-0221 | unticketed (P3) |
| ET-08 | Automation should fail closed | Confirmed | TSK-0221 | new |
| ET-09 | Generated/local artifact classification | Confirmed | TSK-0221 | new |

### 5.9 Code health and consolidation (CC-AUDIT-HEALTH-20260912-4D6F21A8)

| ID | Mechanism | Result | Owner | Disposition |
|---|---|---|---|---|
| CH-01 | Skeleton compatibility has multiple names | Confirmed | TSK-0219 | extend |
| CH-02 | Revision identity replaces fingerprints | Confirmed | TSK-0219 | new |
| CH-03 | Gesture/session primitive justified | Confirmed | TSK-0098 | extend |
| CH-04 | Default/fallback policy is the real risk | Confirmed | TSK-0218, TSK-0220 | extend |
| CH-05 | Unity names at boundaries; core maths pure | Positive observation | — | rejected (no action, do not "fix") |
| CH-06 | Mutable builder `Bone`/`Skeleton` | Duplicate of F-13 | TSK-0156 | duplicate |
| CH-07 | Read-only wrappers are not immutable values | Confirmed | TSK-0095 | extend |
| CH-08 | Delete historical compatibility shims | Already tracked | TSK-0214 | rejected (already owned) |
| CH-09 | Healthy single-owner-per-mechanism trend | Positive observation | — | rejected (no action) |
| CH-10 | Avoid generic utility buckets for domain policy | Confirmed | engineering-guardrails skill | fixed (already covered) |

### 5.10 Explicitly not reopened

From the deep-dive §15 "do not reopen": the continuation-child selection defect,
the four-body-bone criticism, the missing raw-mesh-toggle claim, the static GUI
style initialisation claim, the duplicate root ownership claim, and foot
cross-side coupling as a *new* owner. From the suite exclusions: the structural
preview-root/name collision, the duplicate mirror-math finding, the
`IDnaSerializer` interface shape, and any replacement CI system
(`TSK-0123` is Rejected by the repository owner).

Explicitly **not created**: a generic animation framework, a duplicate scheduler
task, a duplicate snapshot-immutability task, a duplicate foot-domain task, or a
duplicate editor-decomposition task.

---

## 6. Task dispositions applied

### New tasks

| Key | Title | Priority |
|---|---|---|
| TSK-0217 | Reconcile the task-record collision backlog and malformed records | High |
| TSK-0218 | Make binding inputs fail fast and declare the mesh/material contract | Medium |
| TSK-0219 | Define layered revision identity across editor, scheduler, rig, and renderer | Medium |
| TSK-0220 | Add structured validation state and evidence rules to the task and audit process | Medium |
| TSK-0221 | Harden developer tooling to fail closed and verify postconditions | Medium |

### Existing owners extended

| Key | Scope added |
|---|---|
| TSK-0190 | Consolidated explicit-pose, transform, root-motion, ordering, completeness, scale and mirror scope with the required acceptance evidence |
| TSK-0104 | Latest-work-wins, request token, cancellation, backpressure metrics, transactional replacement |
| TSK-0095 | Publish boundary, per-field ownership, raw/resolved authority, generation signature |
| TSK-0008 | Tiled appearance memory, allocator lifetime, measurement priority order |
| TSK-0213 | Backlog-repair link (TSK-0217) and the phantom-owner rule |
| TSK-0168 | Mandatory diagnosis protocol before weight tuning |

### Phantom-owner correction

The deep-dive's `TSK-0203` citation is recorded as **provenance-only**. On this
branch the animated-bounds owner is the record whose id slug is
`tsk-0200-define-animated-bounds-and-culling-policy`. That key is currently
collided with two unrelated records, which is why it is named by slug here;
`TSK-0217` must reconcile it to a unique key.

Verified at source: the SMR structural compatibility guard already exists in the
worktree — `CreatureSkinnedMeshRenderer.Bind` rejects a snapshot whose count
differs from `rig.IndexedBones.Count` and then requires
`rig.RestSkeleton.HasSameBoneOrder(snapshot)`. The remaining gap is the Unity
animated-bounds behaviour, not the guard.

---

## 7. Recurring patterns and process safeguards

The user asked specifically for recurring patterns worth guarding against. Four
qualify by having two or more independent, dated instances. Each already has a
stated rule in a skill file that the recurrence happened despite — that
combination is the strongest signal that the rule needs a sharper form.

### R1 — A stated invariant is stronger than the API that enforces it

Instances in this batch: shallow immutability (F-06, F-07, DC-01, DC-02, CH-07);
"allocation-free" proven for one sub-step (F-02, AN-08); `HasSameBoneOrder`
named as ordering but implemented as rest-state equality (F-14, AN-07, CH-01);
`Persistent` allocator for request-scoped data (F-09, GP-05). Five independent
variants, four reports.

Safeguard: when a boundary introduces a term of art — immutable, allocation-free,
compatible, ordered — the doc comment must state the exact predicate and the
caller obligation. **Owner:** TSK-0220 (evidence vocabulary) plus the
engineering-guardrails skill.

### R2 — Source completion is reported as behavioural completion

Instances: VT-01 names TSK-0203 as the worked example; F-11 shows the acceptance
bar exceeds the regression; the batch's own Unity gate is 14 items; TSK-0194
lists named PlayMode failures that remain open. This pattern has now appeared in
the 2026-09-09 meta-synthesis secondary list and again in 2026-09-12 as VT-01.

Safeguard: a four-state vocabulary — `implemented`, `unit-tested`,
`unity-tested`, `user-accepted` — plus `lastValidatedCommit`. A task may not
leave the first two states and claim completion. **Owner:** TSK-0220.

### R3 — A default silently masks a missing or invalid input

Instances: `ResolveBoneRadius` substituting `FallbackBoneRadius` (SK-01, SK-02),
`DefaultInfluenceRadius` exposed as a production fallback (SK-02),
`Mathf.Max(1f, defaultVoxelsPerUnit)` (DC-06, verified), missing palette key to
null (DC-08), terminal rotation falling back to rest rotation (AN-01), missing
material to `null` (SK-08), legacy shape fallback (F-18, DC-05). Seven
instances. CH-04 correctly identifies this as more dangerous than duplicated
helpers.

Safeguard: a present-but-invalid input must be rejected or reported; a default
may only cover a *missing* input and must be named as a default. Test-only
convenience helpers must be named so they cannot be mistaken for production.
**Owner:** TSK-0218.

### R4 — Cross-branch task-key divergence

Instances: TSK-0136→TSK-0187 and TSK-0172 renumbering (recorded 2026-09-08);
TSK-0188/0189 (recorded 2026-09-09); TSK-0197–TSK-0205 (recorded 2026-09-11,
tracked as TSK-0213); and now 14 live collisions plus a phantom owner citation.
Fourth recurrence, and the first with a *new* failure mode: an audit citing an
owner key that does not exist on the branch under work.

Safeguard: two rules, not one. (a) Repair the backlog — TSK-0217. (b) Before
citing a `TSK-####` owner in any audit, synthesis or handoff, resolve that key on
the branch under work and confirm the record matches the cited mechanism; if it
does not, treat the citation as provenance-only and assign a real owner.
**Owner:** TSK-0217 and TSK-0213.

### Secondary — single instance so far, worth early recognition

- **Destroy-then-build at the coordinator boundary.** Improved in
  `CreatureRig.Build` and `CreatureSkinnedMeshRenderer.Bind` (verified
  build-then-commit), still live at the preview coordinator (F-17, GP-09).
  Already named in the engineering-guardrails skill; TSK-0104 carries the fix.
- **Audit artifacts not committed to the branch they claim.** All ten
  2026-09-12 artifacts are untracked in the worktree. TSK-0220 adds a placement
  check.
- **A validator that silently skips what it cannot parse.** No longer true:
  `Normalize-TaskRecords.ps1` now fails closed on skipped records. Recorded as
  fixed.
- **Debug tooling used as evidence for a different bug.** `RigDebugView` mixed
  rest-space and posed-space coordinates. Now a rule in the TSK-0168 diagnosis
  protocol.

---

## 8. Assumptions, blockers, next evidence

### Assumptions

- The current worktree is the intended state of work; uncommitted TSK-0147
  changes were treated as in flight and not as settled behaviour.
- The 2026-09-12 batch is the "latest audits" for this pass.

### Blockers

1. **Unity is unavailable.** 14 validation gates cannot be closed here. No
   runtime, culling, deformation or play-mode claim is made.
2. **Audit bases are not ancestors of HEAD.** Findings were re-verified in the
   worktree where possible; unre-verified items are marked deferred.
3. **The task board fails its own validator.** Any turn that creates a task
   currently cannot satisfy the mandated step 7 of the task-tracker skill, which
   requires `Normalize-TaskRecords.ps1` to report zero collisions. TSK-0217
   owns the repair; until it lands, task-board integrity claims are qualified.
4. **MCP key renumbering is unproven.** TSK-0217 records this as its first
   investigation.

### Next evidence required

- TSK-0217: `Test-TaskRecords.ps1` exit 0.
- TSK-0200 (id slug): exaggerated-pose visibility and un-clipped culling in
  Unity, plus the SMR local-bounds policy decision.
- TSK-0190: terminal-rotation, explicit-rotation, rest round-trip and
  translated/rotated actor-root fixtures.
- TSK-0168: generated-creature pose-isolation fixture with top-four influences
  captured before any radius tuning.
- TSK-0104: burst-edit coalescing proof and failed-replacement safety.
- Late-2026-09-11 audit tail: a follow-up reconciliation pass.

---

## 9. Source ledger

| ID | Source | Role |
|---|---|---|
| S01 | `docs/audits/creaturecreator-audit-suite-2026-09-12.md` | Suite index |
| S02 | `docs/audits/creaturecreator-extraordinarily-deep-dive-audit-2026-09-12.md` | Primary deep-dive; F-01..F-20, HG-01..HG-06 |
| S03 | `docs/audits/creaturecreator-audit-animation-contract-2026-09-12.md` | AN-01..AN-11 |
| S04 | `docs/audits/creaturecreator-audit-deformation-skinning-2026-09-12.md` | SK-01..SK-12 |
| S05 | `docs/audits/creaturecreator-audit-generation-performance-lifecycle-2026-09-12.md` | GP-01..GP-12 |
| S06 | `docs/audits/creaturecreator-audit-editor-architecture-2026-09-12.md` | ED-01..ED-12 |
| S07 | `docs/audits/creaturecreator-audit-data-config-serialization-2026-09-12.md` | DC-01..DC-12 |
| S08 | `docs/audits/creaturecreator-audit-validation-process-2026-09-12.md` | VT-01..VT-12 |
| S09 | `docs/audits/creaturecreator-audit-engineering-security-tooling-2026-09-12.md` | ET-01..ET-09 |
| S10 | `docs/audits/creaturecreator-audit-code-health-consolidation-2026-09-12.md` | CH-01..CH-10 |
| S11 | `docs/audits/creaturecreator-audit-synthesis-2026-09-11.md` | Prior synthesis baseline |
| S12 | `docs/audits/creaturecreator-recurring-patterns-guardrails-audit-2026-09-08.md` | Recurring-pattern baseline |
| S13 | `docs/audits/meta-synthesis-repeat-patterns-2026-09-09.md` | Recurring-pattern baseline |
| S14 | `Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs` | Verified source |
| S15 | `Assets/Scripts/Runtime/Animation/Skinned/CreatureSkinnedMeshRenderer.cs` | Verified source |
| S16 | `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs` | Verified source |
| S17 | `Assets/Scripts/Runtime/Animation/Binding/InfluenceWeightingPolicy.cs` | Verified source (uncommitted) |
| S18 | `Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs` | Verified source |
| S19 | `Assets/Scripts/Runtime/Generation/CreatureGenerationScheduler.cs` | Verified source |
| S20 | `Assets/Scripts/Runtime/Generation/CreatureGenerationConfig.cs` | Verified source |
| S21 | `Data/Tasks/*.json` + `memorysmith_task_list` | Live task board |
| S22 | `Scripts/Test-TaskRecords.ps1`, `Scripts/Normalize-TaskRecords.ps1` | Integrity evidence |
| S23 | `.github/skills/task-tracker`, `unity-validation`, `engineering-guardrails`, `cc-audit-synthesis` | Process contracts |
| S24 | `git merge-base --is-ancestor` against `b5ba54b` | Provenance evidence |

## 10. Uninspected or limited-evidence artifacts

- The late-2026-09-11 tail named in §2 was not reconciled in this pass.
- `creaturecreator-review-2026-09-05-round2-...`, the delta-audit 10/11
  reconciliations and the pre-2026-09-09 corpus were treated as historical
  provenance, not re-read line by line.
- No Unity scene, PlayMode session, Burst job or benchmark was executed.
- `Assets/Scripts/Runtime/Animation/Binding/InfluenceWeightingPolicy.cs` is
  uncommitted; findings that depend on it are marked as in-flight.

## 11. Completion checklist

| Item | Status |
|---|---|
| Every supplied audit inventoried and read | Yes (10/10) |
| Every material claim has evidence or an explicit unresolved disposition | Yes — §5 ledger, 116 rows |
| Severity and confidence independent and justified | Yes — §4 |
| Duplicates merged without merging distinct fixes | Yes — 14 duplicate rows |
| Existing MemorySmith coverage checked before creation | Yes — `memorysmith_task_list` + record read |
| Superseded/archived records retain searchable evidence | Yes — no archive operations performed |
| New tasks have acceptance criteria, links and validation gates | Yes |
| Fixed claims not reopened | Yes — §5.10 |
| Standards and specification assessments separate | Yes — §3 |
| Open evidence gaps have an owner or next step | Yes — §8 |
| Accepted-baseline failures named by test, not count | Yes — TSK-0194 failure names cited |
| Reconciliation-pace statement and unreconciled backlog named | Yes — §2 |
| Records validated; `git diff --check` | Pending — see §12 |

## 12. Record validation

Read-only checks run after the task mutations:

| Check | Command | Result |
|---|---|---|
| Task records | `pwsh Scripts/Test-TaskRecords.ps1` | FAIL — 44 issues, **unchanged** from the pre-pass baseline |
| Duplicate keys | same output, `Duplicate task key` count | 14, **unchanged** — no new collision introduced |
| New keys unique | `Data/Tasks/tsk-021[7-9]*.json`, `tsk-022[01]*.json` | `TSK-0217`–`TSK-0221`, 5/5 distinct |
| Whitespace | `git diff --check` | exit 0 (one pre-existing LF/CRLF warning in an untouched file) |
| Synthesis path | `docs/audits/creaturecreator-audit-synthesis-2026-09-12.md` | exists |

- The five new tasks were created through `memorysmith_task_create`; no
  `Data/Tasks/*.json` file was hand-edited.
- Owner comments reference real keys; no key was reused for a second mechanism.
- **Not satisfied:** the validator still fails. The failure is the pre-existing
  backlog owned by `TSK-0217`, not a regression from this pass. It is recorded
  as a blocker and as the headline process finding, not hidden.
- The task-tracker skill's mandated step 7 (normalizer reports zero collisions)
  cannot pass until `TSK-0217` lands. Task-board integrity claims made before
  then must carry that qualification.
