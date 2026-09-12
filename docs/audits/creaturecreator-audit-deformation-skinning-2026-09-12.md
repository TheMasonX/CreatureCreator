# CreatureCreator — Deformation & Skinning Audit

**Report ID:** `CC-AUDIT-SKIN-20260912-1D83F0A4`
**Suite:** `CC-AUDIT-SUITE-20260912-9F42C6D1`
**Branch base audited:** `17643c422e156a5b6e1dee4de754a1765501c62a`
**Primary sources:** `ImplicitSurfaceWeightAuthoring.cs`, `LinearBlendSkinning.cs`, `SkinnedMeshBindingBuilder.cs`, `CreatureSkinnedMeshRenderer.cs`, `CreatureRig.cs`, `SkeletonSnapshot.cs`, TSK-0130/0131/0132/0203, ADR-003
**Unity execution:** unavailable

## Executive assessment

The binding stack is considerably more disciplined than its early implementation. The rest-space model, deterministic bone indices, four-influence cap, segment-distance weighting, mirror metadata, transactional renderer bind, and explicit object ownership are all good foundations.

The main remaining risk is **contract mismatch at the edges of an otherwise solid mathematical core**. The deformation code can be locally correct while the renderer still shows artifacts because the system has not yet proven bounds, mesh metadata preservation, morphology-radius authority, mirror pose semantics, and end-to-end generated-creature behavior in Unity.

## Findings

### SK-01 — Morphology radius has inconsistent invalid-input semantics

**Severity:** P2  
**Confidence:** 98%  
**Owner:** TSK-0132 / binding follow-up

`BuildSegmentInfluences` uses `DefaultInfluenceRadius` when the supplied per-bone value is missing or not positive. In that helper, `NaN` therefore falls back while positive infinity passes through; `BuildBindingInfluences` additionally checks finiteness for point influences; `Author` later rejects non-finite radii.

This creates three different policies in one conceptual API:

- missing radius → default;
- invalid radius → sometimes default;
- invalid radius → sometimes deferred error.

The binding contract should be deterministic and fail-fast. A supplied value that is present but non-finite or non-positive should be rejected, not silently replaced. Missing values can use the documented default if that is still desired.

### SK-02 — `DefaultInfluenceRadius` is an architectural escape hatch

**Severity:** P2  
**Confidence:** 96%  
**Owner:** TSK-0132

The core authoring path explicitly says morphology should supply real radii, but it also exposes a skeleton-only fallback of `0.5`.

That is useful for isolated unit fixtures but dangerous if a production caller accidentally omits morphology. The result can be valid-looking but anatomically wrong weights.

Recommendation: split the APIs into:

- production `BuildBindingInfluences(snapshot, requiredRadiuses)` that rejects missing morphology data;
- a clearly named `BuildSyntheticTestInfluences(...)` helper for tests.

Do not let “convenient defaults” mask missing production inputs.

### SK-03 — Weight authoring is O(V × S)

**Severity:** P2  
**Confidence:** 99%  
**Owner:** TSK-0132 / performance follow-up

`Author` measures every rest vertex against every eligible segment, then sorts candidates for every vertex.

For a generated welded surface, `S` is usually modest, so this is not necessarily the primary bottleneck. However, the algorithmic shape is still worth preserving deliberately because the system already has expensive voxel and appearance stages.

The key optimization opportunity is spatial partitioning around segments, not micro-optimizing `Mathf.Pow` or replacing a `List` with a different container.

Acceptance should profile realistic vertex/segment counts before adding complexity. If S is in the low tens and V is tens of thousands, the current design may be adequate; if the geometry grows materially, a grid/BVH/hash over segment bounds becomes the right next step.

### SK-04 — Candidate sorting creates avoidable per-vertex work

**Severity:** P3/P2  
**Confidence:** 97%  
**Owner:** TSK-0132

The code computes all candidate weights, then sorts all candidates even though only the top four are needed.

A bounded top-four selection can be made linear in candidate count and avoids sorting irrelevant candidates. This is not urgent while generation is dominated by larger costs, but it is a clean optimization once profiling shows weight authoring is significant.

Do not introduce a heap or generalized selection framework prematurely; four fixed slots are enough.

### SK-05 — `WeightFor` is less defensive than `Author`

**Severity:** P3  
**Confidence:** 95%  
**Owner:** TSK-0132

`Author` validates finite vertices and segment data. Public `WeightFor` does not perform the same validation before distance/falloff calculation.

Because `WeightFor` is exposed primarily for direct testing, this is not a production vulnerability. It is nevertheless an API inconsistency: a public helper in the same contract family can return NaN/Infinity where the main authoring path rejects it.

Either make it explicitly a “mathematical primitive; caller must validate” API or give it the same finite/positive-radius validation and centralize the implementation so the two paths cannot drift.

### SK-06 — Vertex-domain membership uses string IDs in the inner weighting loop

**Severity:** P2  
**Confidence:** 94%  
**Owner:** TSK-0132 / performance follow-up

`InfluenceDomain.Allows()` performs ordinal string comparisons while `Author` loops over every vertex and candidate segment.

This is correct but creates an avoidable hot-path allocation/CPU boundary at generation time. Domain resolution should occur once and be represented by integer domain indices or compact bitsets where practical.

This is not a runtime-animation issue; it is generation-time scale hygiene.

### SK-07 — Mesh copy has a silent metadata-loss contract

**Severity:** P2  
**Confidence:** 95%  
**Owner:** TSK-0132 / mesh adapter follow-up

`BuildSkinningMeshCopy` preserves vertices, normals, tangents, UV0-UV2, UV3, colors, triangles, bindposes, and bone weights. It does not preserve later UV channels, blend shapes, or other less-common mesh metadata.

For the current procedural mesh output, this may be harmless. The risk comes from the class accepting an arbitrary `Mesh sourceMesh`: the API shape suggests generic preservation but the implementation is actually a restricted mesh contract.

Choose one explicitly:

1. document and validate the supported channel contract; or
2. copy every channel/features that the pipeline promises to support.

Do not leave the current silent behavior as an accidental “generic mesh” promise.

### SK-08 — Material/submesh cardinality behavior is under-specified

**Severity:** P2  
**Confidence:** 93%  
**Owner:** TSK-0132

The renderer creates one material slot per source submesh but fills missing supplied materials with `null` and does not fail binding.

For a procedural welded surface this may be acceptable. For arbitrary mesh assets, silently producing unmaterialized submeshes can create a presentation bug that looks like geometry corruption.

Decide whether materials are:

- optional and defaultable;
- required for every submesh; or
- resolved separately from the binding layer.

Prefer not to make `Bind` own material policy if a material palette/presentation resolver already owns it elsewhere.

### SK-09 — Animated bounds are still an unresolved renderer contract

**Severity:** P1/P2  
**Confidence:** 99%  
**Owner:** TSK-0203

The mesh copy calls `RecalculateBounds()` at bind time, which only establishes rest-mesh bounds. The current task correctly leaves animated visibility/culling as an explicit Unity-gated requirement.

This is a functional correctness issue, not a polish issue. A valid posed creature can be culled if its bounds remain too small or are not configured for animation.

The task should validate real exaggerated poses with:

- camera-facing movement outside rest bounds;
- offscreen-to-onscreen traversal;
- extreme limb extension;
- mirrored extension;
- renderer enabled/disabled transitions.

Only then choose between baked expanded bounds, runtime bounds updates, `updateWhenOffscreen`, or another policy.

### SK-10 — End-to-end deformation cannot be judged from the pure LBS oracle alone

**Severity:** P1  
**Confidence:** 97%  
**Owner:** TSK-0132 + TSK-0172

`LinearBlendSkinning` is an excellent deterministic math oracle. But Unity's actual SMR path adds transform hierarchy, bindposes, bounds, renderer state, mesh layout, and Unity's own skinning implementation.

Therefore:

```text
pure LBS passes
```

is necessary but insufficient evidence for:

```text
actual generated creature deforms correctly in Unity
```

The acceptance fixture must compare the pure oracle and Unity presentation on the same known input and at least one exaggerated pose.

### SK-11 — Influence domains are a strong mechanism but can hide authoring topology errors

**Severity:** P2  
**Confidence:** 94%  
**Owner:** TSK-0150 / TSK-0131

The current domain model correctly prevents unrelated foot/leg regions from sharing arbitrary influences. The remaining risk is the inverse: a too-restrictive domain can create a vertex with no eligible influence and fail generation.

That fail-fast behavior is good. What is missing is a diagnostic that identifies the domain, vertex, nearest excluded candidate, and source part. Without this, artists see a generic generation failure instead of an actionable topology explanation.

### SK-12 — Mirror parity needs animation-space validation, not just rest-space validation

**Severity:** P2  
**Confidence:** 94%  
**Owner:** animation/deformation integration

The rest skeleton and binding code explicitly carry mirrored state, and historical rest-space mirror regressions have been added. That does not prove runtime mirrored animation.

A mirror test should check a nontrivial pose, especially rotation, because reflection and quaternion semantics can differ even when mirrored positions are perfect.

## High-value test matrix

| Test | Pure | Unity | Required |
|---|---:|---:|---:|
| Rest-pose vertex round trip | Yes | Yes | P1 |
| Four-influence normalization | Yes | No | P1 |
| Same-count wrong skeleton rejected | No | Yes | P1 |
| Exaggerated pose remains visible | No | Yes | P1 |
| Mirrored bend deforms symmetrically | Yes | Yes | P1 |
| Foot/leg domain isolation | Yes | Yes | P1 |
| Terminal rotation with unchanged endpoint | Yes | Yes | P1 |
| Nonstandard UV4+/blendshape mesh policy | No | Yes | P2 |

## Exclusions

Do not reopen the historical duplicate mirror-math finding. Current deformation code uses the existing mirrored skeleton metadata and the project-wide mirror utilities elsewhere; the remaining concern is semantic runtime validation, not another copy of a matrix constant.

## Conclusion

The binding layer is ready for focused hardening rather than redesign. The highest value is to make malformed morphology inputs fail consistently, remove production defaults that can mask missing radius data, explicitly narrow or expand the mesh-copy contract, and finish Unity bounds/deformation evidence. Optimization should remain measurement-driven.
