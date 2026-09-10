# CreatureCreator — Round 20: The SDF Programs Get Compiled Twice Per Regeneration — and `TSK-0095` Already Says They Shouldn't

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `28240f0` (pulled 10 new commits this round, including a parallel "11 more audits" effort and its own meta-synthesis — read both before continuing, see note at the end)
**Scope this round:** tracing the full "regenerate and rebind a creature" call path end-to-end, following up on Round 18/19's generation-time findings, specifically to see whether the compiled SDF programs those rounds looked at are being reused sensibly across pipeline stages or rebuilt redundantly.

---

## Finding: `CreatureRuntimePreview`'s regeneration flow compiles the SDF part/body programs twice — once for mesh generation, once again for skin-binding — and an already-`InProgress` task states the exact rule this violates

### The call path

`CreatureRuntimePreview` regenerates a creature in two back-to-back steps:

1. **Mesh generation** — `CreatureMeshGenerator.GenerateData` resolves the definition once (`ValidateAndResolve` → `ResolvedCreatureSnapshot snapshot`), then compiles the SDF part/body programs once for the whole stage:

   ```csharp
   // CreatureMeshGenerator.BakeAppearance
   compiledParts = SdfProgramBuilder.CompileIndividualPartsPortable(definition, snapshot);
   bodyProgram = SdfProgramBuilder.CompilePortableBodyField(definition, snapshot);
   ...
   colors = AppearanceBaker.Bake(definition, meshResult, null, compiledParts, bodyProgram, snapshot.Body, snapshot);
   ...
   finally { /* dispose compiledParts, bodyProgram */ }
   ```

   This part is done correctly — I traced it specifically expecting to find a redundant compile *inside* `AppearanceBaker.Bake` itself (its public 3-argument overload does independently recompile, calling `ResolvedCreatureSnapshot.Resolve` a second time from scratch), but `CreatureMeshGenerator` never calls that overload — it calls the `internal` 6/7-argument overload with its own already-compiled programs, which `AppearanceBaker.Bake` then correctly threads through to `PartAppearanceSampler`/`BakeBurst` without recompiling again. Worth stating plainly since it means Round 18/19's per-generation cost model doesn't need revising — this part of the pipeline is clean.

2. **Skin-binding, immediately after** — `CreatureRuntimePreview.BindImplicitSurfaceToRig(generated, definition, snapshot)` receives the *same* `definition` and the *same, already-resolved* `snapshot` that step 1 just produced (both are parameters, passed down from the caller that ran step 1 moments earlier), and calls:

   ```csharp
   InfluenceDomain[] vertexDomains = ImplicitSurfaceInfluenceDomainResolver.Resolve(
       definition, snapshot, implicitItem.Mesh.vertices);
   ```

   `Resolve`'s body (`Animation/Binding/ImplicitSurfaceInfluenceDomainResolver.cs:35-37`) does:

   ```csharp
   List<ResolvedPartProgram> parts = SdfProgramBuilder.CompileIndividualPartsPortable(definition, snapshot);
   SdfProgram body = SdfProgramBuilder.CompilePortableBodyField(definition, snapshot);
   ```

   — a **second, independent, from-scratch compilation** of the exact same part/body SDF operation trees that step 1 already built, used, and disposed a moment earlier in the same overall regeneration. `Resolve` does receive the already-resolved `snapshot` (so it correctly avoids a second `ResolvedCreatureSnapshot.Resolve`, i.e. it doesn't hit Round 14's `FindPart` cost a second time) — but compiling the SDF operation trees is a distinct, additional, non-trivial step on top of that, and there's currently no way to hand step 1's already-built `compiledParts`/`bodyProgram` across to step 2, because `ImplicitSurfaceInfluenceDomainResolver.Resolve`'s public signature only accepts `(definition, snapshot, vertices)` — it has no overload that accepts pre-compiled programs the way `AppearanceBaker.Bake`'s internal overload does.

### Why this is worth a task on its own, not just an observation

`TSK-0095` ("Establish concrete generation-pipeline stage boundaries," status **InProgress**) states its scope in almost these exact words:

> *"Thread one resolved snapshot or explicit generated correspondence through field, appearance, mesh-asset placement, and assembly stages; **do not recompile morphology independently from raw DNA in a downstream stage.**"*

The skin-binding step is a downstream stage of the same regeneration (it runs immediately after mesh generation, consuming that stage's own output), and it recompiles morphology (the SDF programs) independently — the `snapshot` is threaded through correctly, per the letter of that rule, but the *compiled programs derived from it* are not, so the rule's actual intent (don't redo expensive derived work that's already sitting right there) is still being violated one level down from where the rule currently checks. This is a live, current, concrete instance of exactly the gap `TSK-0095` already named — worth reporting directly against that task rather than as a new one, since it's the same principle, just needing to be applied one hop further down the call chain than it currently reaches.

### Why it matters more than it might look

This compounds with two things already found this audit series:

- **Round 19**: `ImplicitSurfaceInfluenceDomainResolver.Resolve`'s per-vertex loop (which runs on the programs this redundant compile produces) is itself the most expensive non-Burst loop found in the subsystem — `O(Vertices × Parts × OperationsPerPart)`, plain C#, no job parallelization. The redundant compile feeds directly into the single most expensive step already identified.
- **Round 14 (P1)**: `ResolvedCreatureSnapshot.Resolve` is itself costly due to `CreatureDefinition.FindPart`'s uncached hierarchy-index rebuild. This particular redundant-compile finding doesn't re-trigger that cost (the snapshot is correctly reused, not re-resolved) — worth being precise about that so this finding doesn't overstate its own severity by double-counting Round 14's cost. It's specifically the *SDF program compilation* that's redundant here, not the snapshot resolution.

### Recommendation

Give `ImplicitSurfaceInfluenceDomainResolver.Resolve` (or `CreatureRuntimePreview.BindImplicitSurfaceToRig`, whichever is the more natural seam) an overload that accepts already-compiled `List<ResolvedPartProgram>`/`SdfProgram` — mirroring exactly the pattern `AppearanceBaker.Bake`'s internal overload already uses — and have `CreatureRuntimePreview` hold on to the `compiledParts`/`bodyProgram` `CreatureMeshGenerator.GenerateData` builds internally (which currently means either exposing them from `GenerateData`'s result, or restructuring so mesh generation and skin-binding share one compile-dispose scope instead of two separate ones). This removes one full SDF-tree compilation pass from every single creature regeneration, and it directly closes the specific gap `TSK-0095`'s own stated scope is already aimed at — recommend filing this as a concrete sub-item under `TSK-0095` rather than a new task, since it's the same rule, just extended one call-frame further than it currently reaches.

---

## Note: a large amount of parallel audit work landed this round

Ten new commits landed on the branch since Round 19, including a parallel "11 more audits" effort with its own `docs/audits/creaturecreator-11-audit-takeover-synthesis-2026-09-09.md` and `docs/audits/meta-synthesis-repeat-patterns-2026-09-09.md` (a synthesis of *recurring patterns* across all audit rounds so far, including several of mine). I read both in full before continuing this round specifically to avoid duplicating that work — none of it overlaps with this finding (the closest adjacent item in the meta-synthesis is pattern C, "destroy-then-build non-transactional resource replacement," which is a correctness/robustness issue in `CreaturePreviewController`/`CreatureRuntimePreview`, not this performance/redundant-compilation issue). Also confirmed `IkChainSolver`'s LINQ-allocation removal (commit `e7e722a`, landed this round) already covers the one LINQ hot-path allocation I'd have otherwise flagged in `Animation/Ik/` — nothing further to add there.
