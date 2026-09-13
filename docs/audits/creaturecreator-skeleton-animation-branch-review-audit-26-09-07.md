# CreatureCreator — Skeleton / Animation Audit-Branch Review

**Report ID:** `CC-AUDIT-20260907-BRANCH-9A7E31D2`
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Reviewed HEAD:** `976dd8e6b5aabab77aa6d82f244e8b995213478b`
**Comparison base:** `main` at `83b1cbed767a8f58900ca6a82b06a028c3032ca7`
**Divergence:** 110 commits ahead, 0 behind
**Mode:** Read-only; no repository/task mutations made during this review.

## Assessment

The branch is broadly healthy. The large commit count is not itself a defect and is consistent with one-file-at-a-time tooling. The final tree, not individual commit granularity, is the correct review target.

The architectural direction is coherent: authoritative DNA -> resolved snapshot -> derived skeleton/geometry -> narrow Unity/editor presentation.

## Immediate repair queue for the next round

### P0/P1 — Worker-thread generation crosses into Unity-native appearance evaluation

`CreatureGenerationScheduler.Enqueue` runs `CreatureMeshGenerator.GenerateData` on `Task.Run`. The Burst appearance path ultimately calls `BodyVerticalGradientSampler.EvaluateColor`, which evaluates Unity `Gradient` / `AnimationCurve` state through the adapters.

That violates the intended worker-safe/pure generation boundary and creates thread-affinity/undefined-behavior risk.

**Owner:** `TSK-0165` / `TSK-0095`.
**Next action:** use an immutable plain-data appearance representation/evaluator, or explicitly move Unity-native evaluation behind the main-thread presentation boundary. Do not solve this with a global lock.

### P1 — TSK-0150 mirrored-foot coupling still needs a geometry-level proof

The branch correctly added chain-aware `InfluenceDomain` and parent-chain eligibility. However, `ImplicitSurfaceInfluenceDomainResolver` still chooses the nearest part using `abs(SDF)` and determines mirrored-instance identity by comparing distance to the original and reflected **part origins**.

That is a heuristic, not a proof of which mirrored surface instance generated a welded vertex. Rotated/elongated parts and geometry near the symmetry plane can be misclassified.

The focused resolver tests cover simple synthetic points, but do not establish the complete generated-fox isolation contract.

**Owner:** `TSK-0150`, broader refinement under `TSK-0147`.
**Next action:** inspect actual generated `BoneWeight` data and live posed geometry first. Prefer an exact resolved geometry/domain signal already available in the generation pipeline over another geometric heuristic.

### P1 — Compact-rig / widened-body-weight changes still need generated-creature validation

`TSK-0148` now correctly avoids biped/quadruped topology modes and supports arbitrary limb count/order/type. The newest `976dd8e` change widens compact Body binding radii to cover curvature between dense Body samples and straight compact bone chords.

That is a meaningful deformation-policy change and needs generated-geometry validation, especially to ensure the broader Body influence tube does not create unwanted overlap.

**Owners:** `TSK-0148`, `TSK-0147`, with animation integration gates under `TSK-0077`/`TSK-0132`.

### P2 — RigDebugView fails to draw a leaf `HasSegment` bone such as `body_tail`

The compact Body model represents `body_tail` as a real segment with a start position at the pelvis and a distinct `EndPosition`. `RigDebugView` currently draws parent-position -> bone-position links.

For a leaf segment that has no child node at its endpoint, this produces a zero-length line, so the tail segment is not actually visualized.

**Owner:** `TSK-0149`.
**Next action:** when `BoneSnapshot.HasSegment` is true, draw `bone.position -> boneData.EndPosition`; reserve parent-to-node links for point/terminal attachment nodes. Avoid double-drawing ordinary articulated segments.

### P2 — `GeneratedCreatureData.Definition` still permits the resolved-boundary to be reopened

The generated-data object contains both `Definition` and `Snapshot`, but `CreatureMeshGenerator.AppendMeshAssetItems` re-infers a skeleton from `data.Definition` and resolves concrete `CreaturePart` objects again.

That contradicts the stated “resolve once, consume many” boundary and means assembly can depend on mutable/raw definition state after `GenerateData`.

**Owner:** existing `TSK-0095` / output-boundary work.
**Next action:** either make the carried definition explicitly detached/immutable-by-convention or move mesh-asset weighting to resolved-part metadata and remove this downstream raw-DNA dependency. Do not create another resolver.

### P2 — Remaining JSON type/range validation defects

`JsonDnaSerializer` still directly casts numeric doubles to `int` for fields such as `schemaVersion` and `noiseSeed`; non-integral values can therefore be truncated. `RequireEnum` / `ReadOptionalEnum` also accept enum-numeric strings without checking that the resulting value is defined. Present malformed `meshGeometry.attachment` values are silently ignored rather than rejected.

**Owner:** `TSK-0157` under `TSK-0136`.

### P2 — JSON writer still emits invalid raw control characters

`CanonicalJsonWriter.Escape` handles quote, backslash, LF, CR and TAB but passes other U+0000..U+001F characters through unchanged. The reader rejects those raw characters, so the reader/writer contract remains asymmetric.

**Owner:** `TSK-0164` under `TSK-0136`.

### P2 — `BodyFrame.Default` is inconsistent with its documented handedness

The documentation defines `Binormal = Cross(Tangent, Normal)`, but the default uses +Z, +Y, +X. Since +Z x +Y = -X, the fallback frame is left-handed.

**Owner:** `TSK-0163` under `TSK-0024`.

## Already repaired on the branch

Do not reopen these as new findings:

- invalid `CapsuleAxis` is rejected by canonicalization (`TSK-0161`);
- SDF evaluator temporary buffer disposal is now exception-safe (`TSK-0162`);
- derived-output collection views were hardened (`TSK-0158`);
- `SkeletonSnapshot` enforces one root and structural compatibility;
- the old `RemoveAt(0)` snapshot pending queue was replaced (`TSK-0155`);
- `PosedSkeleton` rejects non-finite updates;
- four-influence enforcement exists in the Unity binding conversion;
- old name/prefix preview ownership is superseded;
- compact Body rig no longer derives long-term topology from dense sample count;
- mirrored domains now distinguish mirrored IDs.

## Task bookkeeping for the next round

Do not mark tasks Done merely because source code exists; many have explicit Unity gates.

Keep open until their stated evidence exists:

- `TSK-0150`, `TSK-0147`, `TSK-0148`, `TSK-0149`, `TSK-0158`: implementation/validation gaps remain.
- `TSK-0154`, `TSK-0155`: implementation is present; status/evidence should be reconciled after focused tests.
- `TSK-0161`, `TSK-0162`: implementation appears landed but task records remain stale; reconcile status/evidence rather than leaving them misleadingly Backlog.
- `TSK-0157`, `TSK-0163`, `TSK-0164`, `TSK-0165`: actual implementation still needed.
- `TSK-0156`: real architectural debt, but not an immediate blocker for the next animation repair pass.

## Recommended next-round order

1. Establish/reconnect reliable Unity execution and inspect the actual fox deformation.
2. Resolve/falsify `TSK-0150` using live generated weights and pose isolation before adding further heuristics.
3. Repair the worker-thread Unity-native appearance boundary (`TSK-0165`).
4. Fix the missing leaf-segment visualization in `TSK-0149`.
5. Apply the small deterministic parser/handedness fixes (`TSK-0157`, `TSK-0163`, `TSK-0164`).
6. Re-run focused + full relevant runtime/editor suites and then reconcile MemorySmith task evidence/statuses.
7. Leave the broader `TSK-0156` mutable-skeleton migration for a deliberate separate slice.

## Conclusion

The branch is worth continuing. I do not recommend a broad rewrite or discarding the accumulated commits.

The next round should be a repair-and-validation pass, with the fox's actual generated deformation as the first discriminator. The compact anatomical rig is moving in the right direction; the main risk now is layering plausible heuristics without end-to-end proof.
