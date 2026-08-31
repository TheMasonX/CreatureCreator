# CreatureCreator — Delta Audit #4: God-Class Decomposition (2026-08-25)

**Commit checked:** `1e1a575` — re-verified via `git fetch`, still the tip; no commits since delta audits #1-3.
**Scope this pass:** a concrete decomposition map for `CreatureEditorWindow.cs` (2850 lines, ~150 members — the largest unaddressed item flagged-but-not-detailed across all three prior reports), plus two more instances of the "consumer re-derives resolved data locally" pattern from delta audit #3.

---

## 1. `CreatureEditorWindow.cs` — a concrete decomposition map, not just a size complaint

Every prior report (mine and the uploaded one) names this file as a God Object in passing and moves on. This pass maps its full member list into responsibility clusters, with line ranges and method counts, so "decompose it" becomes a specific list of extractions rather than a size complaint.

I read every method signature in the file (grep of all `private`/`public`/`static` members) and grouped them by what they actually do, not by where they sit in the file:

| Cluster | Methods | Approx. lines | Extract to |
|---|---|---|---|
| **Scene-view handles & viewport drag interaction** | `DrawSkeletonOverlay`, `DrawSelectedPartHandle`, `DrawLimbJointHandles`, `CommitLimbJointDrag`, `CancelLimbJointDrag`, `DrawBodySampleHandles`, `GetBodyDisplayPositions`, `DrawBodySplineConnections`, `DrawBodyEndpointExpansionHandles`, `SolveBodyDrag`, `DrawBodyEditPreview`, `CommitBodyDrag`, `CancelBodyDrag`, `CommitBodyRadiusDrag`, `CancelBodyRadiusDrag`, `CopyBodyPositions`, `HasUnevenBodySpacing`, `ApplyViewportMove`, `HandlePlacementClick`, `PlaceNewPartAtWorldPosition`, `WorldToLocalPosition` (×2) | **~1760-2593** (833 lines) | `BodyViewportController` / `PartViewportController` |
| **Part inspector field drawing** | `DrawPartInspector`, `DrawPartTypeField`, `DrawBodyInspector`, `DrawBodySplineSection`, `DrawBodyAppearanceFields`, `FindBodySample`, `CurrentBodySpacing`, `DrawParentPicker`, `DrawTransformFields`, `ClampToBounds`, `TransformsRoughlyEqual`, `DrawShapeFields`, `DrawMeshGeometryFields`, `FirstPaletteKey`, `DrawLimbFields`, `DrawAppearanceFields`, `DrawSymmetryFields` | **~995-1724** (729 lines) | `PartInspectorPanel` |
| **Part hierarchy tree UI** | `DrawPartList`, `DrawBodyNode`, `DrawPartNode`, `ChildrenOf`, `OrderedChildrenOf`, `SelectPart`, `SetPartExpanded`, `PersistExpandedPartIds`, `LoadExpandedPartIds`, `PruneExpandedPartIds`, `RevealScrollIfTarget`, `AddNewPart`, `NewGenericPart`, `RemoveSelectedPart` | **~648-995** (347 lines) | `PartHierarchyPanel` |
| **Preview generation pipeline** | `RegeneratePreview`, `FormatDiagnosticTiming`, `ApplyPreviewMesh`, `ApplyPreviewGeometry`, `AssignPreviewItemMaterials`, `ClearPreviewGeometryChildren`, `ResolveMeshAsset`, `EffectiveMeshPalette`, `EffectiveMaterialPalette`, `ResolveDefaultMaterial`, `CreateDefaultPreviewMaterial`, `ScheduleAutoRegeneration`, `ProcessAutoRegeneration` | **~2593-2850** (257 lines) | `PreviewGenerationController` |
| **File I/O (new/save/save-as/load)** | `SaveCurrentFromMenu`, `CreateNew`, `SaveCurrent`, `SaveAs`, `WriteToDisk`, `LoadFromDisk` | **~538-648** (110 lines) | fold into existing `CreatureEditorSession.cs`, or a sibling `CreatureFileIO` |
| **Window lifecycle / mutation plumbing** | `ShowWindow`, `OnEnable`, `OnDisable`, `OnUndoRedoPerformed`, `OnGUI`, `OnSceneGUI`, `MutateDefinition`, `ReplaceDefinition`, `ApplyDefinitionChange`, `Revalidate`, `DrawToolbar`, `DrawEditorSettings`, `DrawValidationPanel` | **~213-538, 1724-1822** | genuinely belongs on `CreatureEditorWindow` — this is what an `EditorWindow` subclass is *for* |

That's roughly **2150 of the file's 2850 lines (~75%)** living in four clusters that have no structural need to be on the `EditorWindow` subclass itself — they're UI-panel and interaction-controller responsibilities that happen to read and write the same handful of window fields. What's left after extraction is a genuinely small `EditorWindow` — lifecycle callbacks, toolbar, the mutation/undo plumbing it already centralizes well (`MutateDefinition` → `ApplyDefinitionChange` → `Revalidate` is a clean single path, no complaint there) — plus composition of the four extracted pieces.

**Why this isn't "just move the code":** the largest cluster (scene-view handles, 833 lines) is tightly coupled to ~15 private instance fields — `_bodyDragIndex`, `_bodyDragKind`, `_bodyDragSnapshot`, `_bodyDragFinalTarget`, `_bodyDragPreview`, `_bodyRadiusDragIndex`, `_bodyRadiusDragStartRadius`, `_bodyRadiusDragTargetRadius`, `_limbDragJointIndex`, `_limbDragSnapshotLocal`, `_limbDragFinalTargetLocal`, `_placementModeActive`, plus indirect reads of `_definition`/`_selectedPartId`/`_activeBodySampleIndex`. Extraction has to carry that state along, not leave it behind — e.g. `DrawBodySampleHandles` at line 2034 branches on `_bodyDragIndex >= 0`, checks `Event.current` for Escape/MouseUp, and calls `CommitBodyDrag()`/`CancelBodyDrag()`, all of which read/write the same drag-state fields. The right shape is a `BodyViewportController` class that *owns* that drag state, is constructed once by the window with a reference to `MutateDefinition` (the one clean seam the window already has) and the current `_definition`/`_selectedPartId`, and exposes something like `DrawHandles()` for the window's `OnSceneGUI` to call. This is exactly the same shape `BodyEditSolver` (pure math, no UnityEditor dependency, already extracted) and `BodySplineAuthoring` (pure math, already extracted) already demonstrate elsewhere in this same file's dependency graph — the math is already properly separated; what's still stuck inside the window is the *stateful interaction* layer that drives that math from mouse events. This project has clearly already internalized "pull math out into a testable pure class" (CC-016 did exactly that); it just hasn't yet applied the same move to "pull the interactive controller out into its own stateful class."

**Recommendation, in extraction order (each step independently shippable):**
1. **Preview pipeline** first — lowest coupling to editor-only mouse-event state, most self-contained (reads `_definition`/`_generationConfig`/settings, writes `_previewGameObject`/diagnostics). Good first extraction to prove the pattern.
2. **Part hierarchy tree** — moderate coupling (`_expandedPartIds`, `_partListScroll`, `_selectedPartId`), no SceneView/mouse-event dependency.
3. **Part inspector** — largest single cluster by method count (17), moderate coupling to per-field drag state (`_bodyDragIndex`, etc., since `DrawBodySplineSection` etc. live here too) — do after the viewport controller exists so the inspector can just ask it for current drag state instead of owning any of it.
4. **Scene-view viewport controller** — do this one third-to-last, informed by what the inspector extraction reveals about the drag-state boundary.
5. **File I/O** — smallest, safest, can happen any time; natural home is the existing `CreatureEditorSession.cs` file since it's already the "editor persistence" concern-holder.

None of this needs new abstractions — it's the same "small concrete class, not an interface hierarchy" instinct the uploaded consolidation audit argues for elsewhere (§2, §8). A `BodyViewportController` is a plain class with a constructor and a handful of public methods, not an `IViewportController` with three implementations.

---

## 2. Two more instances of "consumer re-derives resolved data locally" — found while reading the inspector cluster

Delta audit #3 established that `ResolvedBody`/`ResolvedLimb` exist specifically so consumers stop re-deriving segment/arc-length math from raw samples, and found several places that still do. Reading `CreatureEditorWindow`'s inspector cluster for the decomposition map above turned up two more, both small but on-pattern:

```csharp
// CreatureEditorWindow.cs:1290 — CurrentBodySpacing
private static float CurrentBodySpacing(BodySpline spline)
{
    if (spline == null || spline.Samples == null || spline.Samples.Count < 2) return 1f;
    float total = 0f;
    int pairs = 0;
    for (int i = 1; i < spline.Samples.Count; i++)
    {
        if (spline.Samples[i] == null || spline.Samples[i - 1] == null) continue;
        total += Vector3.Distance(spline.Samples[i].Position, spline.Samples[i - 1].Position);
        pairs++;
    }
    return pairs > 0 ? total / pairs : 1f;
}
```

This is `ResolvedBody.TotalLength / (SampleCount - 1)` computed from scratch instead of from a `ResolvedBody` snapshot — a full local re-implementation of the same segment-summation loop `ResolvedBody.Resolve` already does, just averaged instead of summed. It's used to preview the spacing of a new tail sample before `AppendSample` commits it. Once `ResolvedBody`/`ResolvedPolyline` consumption reaches the editor layer (delta audit #3's CC-086 scope), this becomes a one-line read of an already-resolved `TotalLength` instead of a second loop over raw samples — same category as the `MinSpacingSqr` finding, lower severity (this one isn't buggy, just duplicated).

```csharp
// CreatureEditorWindow.cs:1285 — FindBodySample
private static BodySample FindBodySample(CreatureDefinition definition, uint id)
{
    return definition.Body.Samples.First(s => s.Id == id);
}
```

Minor, separate note: this throws `InvalidOperationException` (LINQ `.First()`) on a miss, rather than the `DomainException` the rest of the codebase's domain-layer failures consistently use. Not a bug in practice (it's only ever called with an id known to exist), but worth flagging since it's an inconsistent failure-mode choice sitting in a codebase that's otherwise disciplined about exception typing (per the original report's §6 note that all 21 non-test `catch` sites use `DomainException`/`DnaDeserializationException` — this `.First()` is technically a 22nd failure mode, just one that's never been observed to fire).

---

## Summary table

| # | Finding | Type | Recommendation |
|---|---|---|---|
| 1 | `CreatureEditorWindow.cs`: ~75% of its 2850 lines cluster into 4 extractable, minimally-coupled responsibilities (viewport handles, inspector, part tree, preview pipeline) | God class, concrete decomposition | 5-step extraction order given above; no new interfaces needed |
| 2a | `CurrentBodySpacing` re-derives `ResolvedBody.TotalLength` math locally | Duplication, same pattern as delta audit #3 | Fold into `ResolvedBody`/`ResolvedPolyline` consumption when CC-086 reaches the editor layer |
| 2b | `FindBodySample` throws `InvalidOperationException` via LINQ `.First()`, the one place that doesn't use `DomainException` | Minor consistency gap | Low priority; note for whoever next touches this method |
