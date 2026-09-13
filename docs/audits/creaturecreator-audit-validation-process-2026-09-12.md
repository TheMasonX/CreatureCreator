# CreatureCreator — Validation, Evidence & Task-System Audit

**Report ID:** `CC-AUDIT-VALIDATION-20260912-2F9A6C51`
**Suite:** `CC-AUDIT-SUITE-20260912-9F42C6D1`
**Branch base audited:** `310d393279520b2faf7bad2613e37e82282862dc`
**Primary sources:** `Data/Tasks/*`, task README/skills, audit corpus, Unity-validation skill, current runtime/editor tests, ADRs, latest branch history
**Unity execution:** unavailable

## Executive assessment

The migration to MemorySmith `TSK-*` records is the correct active tracking model. The branch contains a large historical audit corpus, current task records, ADRs, and implementation evidence. The remaining weakness is **evidence lifecycle discipline**: source completion, test completion, Unity validation, and user acceptance are still represented inconsistently across tasks.

This is now an engineering-system issue. The code can be correct and still remain operationally untrusted because the task says “implemented” while its Unity acceptance gate is unresolved.

## Findings

### VT-01 — Source completion and behavioral completion are repeatedly conflated

**Severity:** P2  
**Confidence:** 99%  
**Owner:** task-system process

TSK-0203 is an excellent example: the source compatibility guard is implemented, but Unity regression and bounds/culling remain open. The task remains `InProgress`, which is correct.

Future tasks should explicitly separate:

```text
implemented
unit-tested
Unity-tested
user-accepted
```

A task should not be moved to complete merely because the first two are green.

### VT-02 — Unity-gated work needs reproducible test identities

**Severity:** P2  
**Confidence:** 98%  
**Owner:** unity-validation process

A statement like “tested in Unity” is weak evidence unless it names:

- Unity version;
- test fixture/scene;
- test count or scenario matrix;
- commit SHA;
- console cleanliness status;
- special manual observations.

Historical records often include some of these, but not uniformly.

### VT-03 — Acceptance criteria should distinguish deterministic facts from visual judgment

**Severity:** P2  
**Confidence:** 97%  
**Owner:** task authors

“Mesh looks correct” is not a useful acceptance criterion by itself.

For deformation, pair visual acceptance with measurable invariants:

- vertex remains finite;
- rest round-trip error below threshold;
- mirrored counterpart error below threshold;
- renderer remains visible under exaggerated pose;
- expected bone count and bind array lengths match.

Use manual visual review only for properties that cannot reasonably be reduced to a deterministic test.

### VT-04 — Historical audits are not uniformly dispositioned

**Severity:** P2  
**Confidence:** 99%  
**Owner:** audit process

The repository contains many audits with overlapping themes. Some findings are now resolved, some are owned by current tasks, and some are superseded by architecture changes.

The recurring failure mode is a synthesis that remembers the headline finding but loses small “footnote” findings.

Adopt a reconciliation table:

| Source finding | Current mechanism | Current owner | Disposition | Evidence |
|---|---|---|---|---|
| F-x | helper/task/code | TSK-xxxx | fixed/extended/duplicate/deferred | commit/test |

No finding should disappear without an explicit disposition.

### VT-05 — Audit report IDs are valuable and should be treated as durable evidence keys

**Severity:** P3/P2  
**Confidence:** 99%  
**Owner:** audit process

The user's preferred unique report-hash convention is well suited to the current workflow. Keep an immutable report ID inside every audit and reference it in task comments/hand-offs when a finding survives synthesis.

Do not use timestamps alone because two reports can be created near-simultaneously and they do not uniquely identify content.

### VT-06 — Disabled CI leaves an evidence gap that must be consciously owned

**Severity:** P2  
**Confidence:** 98%  
**Owner:** TSK-0123/process

The repository intentionally disabled its old CI gate during the move to the MemorySmith task system. That is an explicit user/system decision, not an accidental outage.

The consequence is that validation must be driven by the task/Unity workflow rather than assumed from GitHub Actions status.

This should be stated prominently in the project engineering README so new contributors do not interpret the absence of CI as a missing setup task.

### VT-07 — Task records should never encode contradictory ownership or stale CC identifiers as active authority

**Severity:** P2  
**Confidence:** 98%  
**Owner:** task-system process

The project has deliberately migrated CC Markdown tickets into `TSK-*` records. New audit output should therefore reference `TSK-*` owners as canonical and use CC keys only for historical provenance.

This prevents agents from reopening retired ticket systems accidentally.

### VT-08 — Task schemas need a standard “validation blockers” field

**Severity:** P2  
**Confidence:** 95%  
**Owner:** MemorySmith task integration

Current descriptions/comments embed blockers in prose. This makes automated reporting difficult.

A structured validation section should include:

```text
sourceStatus
unitStatus
unityStatus
manualStatus
blockers[]
lastValidatedCommit
```

If the underlying MemorySmith schema cannot support these fields, preserve the same vocabulary in a stable markdown/JSON convention.

### VT-09 — “Regression added” is insufficient unless the regression targets the historical failure mechanism

**Severity:** P2  
**Confidence:** 96%  
**Owner:** task authors/reviewers

A same-count skeleton regression is valuable for TSK-0203 because it attacks the original indexing hazard. But adding any test to a task should not be treated as complete evidence.

Each regression should state:

- historical failure mechanism;
- why the fixture triggers it;
- expected failure before fix;
- expected success after fix.

### VT-10 — Performance claims need explicit workload envelopes

**Severity:** P2  
**Confidence:** 99%  
**Owner:** TSK-0008/0104/0134

“Fast” and “zero allocations” need a workload definition. At minimum specify:

- vertex count;
- bone count;
- voxel resolution;
- number of appearance programs;
- requests per second;
- warmup state;
- profiler scope.

Otherwise an optimization can regress materially while still satisfying the words in the task.

### VT-11 — Unity-gated animation findings should have one canonical adversarial creature fixture

**Severity:** P1/P2  
**Confidence:** 96%  
**Owner:** animation validation family

The project repeatedly uses “dino” and simple generated creatures. A single adversarial fixture should deliberately include:

- mirrored limbs;
- multi-joint limb chains;
- terminal attachment geometry;
- at least one nontrivial mesh asset;
- exaggerated bend and twist;
- actor-root translation/rotation;
- long and short segments;
- material palette references;
- enough vertices to expose performance issues.

This dramatically reduces the number of separate Unity smoke tests needed.

### VT-12 — Audit artifacts themselves should be checked for current-branch placement

**Severity:** P2  
**Confidence:** 99%  
**Owner:** audit process

The present user report exposed a practical workflow risk: an audit can be generated and referenced by hash/URL but still be difficult to locate unless the branch head is explicitly reverified after writing.

Every audit write operation should end with:

1. branch head verification;
2. file existence check at that exact head;
3. direct URL captured;
4. report ID recorded.

## Required process gate for future audits

Before reporting completion:

```text
resolve branch head
→ inspect current head
→ audit
→ write artifact(s)
→ verify file(s) at new head
→ verify branch ref moved to new head
→ report exact SHA(s)
```

## Exclusions

Do not create a replacement CI system merely because CI is disabled. The project explicitly chose task/MemorySmith validation ownership; revisit only if the user changes that policy.

## Conclusion

The technical codebase is increasingly disciplined. The highest leverage now is making **evidence as structured as code**. That means explicit validation states, reproducible Unity scenarios, durable audit IDs, and no silent loss of historical findings during synthesis.
