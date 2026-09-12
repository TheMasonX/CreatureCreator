# CreatureCreator — Audit Round: Rest-Pose Identity Bug — Correctly Fixed and Rarely Well-Validated

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `785f204`
(unchanged tip — no new commits since last round; this round reads
`42aab9b`, the substantial rig-rotation/rest-pose commit I hadn't examined
yet).

---

## A genuinely significant bug, fixed correctly, with the strongest validation signal I've seen in this whole review

`PoseRotationResolver` used to rebuild every non-terminal bone's rotation
via `Quaternion.LookRotation(forward, up)` — reconstructing the *entire*
orientation from a forward/up basis every call. That discards whatever
roll (twist around the bone's own axis) was baked into the bind rotation
whenever the reconstructed basis doesn't happen to reproduce it exactly.
Consequence, stated plainly in the commit message: **the rest pose itself
— zero animation applied — deformed the skinned mesh**, because bind poses
are the mathematical inverse of rest frames, and `LookRotation`'s
reconstruction wasn't guaranteed to round-trip back to the exact original
rotation.

The fix (`ResolveAimRotation`) is the right shape: instead of
reconstructing an orientation from scratch, it computes the **swing**
needed to rotate from the rest-frame aim direction to the posed aim
direction (`Quaternion.FromToRotation(restForward, posedForward)`) and
applies that as a delta on top of the *original* bind rotation
(`* restRotation`). This makes the rest-pose-identity property
**algebraic, not incidental**: when posed direction equals rest direction
(no animation), `FromToRotation` of a vector against itself is exactly
`Quaternion.identity`, so the result is exactly `restRotation` — the
original bind rotation, roll included, with no reconstruction step that
could lose it. I checked this holds for the degenerate-direction fallback
path too (falls back to `restRotation` directly, same safe behavior as
before).

**Validation is unusually strong for this review:** `EditMode 164/164`,
`PlayMode` green apart from a named pre-existing failure (`TSK-0194`,
already tracked, not this change's responsibility), a dedicated
`PoseRotationRestIdentityProbeTests.cs` testing the exact property this
fix guarantees, *and* — the part worth calling out specifically — **"User-
confirmed fixed in the editor."** Across everything I've reviewed on this
branch, most validation claims are either automated-test-only or
explicitly caveated as "no Unity execution available." A named human
confirmation in the actual editor is the strongest evidence category this
whole review has seen. Worth noting as a positive example of what
closing-the-loop validation looks like, not just flagging when it's
missing.

**Bundled, also correctly done:** reload-safe cleanup of untracked
generated rig children (`CreatureRig.CaptureGeneratedBoneRoots`) — a
direct, well-reasoned fix for exactly the risk I flagged as `TSK-0167`
several rounds ago (duplicate rig/skinned-preview components after a
domain reload, since the *tracked* object list is non-serialized and goes
empty on reload while the *actual* scene hierarchy survives). The fix
pattern-matches candidate orphaned roots by structure rather than trusting
the (post-reload-empty) tracked list, and only cleans them up after a
build succeeds — preserving the previous valid rig if a rebuild fails,
consistent with the transactional-replacement principle from earlier
rounds of this review.

I did not find a defect in this commit after a careful read of the core
diff. Reporting that plainly, same as any other round's honest result.

## Standing items, unchanged (branch tip hasn't moved since last check)

- Gradient regression in `DensityGrid.TryEstimateGradient` (`dw`'s third
  term, `c011-c001` should be `c011-c010`) — still present, fifth
  consecutive round.
- Task-ID collisions — still 14, same list, `tsk-0156` still malformed.
  The recommended fix (run `Normalize-TaskRecords.ps1`'s repair pass, now
  confirmed unblocked) still hasn't been executed.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| `ResolveAimRotation`'s rest-pose-identity property is algebraically guaranteed, not incidental | Confirmed (derived directly from the `FromToRotation` identity case) |
| The reload-orphan cleanup correctly addresses the `TSK-0167` duplicate-hierarchy risk | Confirmed (read the capture/cleanup logic and its ordering relative to build success/failure) |
| No new defect found in `42aab9b` | Confirmed for the code read; this is the honest result, not an exhaustive guarantee across the full 19-file diff |
| Gradient regression and task-ID collisions remain open | Confirmed (branch tip unchanged, direct recheck) |
