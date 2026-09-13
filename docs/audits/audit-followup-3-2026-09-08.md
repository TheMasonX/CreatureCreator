# CreatureCreator — Audit Follow-Up #3 (2026-09-08, later state)

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `254ffea`
(up from `4b3d4f9` — 4 commits, a smaller batch than my last two passes).

---

## 1. A real aliasing/ownership bug fixed: `GeneratedCreatureData.Definition` was stored by reference

`GeneratedCreatureData`'s own doc comment states it is an *"immutable
handoff between pure generation and Unity assembly"* with mutable inputs
*"defensively copied so consumers cannot mutate the generated result."* The
`Definition` field was the one exception — stored as `Definition = definition`
(a bare reference), not cloned. Fixed in `3ccb7e4` to `definition?.Clone()`,
with a real regression test (`f2468a3`) that mutates the source definition
after construction and asserts the stored copy is unaffected.

This is a legitimate find, not busywork: a producer holding a reference to
the same `CreatureDefinition` it just handed off (the editor continuing to
mutate a definition while a background generation result is still in
flight, for instance) could previously have corrupted an already-"generated"
result after the fact — silently, since nothing would throw. It's adjacent
to, but distinct from, the `TSK-0104` transactional-preview-replacement gap
I flagged last round: that one is about *object* lifecycle (destroy-before-
confirm), this one was about *data* lifecycle (mutate-after-handoff). Worth
noting both share a root theme — this codebase's async/editor boundary has
had more than one hole of this general shape.

## 2. A third build-break incident: wrong field name in a test, self-corrected one commit later

`f2468a3` added a regression test referencing `Generation.VoxelPerUnit`.
The actual field is `VoxelsPerUnit` (plural). `254ffea`, the very next
commit, fixes the typo. This is now the **third** distinct compile-break
I've traced across three audit rounds on this branch (missing paren in
`CreatureMeshGenerator`; `internal`-vs-`public` `EnqueueCaptured`; now this).
Each was self-corrected within one commit, so none of them represent
ongoing risk by themselves — but three in three consecutive review windows
is a pattern worth naming plainly: **this branch is currently landing
roughly one compile-break per audit cycle**, all caught by the next
commit's author (human or agent) actually attempting to build rather than
by any automated gate. I'm not re-raising `TSK-0123` — just tracking the
base rate, since "self-corrects quickly" depends entirely on someone
building before the next person stacks work on top.

## 3. A finding worth flagging precisely because it's easy to wave through: "five documented pre-existing failures" may no longer mean what it used to

`TSK-0162`'s closure comment cites *"full PlayMode 380/385 with the five
documented pre-existing failures."* That phrase — "five documented
pre-existing failures" — is a real, historically meaningful baseline in this
project: I traced it back through `TSK-0086`/`0087`/`0088` (CC-082/083/084),
where it referred to five *specific, named* failing tests (three
`DefinitionValidator` duplicate-ID throws, one `NoParent` validation gap,
one `DisplayName` round-trip mismatch) against a **428-test** full suite. All
five were individually root-caused and fixed by 2026-08-25, and the
close-out on `TSK-0060` explicitly confirms *"full PlayMode 428/428 green
(all five pre-existing failures fixed by CC-082/083/084)."*

`TSK-0162`'s citation is against a **385-test** suite — a different total,
consistent with test reorganization since August but not verifiable from
here as the *same five tests*. Two explanations are equally plausible from
what's in the task record alone: (a) this is a legitimate, differently-
scoped run (a named test filter or an updated fixture count) that happens to
also have five accepted exceptions, correctly labeled by convention; or (b)
"five pre-existing failures" has become a reassuring stock phrase applied to
whatever the current failure count happens to be, without re-verifying
they're the same known-safe set each time. I can't distinguish these from
source alone — this needs someone to actually list the five failing test
names in the next run that cites this phrase and confirm they match a
currently-maintained known-failures list (I didn't find one; if it exists,
point me to it and I'll cross-check it directly next round).

This is exactly the kind of thing worth catching early: a label that was
precise and true in August quietly becoming a rubber stamp by September is
a slow, easy-to-miss failure mode, and it's the sort of gap a live human
skim of task comments won't catch either, since the phrase reads as
reassuring on every single occurrence.

## 4. Task board: stable, no new integrity issues

Re-scanned all 185 parsable records: **zero duplicate keys** (holds from
last round), `Done` now 93 (up from 92), `Backlog` 46 (down from 48). No
new tasks were created that overlap existing ownership, consistent with the
project's established discipline. `tsk-0156` is still the one unparseable
record — unchanged across four consecutive reviews now; still cheap, still
untouched.

`CreatureEditorWindow.cs` is still 3180 lines.

## Recommendation for next round

Given three compile-breaks in three rounds, I'd suggest a cheap, low-friction
mitigation that doesn't reopen the CI conversation: before closing any task
with a "0 compile errors" claim, actually run (or have the next session run)
a full-solution `dotnet build` rather than trusting the single file that was
touched — all three breaks I found were in files adjacent to, not identical
to, the one being edited in that commit (a cross-file reference going stale).
That's a one-command check, not a pipeline.

Separately: I'd like to verify finding #3 (the failure-count label) against
whatever list of currently-accepted failing tests exists, if one is
maintained — pointing me at it would let me close this out cleanly next
round instead of leaving it as an open question.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| `GeneratedCreatureData.Definition` aliasing bug fix is real and correctly tested | Confirmed (read fix + regression test) |
| Third build-break incident (`VoxelPerUnit` typo), self-corrected | Confirmed (traced both commits) |
| "Five pre-existing failures" phrase's continued accuracy is unverifiable from source alone | Open question, not a confirmed defect — flagged for follow-up |
| Task-board integrity stable, no new duplicates | Confirmed (direct rescan) |
| `CreatureEditorWindow.cs` still stalled at 3180 lines | Confirmed |
