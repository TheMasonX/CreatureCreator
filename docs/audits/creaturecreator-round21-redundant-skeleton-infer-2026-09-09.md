# CreatureCreator — Round 21: `SkeletonInferrer.Infer` Also Runs Twice Per Regeneration — Same Bug Shape as Round 20, Unconditionally This Time

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `28240f0` (no new commits since Round 20)
**Method:** followed the same approach that found Round 20's bug — traced a full pipeline call path end-to-end rather than reading one file in isolation — and applied it to the other expensive per-part computation this subsystem does: skeleton inference, not just SDF compilation.

---

## Finding: every single regeneration infers the skeleton twice, unconditionally, even for creatures with no mesh-asset parts

### The call path

`CreatureRuntimePreview.Update()` runs both of these, back to back, every time a generation result comes back:

```csharp
GeneratedCreature generated = CreatureMeshGenerator.Assemble(result.Data, ResolveMeshAsset);
...
DestroyGeneratedGeometry();
BindImplicitSurfaceToRig(generated, result.Data.Definition, result.Data.Snapshot);
```

**Step 1 — `Assemble`.** `CreatureMeshGenerator.Assemble` unconditionally calls `AppendMeshAssetItems(generated, data, meshResolver)` (`CreatureMeshGenerator.cs:199` — not gated behind "if this creature has mesh-asset parts," it always runs), and `AppendMeshAssetItems`'s very first line is:

```csharp
private static void AppendMeshAssetItems(GeneratedCreature generated, GeneratedCreatureData data, Func<string, Mesh> meshResolver)
{
    SkeletonSnapshot skeleton = SkeletonSnapshot.Capture(SkeletonInferrer.Infer(data.Snapshot));
    var meshParts = data.Snapshot.PartsById.Values.Where(p => p.HasMeshGeometry).OrderBy(p => p.Id, StringComparer.Ordinal);
    foreach (ResolvedPartSnapshot resolvedPart in meshParts) { ... }
    ...
}
```

So `SkeletonInferrer.Infer` runs on every regeneration regardless of whether `meshParts` ends up empty — a pure-implicit-surface creature with zero mesh-asset parts still pays the full inference cost here for a skeleton that this method then does nothing further with (the `foreach` simply never executes its body).

**Step 2 — `BindImplicitSurfaceToRig`, called immediately after.** Same event, same `result.Data.Snapshot` instance:

```csharp
private void BindImplicitSurfaceToRig(GeneratedCreature generated, CreatureDefinition definition, ResolvedCreatureSnapshot snapshot)
{
    ...
    SkeletonModel skeleton = SkeletonInferrer.Infer(snapshot);
    ...
    _rig.Build(skeleton);
    _rig.ApplyPose(PosedSkeleton.FromRestPose(skeleton));
    ...
}
```

`SkeletonInferrer.Infer(snapshot)` runs a second time, on the identical `ResolvedCreatureSnapshot` instance, producing what should be a bit-identical `SkeletonModel` to the one Step 1 just built (and, in Step 1's case, may have discarded unused). This is the exact same shape as Round 20's finding — `TSK-0095`'s already-stated rule (*"do not recompile morphology independently from raw DNA in a downstream stage"*) applies here just as directly, since skeleton inference is exactly the kind of "morphology" derivation that rule is aimed at, and `BindImplicitSurfaceToRig` is exactly such a downstream stage relative to `Assemble`.

### Why this one is worth calling out separately rather than folding silently into Round 20

Two differences make it a distinct, additional finding rather than a restatement:

1. **It's a different expensive computation** (skeleton inference — `AnatomicalBodyRigLayout.Build`'s directional segment walks, `SemanticBoneResolver`'s per-part parent-chain resolution, the whole subsystem this series' Round 14 deep-dive covered — not SDF program compilation). Fixing Round 20's redundant-compile issue wouldn't touch this one; they need separate (if similarly-shaped) fixes.
2. **It's unconditional even when the work ends up unused.** Round 20's redundant compile always produces output that gets used (the domain resolver needs its own programs either way). Here, Step 1's inferred skeleton is thrown away unused whenever `meshParts` is empty — meaning for a creature with no separate mesh-asset parts (plausible for many/most authored creatures, since mesh-asset parts are an optional feature per `CreaturePart.HasMeshGeometry`), `Assemble` pays the full skeleton-inference cost for nothing at all, and `BindImplicitSurfaceToRig` then pays it again for the one result actually used. That's not just "redundant," it's "redundant AND sometimes wasted outright."

### Also confirmed: a third `FindPart` hot call site (extends Round 14's P1 finding)

While tracing this, `AppendMeshAssetItems`'s mesh-parts loop calls `data.Definition.FindPart(resolvedPart.Id)` once per mesh-asset part (`CreatureMeshGenerator.cs:~227`) — this is a call site I hadn't enumerated in Round 14's original census (which counted 2 generation-critical sites plus the Editor window's 38). Same underlying issue (`CreatureDefinition.FindPart` rebuilds the whole hierarchy index every call), same fix (thread a pre-built `CreaturePartHierarchyIndex` through), just one more place it shows up. Not a new root cause — folding this into the existing Round 14 recommendation, not proposing anything new for it.

### Checked against the task system

No existing task covers this: searched for `SkeletonInferrer`, "redundant skeleton," and `AppendMeshAssetItems` across `Data/Tasks/`. `TSK-0186` ("Harden generated mesh assembly resource ownership," Done) is the closest thing physically near this code, but its scope is Unity `Mesh` object disposal/ownership transactionality during partial failures — a completely different concern from redundant computation. `TSK-0095` remains the right umbrella, same as Round 20, since it already states the general principle both findings violate.

### Recommendation

Same shape as Round 20's fix: have `Assemble` accept (or `BindImplicitSurfaceToRig` supply) an already-inferred `SkeletonModel`/`SkeletonSnapshot` rather than each independently calling `SkeletonInferrer.Infer`. Since `Assemble` runs first but may not need the skeleton at all (only mesh-asset placement does), the cleaner order is probably: skip the inference in `Assemble` entirely when `meshParts` is empty (cheap, immediate, no restructuring needed — just move the `SkeletonSnapshot.Capture(SkeletonInferrer.Infer(...))` call after the `Where(...)` filter and only run it if the filtered sequence is non-empty), and separately have `BindImplicitSurfaceToRig` reuse whatever `Assemble` computed when it *did* need one, rather than inferring a second time unconditionally. Recommend filing both this and Round 20's finding as sibling sub-items under `TSK-0095`, since they're the same principle applied to two different expensive derivations in the same pipeline.
