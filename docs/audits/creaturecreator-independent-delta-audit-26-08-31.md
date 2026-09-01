# CreatureCreator — Independent Delta Audit / Audit-Interpretation Reconciliation

**Audit ID:** `CCA-20260831-18-04-91F7C2`  
**Repository:** `TheMasonX/CreatureCreator`  
**Current branch:** `main`  
**Current commit verified:** `df47a9fa38d3d90fd5501e5abcad55ec6e2e657b`  
**Delta baseline:** `1e1a57569a4e66897d04bcb7d45ecce43cc24b09`  
**Delta:** 3 commits  
**Date:** 2026-08-31

## Executive conclusion

The directional handoff was substantially correct, but it under-reported the architectural consequence of the latest work.

The two quoted findings are independently confirmed:

1. `CreatureEditorWindow` remains a monolith with no dedicated decomposition task.
2. `BoxSdfNode` still accepts NaN/Infinity because its constructor only checks `<= 0`.

However, the more important reconciliation finding is that **CC-087 and CC-088 were archived as `Done` even though their own acceptance criteria are not fully true in the current implementation**.

There is also a fresh cross-stage finding:

> `CreatureMeshGenerator.Generate()` creates the portable SDF program, then `AppearanceBaker.Bake()` constructs a `PartAppearanceSampler.Resolver` which recompiles every individual part plus the Body field.

So the code has a resolved-snapshot implementation, but the complete generation pipeline does **not** yet have the intended single-resolution / single-pipeline ownership model.

---

# 1. Verified commit range

`main` is currently exactly `df47a9fa...`.

Comparing the prior fixed point `1e1a5756...` to this commit yields exactly three commits.

The range contains the major audit-synthesis/task migration work plus the surface-attachment implementation. The range modifies 133 files and includes:

- resolved morphology changes;
- semantic bone resolution;
- SDF compiler migration;
- editor placement;
- task archiving/supersession;
- the 930-line utility-consolidation audit;
- the 121-line synthesis audit;
- new CC-087/088/089/090/091 tasks.

This confirms the quoted agent was looking at the correct architectural wave.

---

# 2. Finding A — CC-087 is archived as Done too early

**Severity: P1 — audit/process correctness + architecture**

The archived ticket describes CC-087 as owning:

- one immutable resolved-creature snapshot;
- hierarchy;
- resolved geometry;
- semantic attachment identity;
- frames;
- world transforms;
- revision identity;
- removal of nearest-body-sample binding;
- migration of SDF, skeleton, bounds, mesh placement, appearance, and editor consumers.

Its acceptance criteria include:

- semantic attachment identity survives Body sample-density changes;
- geometry, skeleton, bounds, and editor placement use the same resolved frame/world transform;
- no finalized semantic consumer searches for nearest Body sample;
- snapshot identity is available to stale-preview and generated-artifact checks.

The actual current implementation does have a real `ResolvedCreatureSnapshot`, `ResolvedPartSnapshot`, resolved Body and limb data, and cached part/child frames.

But the architectural contract is incomplete.

### Confirmed residuals

`ResolvedCreatureSnapshot.Resolve()` still delegates every part's frame resolution to:

```text
CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace(...)
```

The resolver itself performs a parent-chain walk and can resolve the Body surface again.

Therefore the old resolver remains an active canonical computation layer rather than merely a temporary construction adapter.

Also:

- there is no snapshot revision/hash identity;
- there is no resolved Body-frame snapshot object;
- semantic attachment identity is still not represented as a first-class resolved attachment object.

### More importantly

The intended "one snapshot per generation request" boundary is not actually the whole generator boundary.

`CreatureMeshGenerator.Generate()` does not construct and pass a snapshot through all downstream stages. It calls the SDF builder, then passes the original `CreatureDefinition` into appearance baking and resolves mesh-asset transforms independently.

Therefore CC-087 should be described as:

> **snapshot foundation implemented; full ownership boundary not complete.**

The archived ticket's `Done` status is misleading unless the completion definition is intentionally weakened.

---

# 3. Finding B — CC-088 is likewise architecturally incomplete

**Severity: P1**

The ticket says:

> "Make the SDF builder a backend compiler for resolved geometry."

The current implementation is materially closer to this than before, and several important things are correct:

- the compiler consumes `ResolvedCreatureSnapshot` for Body/limb geometry;
- the raw `Sample(LimbChain)` production overload is gone;
- current-schema `PrimarySize` no longer directly drives primitive compilation;
- managed SDF is not the normal generation backend.

But the builder still consumes raw authoring data for semantic decisions.

Examples in the current builder include using:

```text
definition.Parts
part.MeshGeometry
part.MirrorAcrossSymmetryPlane
part.Limb != null
part.Shape.Type
```

and maintaining both:

```text
PartUnionBlendRadius(CreaturePart)
PartUnionBlendRadius(ResolvedPartSnapshot)
```

So the compiler is still part backend compiler and part definition interpreter.

More subtly, `ResolvedShape` remains the owner of legacy expansion:

```text
PrimarySize
 -> Radius fallback
 -> Ellipsoid fallback
 -> Box fallback
```

That can be acceptable as a migration bridge, but then the ticket should explicitly say this is a transitional compatibility layer rather than declaring the complete legacy semantics exit.

### Correct interpretation

The quoted agent was right that `PrimarySize` is no longer live in the portable compiler.

The stronger conclusion "CC-088 is completely finished" is not supported.

---

# 4. Finding C — nearest-Body-sample binding still exists

**Severity: P1**

The quoted handoff suggested checking CC-076.

That check confirms the subtle distinction:

**CC-076 centralized the heuristic. It did not eliminate the heuristic.**

`SemanticBoneResolver.ResolveBodyParentBoneId()`:

1. uses anchor identity when a valid `BodySurfaceAnchor` exists;
2. otherwise resolves the part world position;
3. searches every Body sample;
4. chooses the nearest sample;
5. returns that socket.

It also retains a nearest-sample fallback when an anchor is present but invalid.

Therefore the implementation has achieved:

```text
one place that implements nearest-sample binding
```

but not:

```text
semantic binding no longer depends on nearest sample
```

That is exactly the distinction the CC-087 acceptance criterion makes.

**Disposition:** do not close this finding merely because the resolver was centralized.

---

# 5. Finding D — fresh cross-stage duplication: AppearanceBaker recompiles the morphology

**Severity: P1 architecture / P2 performance**

This was not fully captured by the quoted two-gap reconciliation.

`CreatureMeshGenerator.Generate()` first performs:

```text
DefinitionValidator
    -> SdfProgramBuilder.CompilePortable
    -> DensityGrid.SamplePortable
    -> MarchingCubesExtractor
```

Then it runs:

```text
AppearanceBaker.Bake(definition, meshResult, ...)
```

`AppearanceBaker` constructs:

```text
PartAppearanceSampler.Resolver
```

and that resolver does:

```text
SdfProgramBuilder.CompileIndividualPartsPortable(definition)
SdfProgramBuilder.CompilePortableBodyField(definition)
```

It therefore recompiles the morphology/SDF representation specifically for appearance ownership decisions.

This is a concrete instance of the exact architecture the prior audits were trying to eliminate:

```text
Generation
  -> morphology/SDF derivation
  -> mesh

Appearance
  -> morphology/SDF derivation AGAIN
  -> appearance
```

Rather than:

```text
GenerationRequest
  -> one resolved morphology snapshot
  -> one field program
  -> mesh
  -> appearance metadata / correspondence data
```

This is particularly important because the comments in CC-091 explicitly call out:

> "Each stage consumes resolved or explicit generated values and does not reinterpret raw DNA."

The current appearance implementation clearly does reinterpret raw DNA and compile fresh SDF programs.

### Recommendation

Make appearance ownership a downstream consequence of the generation result rather than a second morphology compilation.

The exact implementation can be decided later, but the boundary should be:

```text
ResolvedCreatureSnapshot
       |
       +--> field/SDF stage
       |       |
       |       +--> mesh
       |       +--> surface/part provenance as needed
       |
       +--> appearance stage
```

Do not solve this by merely passing the same `CreatureDefinition` around.

---

# 6. Finding E — `CreatureMeshGenerator` is still a God method, and CC-091 correctly exists but is not yet implemented

**Severity: P2**

CC-091 is actually the right task.

Its current acceptance criteria call for:

- a thin orchestration method;
- distinct implicit-field, mesh-asset, appearance, and assembly stages;
- stage inputs that are resolved/explicit rather than raw DNA.

`CreatureMeshGenerator.Generate()` currently still owns:

- validation;
- SDF compilation;
- density sampling;
- disposal;
- mesh extraction;
- topology validation;
- appearance baking;
- Unity mesh construction;
- mesh-asset filtering;
- mesh asset resolution;
- placement;
- symmetry;
- geometry-item assembly.

So the ticket is well scoped and should stay open.

This should **not** be treated as the same issue as the `CreatureEditorWindow` monolith. The prior handoff was correct to distinguish them.

---

# 7. Finding F — `CreatureEditorWindow` decomposition gap is real

**Severity: P2 architecture/maintainability**

Independent verification confirms the file remains the central editor class and no dedicated runtime/editor decomposition task appears in the current active task index.

The current active list contains CC-091, but that ticket explicitly targets `CreatureMeshGenerator`, not the editor window.

The editor class still mixes:

- persistence/session lifecycle;
- Undo/Redo;
- inspector UI;
- validation display;
- parts-tree UI;
- body editing;
- limb editing;
- placement gestures;
- stale-preview tracking;
- scene GUI;
- preview generation;
- skeleton overlay;
- generation configuration;
- selection state.

The latest placement work added another ~400 lines to this already central class.

### Correct task disposition

A dedicated P2 task is warranted.

Do not attempt a massive rewrite.

The decomposition should start with seams already implied by existing helper types:

```text
CreatureEditorWindow
    |
    +--> placement controller
    +--> body gesture controller
    +--> limb gesture controller
    +--> preview/session coordinator
    +--> parts-tree presenter
```

Keep the window responsible for composition, Unity lifecycle, and high-level command routing.

---

# 8. Finding G — `BoxSdfNode` finite-input validation gap is real

**Severity: P1 correctness**

Current primitive validation is inconsistent.

Sphere:

```text
radius <= 0
NaN
Infinity
```

Capsule:

```text
radius <= 0
height <= 0
NaN
Infinity
```

Ellipsoid:

```text
each component <= 0
NaN
Infinity
```

Box:

```text
component <= 0
```

only.

Because IEEE comparisons with NaN are false:

```text
float.NaN <= 0f == false
```

a NaN half-extent is accepted.

Infinity also passes the positivity test.

This allows an invalid primitive into SDF evaluation where NaNs/Infinities can propagate.

### Ownership correction

The quoted handoff suggested putting this into **CC-090**.

I do **not** recommend that ownership.

CC-090 is explicitly for shared mechanics, tolerances, common helpers, and mechanically identical utility extraction. `BoxSdfNode` finite-input validation is an **SDF primitive contract**, not a shared utility.

Better ownership:

```text
CC-088
```

or the narrower shape/SDF contract already covered by:

```text
CC-043
```

The fix is still tiny, but putting it into CC-090 would blur task ownership.

---

# 9. Finding H — CC-089 remains a real correctness task, and its scope is larger than one validator branch

**Severity: P1**

The active ticket is:

> Make malformed-definition validation and cloning total.

The current code still contains multiple null-hostile paths.

`CreatureDefinition.Clone()` does:

```text
Parts.Select(p => p.Clone())
```

so a null part entry throws.

`DefinitionValidator.ValidateParentsAndCycles()` builds its ID set using:

```text
foreach (CreaturePart part in definition.Parts)
    idsById.Add(part.Id);
```

without a null guard.

`CreatureDefinition.HasParentCycle()` correctly skips nulls while building `byId`, but later iterates `Parts` and immediately reads:

```text
string currentId = part.Id;
```

which can again throw on a null part.

So "validation is total" is not merely a missing edge case; the definition/validator/clone contract still needs to be made consistently total.

The current task is therefore well justified and should remain P1.

---

# 10. Finding I — the new task/audit infrastructure is better, but its own reconciliation invariant is not yet enforced

**Severity: P2 process**

The new synthesis skill is a meaningful improvement.

It explicitly requires:

- full reconciliation;
- direct evidence for material claims;
- deduplication by mechanism;
- searching the complete local task set before creating a new task;
- no closure without validation evidence.

That is good.

But the 2026-08-30 synthesis still arrived at a state where two concrete mechanisms were not represented by active tasks:

- editor-window decomposition;
- BoxSdf finite validation.

More importantly, this review found additional residuals in tickets marked Done.

The missing process control is a **finding-to-disposition ledger**.

Every audit finding should have a mandatory final state:

```text
Implemented
Implemented-with-residual
Deferred
Superseded
Rejected-with-evidence
Unresolved
```

and a direct code/task reference.

That would have caught both the omitted findings and the premature `Done` classifications.

---

# 11. Finding J — task ownership should be mechanism-based, not "nearest thematic ticket"

This is the key lesson from the quoted handoff.

The suggested:

> "put BoxSdfNode into CC-090"

is thematically understandable because CC-090 mentions finite checks.

But mechanism ownership is better:

```text
BoxSdfNode finite constructor contract
    -> SDF primitive/schema task

Shared finite-check helper extraction
    -> CC-090
```

That distinction should be made explicit in the task skill.

Otherwise future synthesis passes will continue to absorb independent defects into broad cleanup tickets and lose traceability.

---

# 12. Finding K — `ResolvedCreatureSnapshot` file/namespace ownership remains transitional

The resolved snapshot types still live in:

```text
CreaturePartWorldTransformResolver.cs
namespace ProceduralCreature.Definition
```

alongside the old resolver.

Meanwhile:

```text
ResolvedBody
ResolvedLimb
```

live under:

```text
ProceduralCreature.Morphology
```

This means current code expresses a derived/runtime concept as though it still belongs to the authoring definition layer.

This is not a correctness blocker, but it is an architectural signal that the migration is incomplete.

Suggested final organization:

```text
Morphology/
    Resolution/
        ResolvedCreatureSnapshot.cs
        ResolvedPartSnapshot.cs
        ResolvedShape.cs
        ResolvedAttachment.cs
        ResolvedBody.cs
        ResolvedLimb.cs
        ResolvedPolyline.cs
```

Delete the resolver file after its consumers migrate.

---

# 13. Finding L — the anchor implementation itself is directionally good, but its canonicalization boundary deserves a regression test

The new `BodyPlacementAuthoring` path intentionally clones and renumbers Body samples before anchor projection.

That is good defensive isolation for editor mutation.

But the canonicalization only changes the Body sample IDs. It does not rewrite unrelated `ParentAttachment` IDs contained elsewhere in the clone.

Under the current validator, malformed/out-of-order Body IDs are rejected, so this is mostly an authoring robustness concern.

Still, the regression test suite should explicitly pin:

```text
noncanonical input
    -> canonical clone
    -> newly projected anchor uses canonical IDs
```

and:

```text
pre-existing valid anchor
    -> projection uses the same canonical semantic identity
```

This should stay a test concern, not become another runtime abstraction.

---

# 14. Interpretation scorecard

| Finding / recommendation | Current status | Interpretation |
|---|---|---|
| Shared resolved Body/Limb morphology | Implemented | Correct |
| Shared `ResolvedPolyline` | Implemented | Correct |
| Raw limb sampling production escape hatch removed | Implemented | Correct |
| Anchor-aware placement | Implemented | Strong |
| Anchor-based bone identity | Implemented | Correct but fallback remains |
| Nearest-sample binding eliminated | **Not implemented** | Centralized only |
| One snapshot per full generation request | **Not implemented** | Partially true inside individual consumers |
| World-transform resolver retired | **Not implemented** | Still canonical |
| SDF backend has no raw-DNA interpretation | **Not fully true** | Transitional |
| Legacy shape exit fully complete | **Not fully true** | Compatibility semantics remain |
| Appearance consumes resolved/generated stage state | **Not implemented** | Recompiles SDF |
| `CreatureMeshGenerator` decomposed | Not implemented | CC-091 is appropriate |
| `CreatureEditorWindow` decomposed | Not implemented | Missing dedicated ticket |
| Box primitive finite validation | Not implemented | Real bug |
| Malformed validator/clone totality | Not implemented | CC-089 correctly open |
| Shared utility consolidation | Not implemented | CC-090 correctly open |
| Task reconciliation | Improved | Needs finding-disposition ledger |

---

# 15. Recommended next task corrections

## Amend CC-087

Do not erase the historical implementation notes.

Add an explicit residual section:

```text
Remaining:
- generation-wide single snapshot threading;
- resolved Body frame identity;
- semantic ResolvedAttachment;
- revision identity;
- nearest-sample elimination;
- retirement of CreaturePartWorldTransformResolver.
```

Keep the implemented snapshot foundation as historical progress.

## Amend CC-088

Add:

```text
Remaining:
- remove raw CreaturePart interpretation from compiler;
- move legacy PrimarySize expansion completely to migration/load boundary;
- make resolved geometry the compiler's only semantic input.
```

## Create a dedicated editor decomposition task

Suggested:

```text
CC-092 — Decompose CreatureEditorWindow into bounded authoring controllers
Priority: P2
```

Scope only the monolith; do not mix it with UI features.

## Put BoxSdf finite validation under CC-088 or CC-043

Do not put the primitive contract defect under CC-090.

## Make CC-091 explicitly own the AppearanceBaker duplication

Its first implementation slice should probably be:

```text
generation snapshot
        |
        +--> SDF field stage
        |
        +--> mesh extraction
        |
        +--> appearance stage
```

with no second morphology compilation.

---

# 16. Recommended implementation order

```text
1. CC-091 — establish the real generation-stage boundary
       |
       +--> pass one resolved snapshot through Generate()
       |
       +--> stop AppearanceBaker from recompiling morphology
       |
       +--> stop mesh-asset placement from independently resolving hierarchy
       |
       v
2. Finish CC-087 residuals
       |
       +--> semantic attachment result
       +--> frame snapshot
       +--> revision identity
       +--> remove nearest-sample semantic fallback
       |
       v
3. Retire CreaturePartWorldTransformResolver
       |
       v
4. CC-089 — make malformed validation/cloning total
       |
       v
5. CC-088 residual — compiler consumes only resolved backend data
       |
       v
6. BoxSdf finite-input fix
       |
       v
7. CC-090 common-mechanics consolidation
       |
       v
8. CC-092 editor-window decomposition
```

The ordering deliberately avoids decomposing classes around an architecture that is still migrating underneath them.

---

# 17. Final assessment

The quoted agent's directional audit was strong in one important respect: it correctly recognized that the intermediate synthesis lost two concrete findings.

Independent verification confirms both.

But the deeper issue is not just two missing tickets.

The current task system has begun to treat:

```text
implementation slice complete
```

as equivalent to:

```text
architectural boundary complete
```

Those are not equivalent.

The implementation is now at a productive transition point:

```text
authoritative DNA
       |
       v
validation
       |
       v
resolved morphology
       |
       v
generation stages
```

The next wave should make that diagram literally true in code.

The single most important architectural rule to preserve is:

> **Once a semantic fact has a canonical resolved representation, downstream stages must consume that representation rather than independently rediscovering the fact from DNA.**

The remaining nearest-sample binding, world-transform resolver, appearance re-compilation, raw SDF interpretation, and separate mesh-asset resolution are all variations of violating that same rule.

That is the primary focus I would give the next implementation wave.
