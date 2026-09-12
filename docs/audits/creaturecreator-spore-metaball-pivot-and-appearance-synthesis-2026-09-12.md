# CreatureCreator — Spore-Informed Synthesis: Metaball Pivot, LOD, Weights, and Appearance

**Report ID:** `CC-SYNTH-SPORE-PIVOT-20260912-A7E4D1`
**Suite:** `CC-AUDIT-SUITE-20260912-9F42C6D1`
**Mode:** full reconciliation of supplied external research into accepted findings and MemorySmith tasks
**Branch base:** `ba680631bc0f3afe7574512a1967d61d8ce6d653`
**Unity execution:** not required and not performed. This report changes no code.
**Code changes in this capture:** none.

---

## 1. User mandate

The user supplied this requirement. It is STRICT. Do not relax or re-scope it.

> I want to go to a metaball + attachable mesh system (no other sdfs like cubes
> or ellipsoids). Non-mesh parts will be either a single metaball or a chain of
> them for now. This will allow for things like spikes or fingers too. I want to
> streamline and simplify.

> The triplanar shader we have doesn't work with animation, and we really need
> the painting texture mapping tech.

Supporting context, also STRICT in intent:

> fix our somewhat slow generation for the LOD and the ever problematic
> animation weights ... I'm willing to pivot systems and make major changes since
> the skeleton definitions and editing are okay, but the mesh generation has
> always left a lot to be desired.

Two accepted system pivots follow: the **field model** (metaball plus attached
mesh) and the **appearance model** (UV charting plus procedural paint).

---

## 2. Executive summary

The supplied research rounds are, on the whole, accurate about Spore's published
record. Two earlier claims were wrong and are corrected here. One earlier framing
was wrong in a way that matters: the research targeted the animation and IK
layer, but the reported symptoms are a weighting layer defect.

Three accepted findings drive the pivot.

- **F-01.** Spore generated skin bone weights from *which body part generated
  which metaball* (`S01`). CreatureCreator infers weights from *nearest resolved
  part SDF* plus *closest point on bone segment*. At a joint these two disagree,
  and the geometric answer can be anatomically wrong. This is the structural
  cause of the elbow, foot, and tail symptoms.
- **F-02.** CreatureCreator's field is an SDF operation tree evaluated with
  polynomial smooth-min. Smooth-min makes influence non-local. That forced a
  conservative "potential influence envelope" design (`CC-099`) and an ellipsoid
  culling exception. A compact-support metaball sum has exactly bounded
  influence, so that machinery disappears and per-primitive culling becomes
  exact. The same representation carries per-primitive attribution for free.
- **F-03.** Appearance is baked per-vertex color from object-space triplanar
  noise, plus a `Triplanar` node in `Assets/Shaders/VertexLit.shadergraph`. Both
  are locked to rest-space object coordinates. Under skinning the pattern
  stretches with the deformation. Baked vertex colors cannot express the paint
  the project needs. Spore used a fast UV charter plus procedural paint into the
  atlas (`S01`, `S04`).

F-01 and F-02 share one root cause and one fix. A metaball field with compact
support gives both cheap local evaluation and the source attribution that Spore
used for weights. The metaball pivot is therefore not only a simplification. It
is the enabling step for the weighting fix.

---

## 3. Scope and fixed point

- **In scope:** reconciliation of the supplied research (`S01`–`S11`), verification
  of claims against source, the target field model, level-of-detail and
  generation performance, weighting from provenance, and the appearance pivot.
- **Out of scope:** skeleton inference, DNA authoring, the editor gesture model,
  and IK. The user states these are acceptable.
- **Fixed point:** `ba680631bc0f3afe7574512a1967d61d8ce6d653`.
- **Excluded:** production code changes. Implementation is owned by the tasks
  named in section 9.

---

## 4. Result counts

| Result | Count |
| --- | --- |
| Sources inventoried | 12 |
| Sources directly read | 7 |
| Sources unavailable or 403/404 | 5 |
| Accepted findings | 12 |
| Corrections to prior research | 3 |
| Rejected recommendations | 4 |
| Duplicate mechanisms merged | 2 |
| Tasks created | 4 |
| Tasks extended or commented | 5 |

---

## 5. Accepted findings

Severity and confidence are independent. Confidence describes evidence quality.

### F-01 — Weight provenance is the correct model, and it is not published as a solved algorithm

**Severity:** P1 **Confidence:** 99% **Result:** Confirmed **Owner:** TSK-0222

`S01` states: "We generate bone weights for the vertices based on which body
parts generated which metaballs." `S01` also states the limits: spine segments do
not generate smooth weights, fat torsos shear, and torso-attached parts were
punted. The public record therefore contains the *mechanism* and the *known
failure regions*, but not a complete algorithm. The honest target is structurally
better weighting with documented failure regions, not universal correctness.

Verified in source (`S12`):

- `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceInfluenceDomainResolver.cs`
  resolves each vertex domain from the nearest resolved part SDF.
- `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs`
  ranks candidates by closest point on segment with `RadiusScale` falloff and a
  four-influence cap.

Both are geometric inference. Neither is source attribution.

### F-02 — A compact-support metaball sum removes the envelope machinery and yields attribution

**Severity:** P1 **Confidence:** 97% **Result:** Net-new **Owner:** TSK-0223

`S01` gives Spore's field function as a fourth-order polynomial in squared
distance, with zero contribution beyond the metaball radius, plus a per-metaball
scale factor. The field is additive.

CreatureCreator instead compiles a validated `CreatureDefinition` into a portable
`SdfProgram` and evaluates an operation tree with `SmoothMinMath`. Because
smooth-min propagates influence beyond each primitive bound, per-op culling needs
a conservative inflated bound. `CC-099` documents that design, and it documents a
regression where the envelope path was disabled by one ellipsoid part and
sampling cost rose from 121 ms to 614–746 ms.

A compact-support sum has three properties the SDF tree does not.

1. Primitive influence is exactly a ball of radius `R`.
2. Per-primitive culling is exact, so no envelope proof is required.
3. Per-primitive contribution is available at every sample, so source attribution
   is a by-product rather than an added pass.

### F-03 — Triplanar appearance cannot survive skinning, and vertex color cannot express paint

**Severity:** P1 **Confidence:** 98% **Result:** Confirmed **Owner:** TSK-0226

Verified in source (`S12`):

- `Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs` bakes one `Color` per
  vertex from part `BaseColor` modulated by `TriplanarNoise.Evaluate`.
- `Assets/Scripts/Runtime/Appearance/TriplanarNoise.cs` evaluates in object space
  from position and normal.
- `Assets/Shaders/VertexLit.shadergraph` contains `TriplanarNode` and
  `PositionNode` nodes.

Object-space mapping is fixed to the rest pose. Skinning moves vertices and
rotates normals. The pattern therefore stretches and the normal-blend weights
change during animation. Spore solved the same "the mesh changes shape, so UVs
must be regenerated" problem with a fast charter plus procedural paint (`S01`,
`S04`).

### F-04 — The Spore charter is documented well enough to reimplement

**Severity:** P2 **Confidence:** 96% **Result:** Net-new **Owner:** TSK-0226

`S01` documents the algorithm precisely.

1. Start on an uncharted triangle.
2. Flood outward to neighbours that face approximately the same cardinal
   direction (down `+x`, `−x`, `+y`, `−y`, `+z`, or `−z`).
3. Project the group flat onto the plane of that axis. This gives bounded
   distortion.
4. Repeat until all triangles are charted.
5. Sort charts by bounding-box size, then pack the boxes in 2D texture space in a
   zig-zag manner.

`S01` reports about 10 ms on minimum specification, with wasted texels and many
seams as accepted costs. `S04` confirms the paint side: Andrew Willmott converted
brush 3D positions to texture positions, so hundreds of brushes per second ran on
the graphics card.

### F-05 — Spore's morphology vocabulary is landmark-relative, not part-index-relative

**Severity:** P2 **Confidence:** 94% **Result:** Net-new **Owner:** TSK-0226

`S04` documents Henry Goffin's extension: particles could spawn "just on the spine,
or halfway up the legs" and crawl "towards the head or the tail, or towards the
belly or along the arms". Spore reasoned about creature regions, not about bone
indices. CreatureCreator already has the raw material for this vocabulary in
`PartType` and in normalized limb arc length. A shared region vocabulary should
serve appearance, weighting diagnostics, and attachment logic.

### F-06 — Interactive cost model: every request runs, so a drag pays for work that cannot be shown

**Severity:** P1 **Confidence:** 99% **Result:** Corroboration (already owned)

`docs/audits/creaturecreator-audit-generation-performance-lifecycle-2026-09-12.md`
finding GP-01 reports that the scheduler launches every request immediately and
rejects stale results only after completion. Ten slider frames produce ten
generations. The fix is a bounded scheduler with one running item and one
replaceable pending item, or cancellation. This is owned by `TSK-0104`. This
synthesis does not create new scope here.

### F-07 — Field sampling is the dominant generation cost, and the cause is operation count

**Severity:** P1 **Confidence:** 95% **Result:** Corroboration

After the Slice A–D work, editor generation measured 216.6 ms at 96³ with
`FieldSampling` at 121.4 ms, or 56% of the total. Only 13.3% of grid corners lie
inside the root bound, and each of those pays a full scan over about 250
operations. Representative part programs were 62, 65, and 107 operations.
`TSK-0008` owns this. The metaball pivot changes the cost model rather than
tuning it, so `TSK-0223` becomes the primary owner and `TSK-0008` keeps the
measurement gate.

### F-08 — Provenance weighted skinning is the target model, and geometry must choose only inside an allowed set

**Severity:** P1 **Confidence:** 96% **Result:** Net-new **Owner:** TSK-0224

The target model:

```text
raw primitive provenance
  -> aggregate by anatomical part
  -> enforce domain eligibility from provenance membership
  -> apply joint-local transfer profile
  -> keep top four
  -> normalize
```

Distance chooses *which bone inside an allowed chain*. Provenance chooses *which
chain*. This is the structural change that the current model cannot make.

### F-09 — Attribution is currently discarded at extraction and welding

**Severity:** P1 **Confidence:** 95% **Result:** Net-new **Owner:** TSK-0224

Verified in source (`S12`):

- `GeneratedCreature` exposes one geometry item for the whole implicit surface,
  with `ImplicitSurfaceSourceId` as its only identity.
- `MarchingCubesExtractor` welds vertices through `GridVertexOwnership`, which
  keys ownership by corner index and by axis-edge index.
- `MeshExtractionResult` stores positions, normals, and triangles only.

The welding key is already deterministic and already a grid identity. A parallel
per-corner and per-edge attribution array can therefore be added without changing
weld determinism. That is the narrowest insertion point.

### F-10 — Total generation falls with grid resolution, so a coarse interactive tier is a direct win

**Severity:** P1 **Confidence:** 93% **Result:** Net-new **Owner:** TSK-0225

Recorded reference baselines from `GenerateDataBenchmark` are about 97.5 ms at
10 voxels per unit and about 215.7 ms at 16 voxels per unit on the same fixture,
on Unity 6000.5.9f1. These are environment-sensitive and must be re-measured, but
they show that resolution is a first-order lever. No level-of-detail system
exists today; no source file contains one.

### F-11 — A dropped metaball can be proven invisible at a given resolution

**Severity:** P2 **Confidence:** 90% **Result:** Net-new **Owner:** TSK-0225

Compact support permits an exact local visibility argument. A metaball can be
dropped at a given grid resolution when its maximum possible contribution cannot
raise the field to the surface threshold anywhere it is evaluated. This turns
level-of-detail from an art decision into a testable rule. The check is: extract
at one resolution with and without the dropped set, and assert an identical mesh.

Confidence is 90% because the conservative bound must be derived for the actual
summation form before the rule is trustworthy.

### F-12 — Polygon-quality work is a post-extraction pass, not a mesher replacement

**Severity:** P3 **Confidence:** 90% **Result:** Correction **Owner:** TSK-0065

Both research rounds corrected this claim already, and the correction is right.
`S01` uses Compact Isocontours for triangle quality, and describes it as
"trivial to implement". The technique displaces vertices after contouring; it is
not a replacement for the extractor. `S08` could not be retrieved, so the
mechanism description rests on `S01` and on the prior repository audit. Treat this
as an optional quality pass and not as part of the pivot.

---

## 6. Corrections to prior research

### C-01 — `daniellochner/creature` is not a code reference

Verified (`S05`): the repository contains only `LICENSE.md` and `README.md`. It is
GPL-3.0. The README points to `daniellochner/creature-creator-game`, which
returned HTTP 404 on fetch (`S06`). Classification: historical pointer only. Do
not cite it as an implementation reference.

### C-02 — The earlier animation description was too expansive

Verified (`S01`, `S02`): Spore recorded animator-authored, morphology-independent
motion, resolved it into pose goals, and solved those goals with constrained IK.
The sources do not support a claim that the runtime derived balance equations or
physically analysed arbitrary creatures. Both lecture and paper titles are
correct. The corrected summary is:

```text
authored semantic motion
  -> semantic body-part queries
  -> morphology-specific pose goals
  -> constrained IK
```

### C-03 — The weighting problem is a deformation-layer problem, not an IK problem

The supplied research emphasised semantic animation and IK. The reported symptoms
are vertex-weight symptoms. Better IK cannot repair vertices bound to the wrong
bones. This correction changes the owner from the animation track to `TSK-0222`
and `TSK-0224`.

---

## 7. Rejected recommendations

| Recommendation | Source | Reason for rejection |
| --- | --- | --- |
| Replace the extractor with Surface Nets or Dual Contouring | prior round | The project already owns a hand-derived Marching Cubes path with an Asymptotic Decider, a topology validator, and tests. Replacement is not required by any accepted finding. |
| Re-plan spine nodes, capability tags, deterministic JSON, FABRIK, and triplanar appearance from scratch | prior round | All exist and the user states skeleton and editing are acceptable. Re-planning is duplicate scope. |
| Adopt general automatic skinning such as heat diffusion or learned rigging | prior round | Build-time cost, determinism, and non-compliance with authored anatomy. Provenance is cheaper, deterministic, and matches Spore. |
| Keep baked triplanar vertex color and add paint later | implied option | Rejected by mandate. Triplanar cannot survive animation, and vertex color cannot express a painted atlas. |

Merged duplicates: "remove non-spherical metaball primitives" and "remove
capsule, box, and ellipsoid from the SDF vocabulary" are one mechanism, owned by
`TSK-0223`. "Provenance through extraction" and "source-attributed weights" are
one mechanism, owned by `TSK-0224`.

---

## 8. Target design

### 8.1 Field model

Define the creature surface with a compact-support metaball sum, in the project
sign convention. Negative is inside and positive is outside.

```text
F(p) = isoLevel - sum_i ( s_i * B( |p - c_i| / R_i ) )

B(q) = ( q*q - 1 )^4   for q < 1
B(q) = 0               for q >= 1
```

This matches the function documented in `S01`, including the scale factor `s_i`
that `S01` calls goopiness. The subtraction restores negative-inside, so the
existing extractor, contour resolver, and gradient winding remain valid.

Design constraints:

- `s_i > 0` and `R_i > 0`. Reject non-finite values at the DNA boundary.
- Contribution order is fixed by the canonical part order, so the sum is
  deterministic.
- Blob spacing along a chain comes from `ResolvedLimb` arc length, as today
  (`ADR-007`). `TSK-0041` owns the blend radius as an authored value.

### 8.2 Primitive vocabulary

The implicit vocabulary becomes `Sphere` only. `Capsule`, `Box`, and `Ellipsoid`
leave DNA, serialization, canonicalization, validation, editor authoring, the SDF
compiler, culling, and tests. Authored geometry stays available as a mesh asset
attachment through `MeshGeometry` and `MeshAssetKey`, which is the Rigblock
analogue.

Non-mesh parts are a single metaball or a chain of metaballs. A single metaball
covers spikes, horn bases, and eye stubs. A chain covers fingers, limb segments,
and tails. This is `TSK-0223`.

### 8.3 Simplifications obtained

| Removed | Reason |
| --- | --- |
| `CC-099` potential influence envelope | Replaced by exact compact-support bounds. |
| Ellipsoid-specific culling exception | No ellipsoids remain. |
| Non-uniform transform distance approximation | No non-uniform primitives remain. |
| Capsule, box, and ellipsoid parameters and validation | No such shapes remain. |
| Root-AABB early-exit fragility | Per-blob bounds are exact and independent. |

### 8.4 Cost and risk register

| Risk | Severity | Mitigation | Falsifying check |
| --- | --- | --- | --- |
| Additive union merges nearby limbs into webbing | P2 | Accept as Spore did, or add per-chain scale and later subtraction | Extract a walk pose and assert limb separation at a chosen gap |
| Existing `.json` creatures use capsule and ellipsoid | P1 | One-way import migration or re-author before removal | Load every file in `Assets/Creatures/` and assert no unknown shape |
| Field is no longer a distance field | P2 | Audit consumers that read distance semantics | Grep for distance reads and remove or replace each |
| Surface quality changes near unions | P2 | Keep the compact-isocontour vertex pass as an optional follow-up | Minimum triangle angle before and after |
| Blob count grows with limb count | P1 | Exact per-blob bucketing plus the drop rule | Measured sample count per corner, not guesswork |

---

## 9. Weighting from provenance

### 9.1 Payload

```text
PrimitiveSource
  primitiveId
  anatomicalPartId
  boneId
  chainId
  symmetryGroupId
  regionRole
  isMirrored

ContributionSample
  totalField
  sourceIds[K]
  sourceContributions[K]

VertexAttribution
  contributions by source
  contributions by anatomical part
  contributions by bone
  ambiguityScore
```

`K` should start at four. Four matches the current influence cap and keeps the
payload small.

### 9.2 Pipeline

1. Accumulate per-corner top-K contributions during field sampling.
2. Interpolate contributions with the same parameter used for the vertex
   position on the grid edge.
3. Merge contributions by source when welding, instead of first-writer-wins.
4. Aggregate by bone.
5. Reject bones whose chain is not allowed for the vertex, using provenance
   membership rather than geometric distance.
6. Apply the joint-local transfer profile.
7. Keep the top four and normalize.

### 9.3 Joint-local transfer profile

Provenance is not automatically an attractive weight. A thick parent blob can
dominate a child near a joint. The transfer profile handles this explicitly: for
a vertex whose dominant bone is `B`, and whose position falls inside the blend
band around the `B` to `parent(B)` joint, transfer a bounded fraction to
`parent(B)`. Keep the profile in `InfluenceWeightingPolicy` so it stays tunable
and testable.

### 9.4 Per-symptom hypotheses and falsifying observations

| Symptom | Candidate cause under the target model | Falsifying observation |
| --- | --- | --- |
| Elbow moves the whole arm | Forearm blobs contribute high on the upper arm, and the domain gate admits the whole chain | After the fix, an upper-arm vertex has zero forearm contribution unless a forearm blob contributed there |
| Foot twists and compresses the leg | Foot blobs are dense and near the ankle, so geometric ranking assigns them to shank vertices | Foot rotation leaves shank vertex positions bit-identical in a fixed-pose fixture |
| Tail moves in disjointed sections | Tail blobs are sparse, so some vertices receive no tail contribution and fall back to a neighbour chain | Every tail vertex's dominant source is a tail blob |
| Knee is acceptable | Blob spacing and radii already produce good transfer | Regression test must hold the knee unchanged |

The last row is the control. A fix that changes the knee as much as the elbow is
not evidence of a correct model.

### 9.5 Rejected weighting alternatives

| Alternative | Reason for rejection |
| --- | --- |
| Tune radius and falloff constants further | Same heuristic class that already fails at junctions. |
| Enlarge the influence cap above four | Raises runtime cost and does not correct chain selection. |
| Geodesic or heat-diffusion weights | Build cost, determinism risk, and no authored anatomy. |
| Weight painting by hand | The editor must generate weights for arbitrary morphologies. |

---

## 10. Level of detail and generation plan

### 10.1 Tiers

| Tier | Trigger | Grid | Mesh | Weights | Atlas | Collider |
| --- | --- | --- | --- | --- | --- | --- |
| Interactive | During a drag or gesture | Coarse | Yes | No rebind | No rebake | No |
| Committed | Gesture end or explicit apply | Full | Yes | Yes | Yes | Yes, deferred |
| Runtime L1/L2 | Camera distance | Derived from committed | Decimated | Reduced bone set | Preserved | Simple |

### 10.2 Levers, in measured order

1. **Exact per-blob bucketing.** Sample only blobs whose support contains the
   corner. This attacks the measured 56% `FieldSampling` share.
2. **Bounded scheduler with coalescing.** `TSK-0104` finding GP-01. Removes
   generations that cannot be presented.
3. **Coarse interactive grid.** About 97.5 ms at 10 voxels per unit against about
   215.7 ms at 16 on one fixture.
4. **Provable metaball drop at distance.** Section F-11.
5. **Derive runtime LOD by decimation.** Preserves UVs. Re-extraction would
   require a new charter per level.

### 10.3 Acceptance gates

- Publish a before and after sample-per-corner count and total time on the fixed
  `GenerateDataBenchmark` fixture at 10 and 16 voxels per unit.
- Assert bit-identical extraction with per-blob bucketing enabled and disabled.
- Assert bit-identical extraction with the provably dropped blob set removed.
- Assert the same committed mesh from two identical generation requests.

---

## 11. Appearance pivot

### 11.1 Charter

Implement the charting algorithm from F-04, with one change for determinism.
Spore started on a random uncharted triangle. CreatureCreator must start on the
lowest canonical triangle index, so that two identical definitions chart
identically.

### 11.2 Paint

Paint into the atlas with morphology-relative rules, using the region vocabulary
from F-05. CreatureCreator already exposes `PartType` and normalized limb arc
length, which cover spine, limb, proximal, and distal. Dorsal and ventral need a
body-frame sign test, which the body frame resolver already provides.

Support at minimum an albedo channel. Add specular, gloss, and normal channels
only when a task requires them. Dilate the atlas to prevent bilinear and mipmap
seams, as `S01` documents.

### 11.3 Consequences

- Appearance output changes from per-vertex color to an atlas texture plus a
  material. `TSK-0064` and `ADR-003` already describe material ownership.
- Regeneration cost becomes mesh plus charter plus paint. The charter must be fast
  enough to run on commit, not on drag.
- `TriplanarNoise` stops driving surface mapping. Keep it only as an optional
  pattern source if a task needs it.
- Runtime LOD must decimate the charted mesh so UVs survive.
- `BuildSkinningMeshCopy` already preserves UV0 to UV2, so the binding path
  already carries the channel. `docs/audits/creaturecreator-audit-deformation-skinning-2026-09-12.md`
  finding SK-07 asks for that contract to be declared explicitly.

### 11.4 Acceptance gates

- A material regression test asserts that no object-space or world-space position
  node feeds texture sampling.
- A deformation test animates one limb and asserts the texture coordinate per
  vertex is unchanged while the vertex position changes.
- A determinism test charts one definition twice and asserts identical UV
  assignment and identical atlas layout.

---

## 12. Standards assessment

Complies with the repository invariants.

- `CreatureDefinition` stays authoritative. The metaball set is derived.
- Negative-inside and positive-outside signs are preserved by the subtraction in
  section 8.1.
- Symmetry stays stored once. A mirrored blob receives the mirror transform at
  generation, as today.
- Runtime code gains no scene or editor dependency. The field is plain blittable
  data.
- One derivation path is preserved. `ResolvedLimb` and `ResolvedBody` still own
  morphology. The metaball set replaces the SDF operation tree as the field
  representation, not as a second morphology source.

Two documented simplifications are deliberately replaced, and the user requested
both: non-uniform SDF scaling, and the ellipsoid culling exception. Record this
in the affected ADRs when `TSK-0223` lands.

## 13. Specification assessment

| Mandate clause | Covered by |
| --- | --- |
| Metaball plus attachable mesh | `TSK-0223`, sections 8.1 and 8.2 |
| No cube or ellipsoid SDFs | `TSK-0223`, section 8.2 |
| Non-mesh part is one metaball or a chain | `TSK-0223`, section 8.2 |
| Spikes and fingers possible | `TSK-0223`, section 8.2 |
| Streamline and simplify | `TSK-0223`, section 8.3 |
| Slow generation and LOD | `TSK-0225`, section 10 |
| Animation weights | `TSK-0222` and `TSK-0224`, section 9 |
| Triplanar fails under animation | `TSK-0226`, section 11 |
| Painting texture mapping | `TSK-0226`, section 11 |

---

## 14. Task dispositions

| Mechanism | Owner | Disposition |
| --- | --- | --- |
| Metaball field, sphere-only vocabulary, mesh attachments | `TSK-0223` | Create |
| Provenance-attributed weights through extraction and welding | `TSK-0224` | Create, child of `TSK-0222` |
| Level-of-detail tiers and generation coalescing | `TSK-0225` | Create, child of `TSK-0008` |
| Deterministic UV charting and procedural paint atlas | `TSK-0226` | Create |
| Weighting research and recommendation | `TSK-0222` | Extend with this report as evidence |
| Limb binding radius against the metaball envelope | `TSK-0201` | Extend, now a metaball-envelope proof |
| Interactive generation cost model and coalescing | `TSK-0104` | Keep, corroborated by F-06 |
| Chain-aware domains and influence weighting policy | `TSK-0147` | Keep. Supersession risk flagged. Reuse its policy and gate from `TSK-0224` |
| Preview generation measurement gate | `TSK-0008` | Keep, re-pointed at the metaball cost model |
| Compact-isocontour vertex quality pass | `TSK-0065` | Keep as P3, not part of the pivot |

Do not reopen `TSK-0111`. It consolidated the primitive vocabulary by design. The
removal is new scope.

---

## 15. Assumptions, blockers, and next evidence

**Assumptions.**

1. The extractor's sign convention can be preserved by the subtraction in
   section 8.1. Verify before `TSK-0223` implementation.
2. `Assets/Creatures/*.json` is the complete set of authored creatures. The prior
   audit reports capsule and ellipsoid use in the dino family. Re-inventory before
   migration.
3. Additive blobs give acceptable limb separation. Spore accepted webbing. This
   project may not. Measure before committing.

**Blockers.**

- None for `TSK-0222`. The weighting research gate can close with this report.
- `TSK-0223` needs a migration decision for existing DNA before removal.

**Next evidence.**

1. Re-measure `GenerateDataBenchmark` on the target machine and record it on
   `TSK-0008`.
2. Derive the conservative drop bound for the summation form and record it on
   `TSK-0225`.
3. Prototype one limb as a blob chain and compare sample count and mesh quality
   against the current SDF path before removing capsule and ellipsoid.

---

## 16. Source ledger

| ID | Source | Access | Result |
| --- | --- | --- | --- |
| S01 | `https://chrishecker.com/My_Liner_Notes_for_Spore` | fetched, full body | Confirmed |
| S02 | `https://chrishecker.com/Real-time_Motion_Retargeting_to_Highly_Varied_User-Created_Morphologies` | fetched | Confirmed |
| S03 | `How To Animate a Character You've Never Seen Before`, GDC 2007 | referenced from S01 and S02, deck not read | Partially confirmed |
| S04 | `https://oceanquigley.blogspot.com/2009/04/spores-creature-skin-painting.html` | fetched | Confirmed |
| S05 | `https://github.com/daniellochner/creature` | fetched | Confirmed. README and LICENSE only. GPL-3.0 |
| S06 | `https://github.com/daniellochner/creature-creator-game` | HTTP 404 | Unavailable |
| S07 | `https://www.andrewwillmott.com/s2007` | HTTP 404 | Unavailable |
| S08 | Moore and Warren, Compact Isocontours, Graphics Gems III | redirect then HTTP 401 | Unverified |
| S09 | Strange Seed devlog 1 | fetched | Confirmed. Modelled parts, CCD IK cancelled |
| S10 | Critter Crosser, The Sapling, Thrive, Elysian Eclipse, Species, The Big Forest | not inspected | Unverified leads |
| S11 | Repository audits: `2026-09-07-spore-spherical-metaballs-rigblocks-and-skin-paint-research.md`, `creaturecreator-spore-informed-deepdive-2026-09-07.md`, `creaturecreator-audit-generation-performance-lifecycle-2026-09-12.md`, `creaturecreator-audit-deformation-skinning-2026-09-12.md`, `procedural-creature-spore-modern-design-and-implementation-guide.md` | read | Confirmed |
| S12 | Source: `SdfProgramBuilder.cs`, `SmoothMinMath.cs`, `MarchingCubesExtractor.cs`, `MeshExtractionResult.cs`, `GeneratedCreature.cs`, `ImplicitSurfaceWeightAuthoring.cs`, `ImplicitSurfaceInfluenceDomainResolver.cs`, `InfluenceWeightingPolicy.cs`, `AppearanceBaker.cs`, `TriplanarNoise.cs`, `Assets/Shaders/VertexLit.shadergraph`, `docs/adr/ADR-007-resolved-morphology-model.md` | read | Confirmed |

Uninspected artifacts: the Critter Crosser video series, The Sapling videos,
the GDC 2007 slide deck, the Mesh Displacement paper, the Blender skin modifier
manual (HTTP 403), and `creature-creator-game`.

## 17. Reconciliation pace

`docs/audits/` holds well over one hundred records. A synthesis dated
`creaturecreator-audit-synthesis-2026-09-12.md` already exists for the same batch
of code audits. This report is specific to the metaball and appearance pivots and
does not replace that record. The unreconciled backlog remains the research leads
in `S10`, which are leads and not findings.
