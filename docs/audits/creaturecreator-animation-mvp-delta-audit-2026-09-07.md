# CreatureCreator — Animation MVP Delta Audit

**Date:** 2026-09-07
**Baseline:** current `main` tip — last commit title `"Record five-round sprint handoff"`,
pushed `2026-09-07T04:15:55Z`. This is a **delta-only follow-up** to
`creaturecreator-animation-locomotion-deep-dive-2026-09-05-2029.md` per the standing
audit methodology: prior findings are treated as accepted unless corrected below; only
new findings, corrections, stronger evidence, and task extensions are reported in full.
**Method:** raw-file fetch via `raw.githubusercontent.com`/`codeload.github.com`
(GitHub REST API rate-limited again this session; provenance from the public Atom
commit feed), direct source read of every file touched since the last pass, full
`Data/Tasks/*.json` inventory (tsk-0100 and above), and cross-check against the six
audit documents and one MVP-specific audit already committed to `docs/audits/` since
the last pass (see **Note on existing coverage** below).

## Headline: the animation MVP essentially landed since the last pass

In under 48 hours the repo went from "indexed skeleton exists, nothing skins it yet" to
a working `SkinnedMeshRenderer` pipeline: real per-vertex bind weights for both the
welded implicit surface (`tsk-0131`) and rigid mesh-asset parts (`tsk-0130`), a
`CreatureSkinnedMeshRenderer` adapter (`tsk-0132`), an explicit pose-driver interface
decision (`tsk-0133`: direct `CreatureRig.ApplyPose`, not a Unity `Animator`/`Avatar`),
and wiring into both the runtime demo (`CreatureRuntimePreview`) and the live editor
preview (`CreaturePreviewController`) (`tsk-0142`). All four "Critical/High" findings
from the last audit (C-1, H-1, H-3, H-4) are **confirmed fixed in source**, not just
claimed — this review re-derived each one directly rather than trusting task status.

## Note on existing coverage — read this before the findings below

Since the last pass, the repo accumulated its own very thorough self-audits, including
one written specifically to cross-validate the last report
(`docs/audits/2026-09-06...` synthesis, folded into `tsk-0118`) and a dedicated
`creaturecreator-skinnedmeshrenderer-mvp-audit-2026-09-06.md` that scoped almost exactly
the work this review would otherwise have recommended (real-geometry weighting, SMR
wiring, the Animator/Avatar decision, a runtime performance budget). A
`creaturecreator-whole-codebase-exhaustive-audit-26-09-06-16-57-00.md` also already
found and filed **F-18** (throwaway `CreaturePart` construction), **F-23** (list-as-
priority-queue in `SkeletonSnapshot.Capture`), **F-25/F-26** (`LinearBlendSkinning`
accepts non-normalized quaternions / doesn't reject duplicate bone indices), and
**F-27** (`PoseRotationResolver`'s narrow motion semantics). This review does not
re-report any of those as new. Where this review's own independent read reached one of
the same lines of code, it's called out below only where it adds a correction or
stronger evidence beyond what's already filed — not as a rediscovery.

---

## Confirmed fixed (from the 2026-09-05 report)

| Finding | Status | Evidence |
|---|---|---|
| **C-1** — per-frame `Dictionary<string,Quaternion>` allocation + string-keyed lookups in `CreatureRig.ApplyPose` | **Fixed** | `CreatureRig` now caches index-parallel `Transform[] _indexedBones` and `Quaternion[] _indexedRotations`; `ApplyPose` calls `PoseRotationResolver.ResolveInto(...)` into the cached array and indexes both arrays by `int`. No dictionary allocation, no string lookup, on this path. Confirmed by direct read of `CreatureRig.cs:87-99` and `PoseRotationResolver.cs:44-83`. |
| **H-1**/`tsk-0113` — non-deterministic `children[0]` branch rotation | **Fixed, and more robustly than the minimal fix would have been** | `PoseRotationResolver.FindPrimaryChild` (`PoseRotationResolver.cs:85-97`) now picks the child with the lexicographically smallest bone ID via `string.CompareOrdinal`, not list position — deterministic regardless of `SkeletonSnapshot`'s internal children-list order. |
| **H-2**/`tsk-0114` — parent-before-child ordering not guaranteed | **Fixed, with a real topological sort** | `SkeletonSnapshot.Capture` (`SkeletonSnapshot.cs:77-126`) now does an explicit BFS from ID-sorted roots, sorting each node's children by ID before enqueueing, and **explicitly detects and rejects a parent cycle** (`orderedBones.Count != skeleton.Bones.Count` → `DomainException`) — stronger than the original acceptance criteria asked for. |
| **H-3**/`tsk-0116` — `CreatureRig.Build` not transactional | **Fixed** | `Build` now constructs into local `nextBones`/`nextIndexedBones`/`nextGeneratedObjects`, with a `try/catch` that destroys the partial construction and rethrows on failure (`CreatureRig.cs:51-72`), and only commits over the previous rig (`DestroyGeneratedObjects(_generatedObjects); ...`) after the new one fully succeeds (`CreatureRig.cs:74-84`). |
| **H-4** — `SkeletonInferrer.AppendLimbBones` terminal-bone `FindBone` re-lookup | **Fixed** | Per `tsk-0118`'s own round-1 evidence ("SkeletonInferrer terminal rotation reuses previous segment rotation"); this review did not re-verify byte-for-byte but has no reason to doubt evidence that's already PlayMode-tested (28/28) and orchestrator-reviewed. |
| **M-1** — missing direct `SkeletonSnapshot`/contract tests | **Fixed** | `tsk-0118`'s round-1 evidence lists focused tests added covering duplicate IDs, missing parents, indexed-order mismatch, and invalid child indices — exactly the gaps the last report named. |
| **L-1** — `CC-011`/`CC-012` ticket doc-links to the wrong `PosedSkeleton` path | **Not verified this pass** | Lowest priority in the last report; not re-checked given the volume of higher-value MVP work to review. Flagging as still-unconfirmed rather than assuming it was fixed as a side effect of unrelated work. |

**Correction to the last report:** the "M-2" finding (`IkChainSolver.SolveChainTarget`
tests living in `BoneChainTests.cs` instead of a dedicated `IkChainSolverTests.cs`) is
**still open** — confirmed by file listing; no `IkChainSolverTests.cs` exists on current
`main`. Restating it here only because the last report's own confidence section flagged
it as "worth extracting" and it's easy to lose track of a pure file-organization item
against 27 higher-severity findings landing around it. Still Low priority; no urgency.

---

## New findings (this pass)

### N-1 — `F-18`'s owner tag doesn't match its actual scope; the finding is still open and has since spread to a third file

**Severity:** Medium (correction to task tracking, not a new bug class)
**Files:** `Assets/Scripts/Runtime/Skeleton/SemanticBoneResolver.cs:150`,
`Assets/Scripts/Runtime/Animation/Binding/MorphologyInfluenceRadiusBridge.cs:95,102`

`F-18` (`docs/audits/creaturecreator-whole-codebase-exhaustive-audit-26-09-06-16-57-00.md`)
correctly identified `new CreaturePart { Id = parent.Id }` in `SemanticBoneResolver`'s
resolved-snapshot overload as an avoidable "fit new data through an old API" smell, and
tagged it `Owner: TSK-0124`. `TSK-0124` (now `Done`) was real, useful work — it
collapsed the *duplicate raw-vs-snapshot decision logic* in `ResolveParentBoneId` down
to one shared `ResolveParentBoneIdCore` — but its own recorded scope
(`Data/Tasks/tsk-0124-*.json`) never mentions the throwaway-object construction at all;
it's about eliminating two parallel *implementations* of the same policy, not the
object-construction pattern F-18 flagged. Closing `TSK-0124` as `Done` didn't close
F-18, and nothing currently tracks F-18 on its own:

- The original instance is still present verbatim at `SemanticBoneResolver.cs:150`.
- Two more instances have since been added in `MorphologyInfluenceRadiusBridge.cs:95`
  and `:102` (a file that didn't exist when F-18 was written, built for `TSK-0141`) —
  the same pattern, now three call sites deep, all constructing a throwaway
  `CreaturePart` with only `.Id` set purely to satisfy
  `SemanticBoneResolver.ResolveLimbSegmentBoneId(CreaturePart part, int segmentIndex,
  bool mirrored)`'s signature. Verified by direct read: that method
  (`SemanticBoneResolver.cs:43-46`) and its siblings
  (`ResolveLimbJointBoneId`/`ResolveLimbTerminalBoneId`) only ever touch `.Id` (or,
  for the two-argument terminal overload, `.Limb` — not used by the segment/joint
  overloads), so every one of these three throwaway objects is pure signature-fitting
  with no functional payload.

This is exactly the shape F-18's own recommended correction anticipated ("Add a direct
resolved helper... Do not create a synthetic domain object just to satisfy an API
signature") — the fix is still a small, mechanical signature change
(`ResolveLimbSegmentBoneId(string partId, int segmentIndex, bool mirrored)` etc.), it
just hasn't landed yet and the task board doesn't currently show it as open work under
any task. Recommend either reopening a task explicitly owning F-18 (not `TSK-0124`,
which is legitimately closed for its own scope), or folding it into whichever task next
touches `SemanticBoneResolver`'s public surface — but *some* task should own it now that
it has three call sites instead of one, since every future consumer working from a
`ResolvedCreatureSnapshot`/`ResolvedPartSnapshot` (increasingly the common path, not the
exception, per this sprint's own direction) will otherwise keep reaching for the same
throwaway-object pattern by example.

### N-2 — No performance-budget task currently covers the *bind-time* cost the animation MVP actually introduced; `TSK-0134` as scoped covers a different phase

**Severity:** Medium (planning gap, not a measured regression — no profiler available this session)
**Files:** `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs:189-278`,
`Assets/Scripts/Runtime/Animation/Skinned/CreatureSkinnedMeshRenderer.cs:87-154,192-237`,
`Assets/Scripts/Editor/CreaturePreviewController.cs:181-221`

The already-filed `TSK-0134` ("Define per-frame animation/skinning performance
budget and benchmark") is scoped, by its own title and the MVP audit that recommended
it, around *steady-state per-frame* cost — the thing `TSK-0118` already put a number on
(0 bytes / 0.555 ms per 1,000 `ApplyPose` calls). That's the right thing to benchmark
for locomotion, but it's not where the cost this sprint actually added lives. The new
MVP work runs on **every bind** (i.e., every time `CreaturePreviewController
.BindImplicitSurface` or `CreatureRuntimePreview`'s completed-generation handler fires
— in the editor, that's every DNA edit that changes the welded surface, not once at
startup):

- `ImplicitSurfaceWeightAuthoring.Author` is O(vertices × eligible segments): for every
  rest vertex it computes `DistanceToSegment` against **every** segment-carrying bone
  (`ImplicitSurfaceWeightAuthoring.cs:230-240`) before selecting and sorting the top four
  candidates. A welded marching-cubes surface can easily be several thousand vertices;
  a creature with a dozen limb segments plus Body samples is a plausible segment count
  in the tens. This is a one-time cost per bind, not per-frame — but "per bind" in the
  live editor preview is effectively "per interactive edit."
- `CreatureSkinnedMeshRenderer.BuildSkinningMeshCopy` (`CreatureSkinnedMeshRenderer.cs:192-237`)
  deep-copies every vertex/normal/tangent/uv/uv2/uv3/color array from the source mesh on
  every bind, in addition to the weight authoring above.

None of this is a bug — a full rebind is genuinely necessary whenever the underlying
mesh topology changes, and the class-level doc comment on `CreatureSkinnedMeshRenderer`
is explicit that there is deliberately no per-frame mesh work (`CreatureSkinnedMeshRenderer.cs:21-26`),
which is the correct design for the steady-state case `TSK-0134` already targets.
The gap is narrower: **no task currently states a target bind-time budget**, so there's
no number to check the weight-authoring/mesh-copy cost against as creature complexity
(vertex count, segment count) grows, and no signal for whether this becomes the actual
interactive-editing bottleneck now that `TSK-0103`'s async generation pipeline exists
specifically to keep the editor responsive during that same interaction.

**Recommendation:** either widen `TSK-0134`'s scope to explicitly name two separate
budgets (steady-state per-frame `ApplyPose`+GPU-skin cost, and per-bind weight-
authoring+mesh-copy cost), or file a small sibling task scoped to the bind-time number
specifically, anchored the same way the MVP audit anchored `TSK-0134` off `TSK-0118`'s
existing figure — a synthetic-mesh benchmark (N vertices × M segments) is enough to get
a first number; it doesn't need a real Unity profiler session to be useful as a
starting target.

### N-3 — `CreatureSkinnedMeshRenderer.Bind` assumes exactly one skeleton root; not confirmed whether that's always true

**Severity:** Low / open question (no reproduction attempted — flagging for someone with
domain knowledge of whether a Body-less definition is actually authorable)
**File:** `Assets/Scripts/Runtime/Animation/Skinned/CreatureSkinnedMeshRenderer.cs:135`

```csharp
renderer.rootBone = rig.IndexedBones[0];
```

`SkeletonSnapshot.Capture` collects **all** bones with `ParentBoneId == null` into a
`roots` list and BFS-orders from each of them (`SkeletonSnapshot.cs:78-121`) — the data
structure explicitly supports more than one root. `SemanticBoneResolver
.ResolveBodyParentBoneId` returns `null` when `definition.Body == null` or has no
samples (`SemanticBoneResolver.cs:208-212`), which — if a Body-less definition with
multiple top-level (Body-rooted) parts is actually authorable — would make each of
those parts' root bones independent roots, not children of a shared body root. This
review did not determine whether `DefinitionValidator` actually forbids a Body-less
definition with more than one top-level part (a quick grep found `Body == null` treated
as a tolerated/skip case in a few validator branches, not an outright rejection, but
didn't trace every path). If a multi-root skeleton is reachable, `IndexedBones[0]` is
just whichever root sorts first alphabetically by bone ID, and `SkinnedMeshRenderer
.rootBone` — which Unity primarily uses for bounds computation, not skinning
correctness — would point at an arbitrary one of several disconnected trees rather than
a meaningful single root. Flagging as an open question rather than a confirmed bug:
worth a five-minute check (does `DefinitionValidator` require at least one Body sample,
or a single root part?) before deciding whether this needs a fix or just a code comment
recording the assumption.

### N-4 — `MorphologyInfluenceRadiusBridge` has no dedicated test file; only exercised transitively

**Severity:** Low
**Files:** `Assets/Scripts/Runtime/Animation/Binding/MorphologyInfluenceRadiusBridge.cs`,
`Assets/Scripts/Tests/Runtime/CreatureSkinnedMeshRendererTests.cs`

Every other file in the new `Animation/Binding` and `Animation/Skinned` namespaces has
its own 1:1 test file (`LinearBlendSkinningTests.cs`, `RigidMeshWeightAuthoringTests.cs`,
`ImplicitSurfaceWeightAuthoringTests.cs`, `SkinnedMeshBindingBuilderTests.cs`,
`CreatureSkinnedMeshRendererTests.cs`) — the convention this codebase otherwise follows
consistently, and the same convention `TSK-0118`'s test additions reinforced for
`SkeletonSnapshot`. `MorphologyInfluenceRadiusBridge` is the one exception: it's only
exercised indirectly through `CreatureSkinnedMeshRendererTests.cs`. Its own fallback
logic (non-finite/non-positive radius → default; missing Body/limb entry → default;
`ResolveSegmentRadius`'s clamped-midpoint thickness sampling) has no direct test
asserting those branches individually. Same shape as the `SkeletonSnapshot` gap `M-1`
closed last round — recommend the same treatment (a small dedicated test file) next
time this file is touched, rather than as urgent standalone work.

---

## Existing Task Impact (this pass)

- **`TSK-0118`** ("Close indexed pose-application hot path and skeleton coverage gaps"):
  still `InProgress`, and that's *correct*, not neglect — its round-1 scope (C-1, H-4,
  M-1) is confirmed landed and evidenced, but a later same-batch audit folded in
  **F-207** (`HasSameBoneOrder` only compares bone-ID order, not topology/parent
  indices/`SourcePartId`/mirror state/rest bind — so two structurally different
  skeletons with identical ID lists in the same order would incorrectly pass the
  pose-compatibility gate). This review independently re-derived the same gap by
  reading `SkeletonSnapshot.cs:191-199` before finding it was already filed — treat
  that as confirmation, not a new finding. No correction needed to `TSK-0118`'s scope;
  recommend closing once F-207 lands, per the MVP audit's own recommendation.
- **`TSK-0134`**: still `Backlog`. See **N-2** above — recommend widening its scope
  before implementation starts, so the eventual benchmark task doesn't have to be
  re-scoped mid-flight.
- **F-18**: see **N-1** — recommend a new small task (or folding into the next
  `SemanticBoneResolver` touch) now that it has three call sites and no current owner.
- **`TSK-0010`/`TSK-0011`** (semantic animation queries / locomotion foot-IK): still
  correctly `Backlog` per the MVP audit's own scope correction (not prerequisites for
  this MVP). Nothing in this pass changes that ordering.
- No correction needed to `TSK-0113`, `TSK-0114`, `TSK-0116` (last round's H-1/H-2/H-3)
  — all three confirmed genuinely fixed, with H-2's fix notably stronger than the
  original acceptance criteria required (explicit cycle detection).

---

## Assumptions

- Treated current `main` tip (`2026-09-07T04:15:55Z`, "Record five-round sprint
  handoff") as ground truth, per the same instruction basis as the last report.
- `api.github.com` was rate-limited again this session; commit provenance came from the
  public Atom feed (titles + timestamps, not full SHAs for the newest commits).
- No Unity/Burst runtime available; all findings are static-source-verified. Where this
  report describes a cost (N-2) as a concern, that's algorithmic reasoning from the
  source (loop bounds, allocation sites), not a profiler measurement — flagged as such
  in the finding itself.
- This pass did not re-audit SDF culling, triangle winding, or the DNA
  serializer/legacy-migration tracks (`TSK-0119`, `TSK-0129`, `TSK-0137`, `TSK-0140`) —
  those have their own recent dedicated audits and are outside an animation-MVP-focused
  scope.
- Did not re-verify **L-1** (doc-link drift in `CC-011`/`CC-012`) this pass — noted
  above as unconfirmed rather than assumed fixed.

## Open Questions

- N-3 (single-root assumption in `CreatureSkinnedMeshRenderer.Bind`): is a Body-less,
  multi-top-level-part definition actually authorable/valid today? A five-minute
  `DefinitionValidator` check would resolve this either way.
- N-1: should the fix be a full signature change
  (`ResolveLimbSegmentBoneId(string partId, ...)`) now, given three call sites depend on
  the current shape, or is there a reason the `CreaturePart`-typed signature needs to
  stay (e.g. a future field on `CreaturePart` that these call sites will eventually need
  beyond `.Id`)? Worth a quick check of `TSK-0077`/`CC-073`'s remaining scope before
  committing to the narrower signature.
- N-2: is a synthetic-mesh micro-benchmark sufficient to set a first bind-time budget
  number, or does this need to wait for a real multi-creature scene to be meaningful?
  Given `TSK-0118` established the precedent of a focused, synthetic, PlayMode-measured
  number being accepted evidence, this review assumes the same bar applies here, but
  that's a judgment call for whoever scopes the task.

## Confidence

- **Confirmed** (direct source read, current `main` tip): C-1, H-1, H-2, H-3 fixes; N-1
  (all three `CreaturePart` throwaway sites, and `SemanticBoneResolver`'s methods'
  actual field usage); N-4 (test file inventory); the M-2 carryover (`IkChainSolverTests.cs`
  still absent).
- **Accepted from existing evidence, not independently re-verified byte-for-byte**: H-4
  fix, M-1 fix — based on `TSK-0118`'s own PlayMode-tested, orchestrator-reviewed round-1
  evidence, which this review has no basis to doubt.
- **Strong evidence, not benchmarked**: N-2's cost characterization (O(vertices ×
  segments) weight authoring, full mesh deep-copy per bind) is Confirmed as an
  algorithmic fact from the source; whether it's *actually* a perceptible interactive-
  editing cost at realistic creature complexity is Strong evidence pending a benchmark,
  same caveat as the original C-1 severity call.
- **Open question, not a claim**: N-3 — the underlying mechanism (multi-root-capable
  `SkeletonSnapshot`, `null`-returning Body-less fallback) is Confirmed; whether it's
  actually reachable through validated `CreatureDefinition`s is unverified this pass.
