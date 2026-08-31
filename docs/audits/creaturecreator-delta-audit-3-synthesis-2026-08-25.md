# CreatureCreator — Delta Audit #3: Consolidation Synthesis (2026-08-25)

**Commit checked:** `1e1a575` — still the tip; no commits since the original report or either prior delta.
**Inputs synthesized:**
1. `creaturecreator-code-audit-2026-08-25.md` (mine — line-level bugs, ticket corrections)
2. `creaturecreator-delta-audit-2026-08-25.md` (mine — mirror-matrix duplication)
3. `creaturecreator-delta-audit-2-2026-08-25.md` (mine — `MinSpacingSqr` unit-mismatch bug)
4. `creaturecreator-consolidation-legacy-exit-audit-26-08-29.md` (uploaded, another agent — architecture-level consolidation review)

**Method for this pass:** rather than treat the uploaded audit as a second opinion to summarize, I went back to source for every checkable claim in it — exact file sizes, exact method names, exact call sites, exact call graphs — before agreeing with any of it. Where I could independently confirm a claim with grep/line evidence, I say so and cite the evidence. Where I found something the uploaded audit didn't (a claim it undersold, or a mechanism it described at a level of abstraction that hid an even more concrete problem), I say that too. Nothing below is taken on the other audit's word alone.

**Bottom line: the uploaded audit's diagnosis holds up.** Every checkable claim I traced was accurate, and in three cases the ground truth is *more* concrete/severe than the prose described. This report's job is to fuse that architectural read with the specific bugs and duplications my own passes already found — several of which turn out to be the same underlying disease at a different altitude.

---

## Part A — Independent verification of the uploaded audit's core claims

### A.1 `SdfProgramBuilder` — confirmed as a second, independent interpretation of the creature model, not just "large"

The uploaded audit's §2/§3 claims: 41 KB, mixes definition traversal with compilation, and carries a private mutable-metadata mini-state-machine (`SetWorldAabb`/`SetCullable`/`SetConsumer`/`ReadAabb`/`ReadCullable`/`PrimitiveLocalAabb`/`TransformToWorld`).

Verified exactly:
```
$ wc -c Runtime/Morphology/Sdf/SdfProgramBuilder.cs
41393  (≈ 41 KB, 834 lines)
```
All seven named helpers exist verbatim, and the `create → SetWorldAabb → SetCullable → SetConsumer` sequence repeats **eight separate times** across the file at near-identical call shapes (lines 249-265, 349-377, 436-455, 512-522, 759-761, 780-800). This is exactly the "partially-initialized-operation state machine" the uploaded audit describes — I count eight sites that would all collapse into one constructor call under the `CompiledSdfNode` IR it proposes.

**What I found beyond the uploaded audit's framing:** tracing `CompileLimbChain`'s call graph shows *why* this file is a second morphology engine, concretely, not just structurally:

```csharp
// SdfProgramBuilder.cs:663
return part.Limb != null ? CompileLimbChain(part.Limb) : CompilePrimitive(part.Shape);
                                              ^^^^^^^^^ raw DNA, not ResolvedLimb

// SdfProgramBuilder.cs:672
private static ISdfNode CompileLimbChain(LimbChain limb)
{
    List<LimbMetaball> metaballs = LimbMetaballSampler.Sample(limb);   // raw LimbChain in, resolves internally
```

`SdfProgramBuilder` never holds a `ResolvedLimb` as a type anywhere in its own traversal — it walks `CreaturePart`/`LimbChain`/`Shape` end to end, and the only place a `ResolvedLimb` gets constructed is *inside* `LimbMetaballSampler.Sample(LimbChain)`, transiently, per call, then discarded. This is the mechanism behind the uploaded audit's "second interpretation of the creature model" claim, not just a description of one — the compiler is architecturally incapable of reusing a resolved snapshot today because it never receives one.

### A.2 `PrimarySize` — confirmed live in the production SDF compiler, extending my own original finding from three sites to five

My original report (§2) found the legacy-shape fallback rule independently reimplemented in three places (`ShapeDefinition.UsesLegacySize()`, `DefinitionCanonicalizer.CanonicalizeShape()`, `CreatureEditorWindow.DrawShapeFields()`). The uploaded audit's §4 claims a fourth: *"the portable compiler still reads `Shape.PrimarySize` and uses it as fallback."* Verified — and it's actually **six call sites in one file**:

```
Runtime/Morphology/Sdf/SdfProgramBuilder.cs:323   float legacySize = part.Shape.PrimarySize;
Runtime/Morphology/Sdf/SdfProgramBuilder.cs:487   float legacySize = part.Shape.PrimarySize;
Runtime/Morphology/Sdf/SdfProgramBuilder.cs:809   shape.Radius > 0f ? shape.Radius : shape.PrimarySize
Runtime/Morphology/Sdf/SdfProgramBuilder.cs:813   new Vector3(shape.PrimarySize, shape.PrimarySize, shape.PrimarySize)
Runtime/Morphology/Sdf/SdfProgramBuilder.cs:817   shape.Radius > 0f ? shape.Radius : shape.PrimarySize
Runtime/Morphology/Sdf/SdfProgramBuilder.cs:823   new Vector3(shape.PrimarySize, shape.PrimarySize, shape.PrimarySize))
```

So the "single source of truth" violation from my original report isn't three independent implementations of one rule — it's **four**, and the fourth is inside the exact file both audits agree needs the most consolidation. This matters for sequencing: CC-043 (per-shape parameters) and the uploaded audit's CC-086/consolidation work are not two separate efforts that happen to touch the same field — fixing `SdfProgramBuilder` to consume `ResolvedShape`/`ResolvedGeometry` (uploaded audit §2) and eliminating the `PrimarySize` fallback (uploaded audit §4) are the same change. Doing one without the other leaves a legacy read sitting inside whatever replaces the current compile loop.

### A.3 `ResolvedBody` / `ResolvedLimb` — confirmed as a near-exact duplicated block, not just "the same shape of data"

The uploaded audit's §6 describes this as shared conceptual state (positions, segment lengths, arc length, root/terminal, degenerate behavior). Reading both files in full, it's stronger than that — it's the same code, twice, including matching magic constants and matching prose:

| | `ResolvedBody.Resolve` | `ResolvedLimb.Resolve` |
|---|---|---|
| Segment-length loop | `segmentLengths[i] = Vector3.Distance(positions[i], positions[i+1]); totalLength += ...` | identical |
| Degenerate threshold | `if (totalLength <= 1e-6f)` | identical literal |
| Terminal pinning comment | `// Pin the terminal to exactly 1 (defensive against float accumulation).` | **word-for-word identical comment** |
| `Centerline` / `RootSocket` / `TerminalSocket` | computed properties, same shape | identical shape |

The only real deltas are `ResolvedBody`'s per-sample `SampleRadii` and `ResolvedLimb`'s `Thickness` profile — both of which the uploaded audit's proposed split (`ResolvedPolyline` core + a thin `ResolvedBody`/`ResolvedLimb` wrapper carrying only the type-specific field) already accounts for correctly. One additional asymmetry worth folding into that work: `ResolvedBody.Resolve` has a second overload taking `IReadOnlyList<BodySample>` directly (for callers that don't have a `BodySpline`); `ResolvedLimb.Resolve` has no equivalent overload. Worth deciding whether `ResolvedPolyline` needs that entry point once, rather than each resolved type deciding independently again.

**Connection to my delta audit #2:** `ResolvedBody.Resolve`'s degenerate check uses `1e-6f` — a properly linear-scale epsilon. That's independent confirmation of the fix value for the `MinSpacingSqr = 1e-10f` bug I found in `BodySplineAuthoring.cs` (five of six call sites compare a squared-magnitude-scale constant against linear distances, silently defeating degenerate-spline guards). `ResolvedBody`'s own convention for "this polyline has collapsed" is `1e-6f`, not `1e-10f` — that's the number to use when fixing `MinSpacingSqr`, not an arbitrary pick. **This also means the `MinSpacingSqr` fix should land as part of the `ResolvedPolyline` extraction, not as an isolated bugfix** — `BodySplineAuthoring`'s degenerate-length checks are exactly the kind of "consumer re-derives what a resolved snapshot should own" pattern §14 of the uploaded audit describes, and if `BodySplineAuthoring` is migrated to consume `ResolvedBody`/`ResolvedPolyline` for its length/degeneracy checks instead of recomputing them locally, the unit-mismatch bug is deleted along with the duplicate code, rather than patched in place and left to duplicate `ResolvedBody`'s logic a second time.

### A.4 `LimbMetaballSampler.Sample(LimbChain)` — confirmed to exist, but the uploaded audit undersells where it's used

The uploaded audit frames this as "a compatibility escape hatch... for callers [to migrate off]," implying legacy call sites gradually being replaced. The actual call-site census:

```
Tests/Runtime/ResolvedLimbTests.cs        — 2 call sites (explicitly testing both overloads agree)
Tests/Runtime/LimbMetaballSamplerTests.cs — 9 call sites (the original CC-039 test suite, pre-dates ResolvedLimb)
Runtime/Morphology/Sdf/SdfProgramBuilder.cs:674, 727 — 2 call sites, PRODUCTION
```

The two production call sites are inside `CompileLimbChain`, called from the SDF compiler's main per-part dispatch (see A.1) — this is not a vestigial back-compat path being kept alive only by old tests, it is **the only way a limb chain gets metaball-sampled during real SDF compilation today.** `SdfProgramBuilder` doesn't have a `ResolvedLimb` to pass, so it structurally can't call the resolved overload even if it wanted to — it's not choosing the compatibility path, it's the only path available to it given its current inputs. This raises the priority of A.1/A.2's fix relative to how the uploaded audit sequenced it: deleting the `Sample(LimbChain)` overload (uploaded audit §5) isn't safe until `SdfProgramBuilder` is restructured to receive resolved geometry (uploaded audit §2) — they're not two independent P1 items, the second is a hard prerequisite for the first, not just a nice-to-have ordering.

### A.5 Nearest-body-sample binding — confirmed as a literal linear nearest-neighbor scan, exactly as the mechanism the audit warns about

Uploaded audit §10: *"`SkeletonInferrer.ResolveBodyParentBoneId` still effectively performs attachment position → nearest Body sample → bone."* Confirmed verbatim:

```csharp
// SkeletonInferrer.cs:361-370
int nearestIndex = 0;
float nearestDistance = float.PositiveInfinity;
for (int i = 0; i < definition.Body.Samples.Count; i++)
{
    float distance = (definition.Body.Samples[i].Position - position).sqrMagnitude;
    if (distance < nearestDistance) { nearestDistance = distance; nearestIndex = i; }
}
return CreatureDefinition.BodyId + LimbJointBoneSeparator + definition.Body.Samples[nearestIndex].Id;
```

This is precisely the density-dependent binding the audit warns about: re-running `SpaceEvenly` (`BodySplineAuthoring.cs`) at a different sample count, with the exact same anatomical shape, can change which sample is nearest to a given attachment point and therefore which bone a limb parents to — a purely cosmetic re-spacing operation silently able to change rig topology. Same file, three lines above this method (line 277), the raw `parentPart.Limb.Joints.Count - 2` terminal-index arithmetic the uploaded audit's §14 separately flags ("every consumer's `N - 2` calculation") lives right next to it — both are raw-DNA reads inside the one method that's supposed to be producing semantic, representation-independent bone bindings.

---

## Part B — Where my prior findings and the uploaded audit describe the same disease

Laid out explicitly, because the pattern itself is the finding:

| My finding (code-level) | Uploaded audit's framing (architecture-level) | Same root cause? |
|---|---|---|
| Legacy shape fallback triplicated (orig. report §2) → now known to be 4-5 sites incl. `SdfProgramBuilder` (A.2 above) | §4: eliminate `PrimarySize` rather than abstract it | Yes — identical |
| Mirror reflection matrix quadruplicated across `MirrorUtility`/`SdfProgramBuilder`/`CreatureMeshGenerator`/`SkeletonInferrer` (delta #1) | Not named directly, but is a textbook instance of §21's "multiple representations of the same concept surviving simultaneously" | Yes — same class, different subsystem |
| `PartType.Limb/Leg/Arm` with no `Limb` chain unvalidated; demo fixture relies on it (orig. report §3) | §9's "attachment... should not rediscover... spatially" / §16's Body/editor cluster convergence | Related — another spot where the type doesn't fully commit to what it claims to represent |
| `IsLimbChainType` duplicated across Editor/Runtime asmdef boundary (orig. report §4) | Not directly addressed | Adjacent — same "convergence" instinct, applied to a predicate instead of a resolved type |
| `MinSpacingSqr` unit mismatch (delta #2) | §6's `ResolvedPolyline` proposal (via A.3 above) | Yes, once traced — the fix and the consolidation are the same commit |

The throughline across every one of these, mine and the uploaded audit's alike: **there is no single place in this codebase yet where "what does this creature actually look like, geometrically" is computed once and handed downstream.** Every consumer that needs a length, a mirror, a terminal, or a fallback-shape value currently has the option to either ask a resolved type for it or re-derive it from raw DNA locally, and in practice the codebase does both, inconsistently, depending on which subsystem and which era of the project touched that call site last.

---

## Part C — Corrections / additions to the uploaded audit's task-board recommendations

The uploaded audit's §17 table and CC-086/CC-087 proposals (§18/§19) are sound and I'd endorse both new tickets as specified. Additions based on Part A/B:

- **CC-086's acceptance criteria** should explicitly include *"`BodySplineAuthoring`'s degenerate-length checks are expressed against `ResolvedBody`/`ResolvedPolyline` rather than re-computed locally"* — this is where the `MinSpacingSqr` bug gets fixed as a side effect of consolidation rather than needing its own patch that then has to be kept in sync with `ResolvedPolyline`'s own degenerate-length convention (`1e-6f`) a second time.
- **CC-087's acceptance criteria** ("builder does not resolve morphology... transform/symmetry decisions come from resolved geometry") should explicitly call out the reflection-matrix consolidation from my delta audit #1 as in-scope — `SdfProgramBuilder`'s private `CreatureMirrorAcrossX` field is exactly the kind of compiler-owned domain-math duplicate CC-087 exists to remove, and it's a same-file change already touched by the rest of CC-087's work, so it should ride along rather than become a separate ticket.
- **Sequencing correction to §20's Step 5/6:** the uploaded audit lists "migrate all consumers" (Step 5) before "shrink `SdfProgramBuilder`" (Step 6). A.1/A.4 above show this ordering has a hidden dependency in the other direction for the limb path specifically — `SdfProgramBuilder` cannot stop calling `LimbMetaballSampler.Sample(LimbChain)` until it has a `ResolvedLimb` to pass, which means the "resolve once at the top of a limb's compile, pass `ResolvedLimb` through `CompileLimbChain`" piece of Step 6 has to land *before or alongside* the `Sample(LimbChain)` overload deletion, not strictly after it as a separate consumer migration.
- **CC-082/CC-083/CC-084**, which the uploaded audit's table marks "fold into validator-totality work" / "keep independent": my original report already pinpointed exact root causes for all three (CC-082's `HasParentCycle.ToDictionary` throw, CC-083's test-helper `??` bug rather than a validator bug, CC-084's `CanonicalJsonWriter.cs:231` write-time substitution) — worth attaching those line references to whichever ticket absorbs CC-082/083 so the fix doesn't have to be re-diagnosed from the ticket text alone.

---

## Summary table

| # | Finding | Source | Status this pass |
|---|---|---|---|
| A.1 | `SdfProgramBuilder` never holds a `ResolvedLimb`; resolves transiently inside `LimbMetaballSampler` per call | New (call-graph trace) | Confirms & sharpens uploaded audit §2 |
| A.2 | `PrimarySize` fallback present at 6 call sites inside `SdfProgramBuilder` — a 4th/5th independent implementation of my original report's §2 finding | New evidence for uploaded audit §4 + extends my original report | Confirmed, more severe than either audit alone stated |
| A.3 | `ResolvedBody`/`ResolvedLimb` share a near-verbatim 40-line block (matching constants, matching comments) | Confirms uploaded audit §6 with exact evidence | Confirmed exactly |
| A.3b | `MinSpacingSqr` fix value should be `1e-6f`, matching `ResolvedBody`'s own convention; fix belongs inside the `ResolvedPolyline` migration, not as a standalone patch | Synthesizes my delta #2 with uploaded audit §6 | New recommendation |
| A.4 | `Sample(LimbChain)` overload is live in the SDF compiler's only production limb path, not just kept alive by tests | Corrects uploaded audit §5's framing | New — changes sequencing priority |
| A.5 | Nearest-Body-sample bone binding confirmed as literal O(n) nearest-neighbor scan; sits next to the raw `N-2` terminal-index calc the same audit flags separately | Confirms uploaded audit §10 | Confirmed exactly |
| C | CC-086/CC-087 acceptance criteria should absorb `MinSpacingSqr` and mirror-matrix consolidation respectively, rather than leaving them as orphaned small fixes | Task-board correction | New |
