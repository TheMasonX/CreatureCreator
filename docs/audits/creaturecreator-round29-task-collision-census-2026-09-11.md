# CreatureCreator — Round 29: 14 Task Numbers Are Currently Double- (or Triple-) Allocated — Most Still Live and Unresolved

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `0afbaee` (no new commits since Round 28)
**Trigger:** while checking `TSK-0204`/`TSK-0205` (referenced by the new generation-integrity-validation handoff) against `Data/Tasks/`, found each was itself double-allocated — same shape as Round 17's `TSK-0188`/`TSK-0189` finding. Ran a full census this time rather than stopping at two.

---

## Full census: 14 colliding numbers, out of 228 task files

```
ls Data/Tasks | grep -oE '^tsk-[0-9]{4}' | sort | uniq -c | sort -rn | awk '$1>1'
      3 tsk-0200
      2 tsk-0205  2 tsk-0204  2 tsk-0203  2 tsk-0202  2 tsk-0201
      2 tsk-0199  2 tsk-0198  2 tsk-0197  2 tsk-0196  2 tsk-0195
      2 tsk-0189  2 tsk-0188  2 tsk-0153
```

`TSK-0188`/`TSK-0189` are the exact pair Round 17 flagged. `TSK-0195`–`TSK-0205` is an almost-unbroken run of 11 consecutive numbers, each shared by two (or for `0200`, three) unrelated task files created around the same time — strongly consistent with what this whole session has observed repeatedly: multiple concurrent audit/work sessions each allocating their own "next" task number from a snapshot that didn't account for what the other was creating at the same time.

## The project has already been resolving these — by archiving, not renumbering

Checked `TSK-0153`, `TSK-0188`, and `TSK-0189` (all previously flagged, `0188`/`0189` by me in Round 17) — all three are now resolved, but not the way I recommended (renumber to the next free slot). Instead, one member of each collided pair was marked `Archived`, leaving the other as the live task at that number:

| Number | Resolved via archiving |
|---|---|
| `0153` | `Archived`: "Fix Body spline spacing epsilon unit mismatch" · Live: `Backlog` "Normalize initial creature placement..." |
| `0188` | `Archived`: "Define full indexed animation pose contract" · Live: `InProgress` "Make debug rig attachment bones follow posed bones..." |
| `0189` | `Archived`: "Add reusable zero-allocation animation pose buffer" · Live: `Done` "Make task-record normalization collision-safe" |
| `0202` | `Archived`: "Cache Body arc-length prefix data..." · Live: `Backlog` "Cache Body arc-length prefixes..." (near-identical topic — looks like a genuine supersession, not two unrelated tasks colliding) |
| `0203` | `Archived`: "Measure dense extraction ownership memory before further redesign" · Live: `Backlog` "...before redesign" (same — near-identical, likely a supersession) |

This is a workable pattern (an `Archived` status unambiguously signals "not the current referent of this number" to a reader), even though it leaves two files sharing the same numeric prefix on disk permanently rather than freeing the number — different from what I recommended in Round 17, but functionally addresses the actual harm (ambiguity for a reader/agent encountering "TSK-0188" in conversation or a commit message). Worth naming as the project's now-established de facto resolution convention, since future rounds should check for it before re-flagging an old collision as unresolved.

## What's still genuinely live and unresolved: 9 numbers, none archived

None of these have an `Archived` member — both (or all three) files at each number are currently active (`Backlog`/`InProgress`/`Done`), and the topics are unrelated pairs, not near-duplicate supersessions:

| Number | Task A | Task B | Task C |
|---|---|---|---|
| `0195` | Backlog: "Harden non-finite body-bone radius handling in morphology bridge" | InProgress: "Sanitize non-finite Body proxy radius before morphology fallback" | — |
| `0196` | Rejected: "Repair malformed task record blocking fail-closed normalization" | Backlog: "Short-circuit non-finite limb joint validation before geometry math" | — |
| `0197` | Done: "Apply audit recommendations to agent and skill guidance files" | InProgress: "Use cross-thread-safe allocator for background generation scratch" | — |
| `0198` | Backlog: "Define portable animation clip and deterministic sampling contract" | InProgress: "Stream preview hot paths and remove extraction hash overhead" | — |
| `0199` | Backlog: "Cache pose topology decisions in SkeletonSnapshot" | InProgress: "Reuse resolved Body frames in appearance baking" | — |
| `0200` | Backlog: "Define animated bounds and culling policy for skinned meshes" | Backlog: "Prove sparse candidate-region SDF sampling before enabling it" | InProgress: "Restrict SDF sampling to conservative root-envelope candidates" |
| `0201` | Backlog: "Prove limb binding radii cover the sampled metaball envelope" | Backlog: "Use proven root potential bounds during appearance resolution" | — |
| `0204` | Backlog: "Consolidate committed creature fixture corpus" | InProgress: "Prove repeatable generated field and mesh determinism" | — |
| `0205` | Backlog: "Align Body appearance documentation with spine-frame implementation" | Backlog: "Repair invalid implicit-mesh topology after determinism isolation" | — |

`0195` is a near-duplicate pair (like `0202`/`0203`) and is plausibly headed for the same "archive one" resolution once someone notices — flagging it in this table anyway since, unlike `0202`/`0203`, neither side is archived yet, so the live ambiguity still exists today. The rest (`0196`–`0201`, `0204`, `0205`) are genuinely unrelated topics sharing a number, the same harmful shape as the original `0188`/`0189` finding.

**Worth naming specifically:** `TSK-0197` is one of the ones I personally cited by number in Rounds 25 and 28 (the cross-thread-safe-allocator fix) — meaning this exact collision was live and active while I was writing about it, and anyone reading those rounds' "TSK-0197" references without also checking `Data/Tasks/` directly could easily land on the wrong file (the unrelated "apply audit recommendations to skill files" one). Concrete, current evidence the ambiguity isn't just theoretical.

`TSK-0204`/`TSK-0205` are the two numbers the brand-new generation-integrity-validation handoff (`docs/handoffs/creaturecreator-generated-mesh-integrity-validation-handoff-26-09-11.md`) explicitly names and depends on for its own diagnostic instructions — so this collision has immediate, practical relevance: whoever picks up that handoff needs to know it means the *InProgress* `TSK-0204` ("Prove repeatable generated field and mesh determinism") and the *Backlog* `TSK-0205` ("Repair invalid implicit-mesh topology..."), not their same-numbered siblings.

## Checked against the collision-safe normalizer

Same conclusion as Round 17: `Scripts/Normalize-TaskRecords.ps1`'s fail-closed duplicate-key preflight (`TSK-0189`'s own subject, now itself part of a resolved collision) would catch every one of these if run — it wasn't, or wasn't acted on, for any of the 9 still-live pairs. This continues to point at the same structural gap Round 17 already named: nothing forces this script to run at task-creation time, and no CI gate exists by deliberate choice (`TSK-0123`, rejected).

## Recommendation

1. Apply the project's own now-established resolution pattern (archive the superseded/duplicate member) to the 9 still-live collisions, prioritizing `TSK-0197` and `TSK-0204`/`TSK-0205` given they're both actively being referenced in current work (this audit series and the new integrity-validation handoff, respectively).
2. `0195`, `0202`, and `0203` look like genuine same-topic supersessions rather than independent collisions — worth confirming that read and, if correct, no content work is needed there, just the archival bookkeeping.
3. Still recommend deciding when `Normalize-TaskRecords.ps1` actually runs (Round 17's original ask) — a pre-commit hook, as suggested earlier this session, remains a reasonable answer; nothing about this round changes that recommendation, it just adds nine more data points that the gap is real and recurring, not a one-off.
