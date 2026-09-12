# CreatureCreator — Editor Architecture & Authoring Audit

**Report ID:** `CC-AUDIT-EDITOR-20260912-4E52D8B7`
**Suite:** `CC-AUDIT-SUITE-20260912-9F42C6D1`
**Branch base audited:** `dc7a7a685d141122e1cc25bef202883b0cbbd466`
**Primary source:** `Assets/Scripts/Editor/CreatureEditorWindow.cs`, plus editor authoring helpers, preview controller, task records CC/TSK lineage, ADR-006
**Unity execution:** unavailable

## Executive assessment

The editor has made real architectural progress. The single mutation funnel, clone-before-mutate model, Undo integration, transient body/limb gestures, stale-preview warning for placement, structural preview recovery, and presentation-only tree state are all strong patterns.

The remaining risk is that `CreatureEditorWindow` is becoming the policy nexus for authoring, persistence, regeneration, preview lifecycle, SceneView input, configuration, and visual diagnostics. The class currently works as a coordinator, but continuing to add features here will turn each new gesture or panel into another cross-cutting state machine.

The second major issue is **stale or mixed-time information**: definition state, last regenerated mesh, preview acceptance state, and user input can exist at different revisions. The editor has started adding fingerprints and HelpBoxes to address this; that should become a first-class revision model rather than a collection of individual stale-state flags.

## Findings

### ED-01 — `CreatureEditorWindow` is accumulating too many state domains

**Severity:** P2  
**Confidence:** 98%  
**Owner:** TSK-0098

The current class contains fields for:

- canonical DNA;
- validation;
- selected part and body sample;
- body drag state;
- body radius drag state;
- limb joint drag state;
- parts tree expansion;
- validation pane state;
- preview GameObject/controller;
- undo state;
- placement mode;
- placement feedback;
- placement drag;
- regeneration timing/configuration;
- skeleton display;
- diagnostics logging;
- file path/session state.

This is not merely a “large class” smell. These are distinct state machines with different lifetimes.

Recommended decomposition is by state ownership, not arbitrary UI widgets:

```text
CreatureAuthoringState
SceneGestureController
PreviewRequestController
PreviewPresentationController
EditorSessionState
```

Each can remain internal to the editor assembly.

### ED-02 — Continuous inspector drags still produce many Undo records

**Severity:** P2  
**Confidence:** 99%  
**Owner:** TSK-0098

The class explicitly documents that continuous inspector/part-handle edits route through `MutateDefinition` repeatedly, while body sample drags use one snapshot/one commit.

This creates inconsistent interaction semantics: two visually identical drags can have very different Undo behavior depending on the control used.

Adopt one drag-session abstraction for all continuous controls:

```text
BeginGesture
  → transient state
  → preview frames
  → CommitOnce / Cancel
```

Do not duplicate this logic per widget.

### ED-03 — Stale mesh placement is correctly blocked, but the revision concept is fragmented

**Severity:** P2  
**Confidence:** 97%  
**Owner:** TSK-0098 / preview lifecycle

The editor records a body fingerprint for the mesh used by placement and blocks placement when the live definition no longer matches. This is a good local fix.

The broader issue is that the editor has multiple revision dimensions:

- definition revision;
- last generated revision;
- preview acceptance revision;
- placement geometry revision;
- potentially skeleton/binding revision later.

These should become one immutable `AuthoringRevision` or monotonic generation token. Every generated/presented artifact should state exactly which revision produced it.

### ED-04 — Bounds semantics are internally inconsistent between local authoring and world visualization

**Severity:** P2  
**Confidence:** 95%  
**Owner:** morphology/authoring contract

The editor manipulates positions in creature/world space but commits parent-relative local positions and validates local bounds. The source explicitly notes that a child can appear outside the overall creature silhouette while still being inside its parent-relative local bounds.

That is not automatically wrong, but it is a specification hazard. Artists generally reason about the visible creature, not parent-local coordinate envelopes.

Either:

- define bounds as local transform bounds and expose that rule visibly; or
- validate against resolved creature-space position as a second authoring constraint.

Avoid changing this silently because it would alter serialized semantics.

### ED-05 — Placement raycasts against a MeshCollider tied to the last accepted preview

**Severity:** P2  
**Confidence:** 96%  
**Owner:** TSK-0098 / TSK-0104

The editor intentionally raycasts against generated preview geometry, not the live definition. This is sensible for accurate surface placement, and stale mismatches are now surfaced.

The missing acceptance rule is what happens when generation is in flight while the user is dragging. The placement system should capture the accepted preview revision at mouse-down and continue to use that immutable surface for the gesture. It should not switch colliders midway through a drag.

### ED-06 — Selection identity should remain definition-based, not GameObject-based

**Severity:** P2  
**Confidence:** 95%  
**Owner:** TSK-0098

The editor has correctly moved toward stable part IDs and a structural preview recovery model. Keep this direction.

SceneView selection should resolve back to stable authoring IDs rather than caching direct references to generated Transforms/GameObjects. Generated preview replacement is frequent; authoring selection must survive it.

### ED-07 — Config assignment path exists but project-asset assignment remains an evidence gap

**Severity:** P2  
**Confidence:** 92%  
**Owner:** TSK-0076 / editor config follow-up

The editor exposes a `CreatureGenerationConfig` object field and derives `EffectiveMeshPalette` from it. The runtime preview likewise reads the same shared config type. ADR-006 explicitly defines this as the accepted architecture.

The remaining issue is not source duplication; it is operational proof that a real project asset can be assigned, saved, reloaded, and then actually resolves the expected palette in both editor and Play Mode.

This should be tested as an asset fixture, not inferred from source.

### ED-08 — Presentation toggles and authoring settings have mixed persistence mechanisms

**Severity:** P3/P2  
**Confidence:** 96%  
**Owner:** TSK-0098

The class uses EditorPrefs, SessionState, serialized asset configuration, current-file persistence, and in-memory state for different settings.

Each mechanism can be justified, but the lifetime model should be documented in one table. The user-facing behavior should answer:

- survives window close?
- survives editor restart?
- survives domain reload?
- travels with creature file?
- machine-local only?

Without that, future settings will be assigned to whichever storage mechanism is easiest.

### ED-09 — Error presentation is becoming a second data pipeline

**Severity:** P2  
**Confidence:** 93%  
**Owner:** TSK-0098 / diagnostics

The editor formats generation diagnostics, validation results, placement failures, and stale-state feedback into UI-specific text.

Keep raw diagnostics machine-readable and presentation-independent. Every HelpBox/dialog should consume a stable diagnostic object rather than reinterpreting exceptions and ad-hoc strings.

### ED-10 — SceneView gesture behavior deserves a shared interaction contract

**Severity:** P2  
**Confidence:** 98%  
**Owner:** TSK-0098

Body, limb joint, body radius, and placement gestures now each have increasingly similar patterns: capture mouse-down state, preview without mutation, commit once, Esc cancels.

This is now a clear duplication pattern. It should be consolidated into a generic gesture/session utility with:

- captured authoring revision;
- start state;
- preview state;
- commit/cancel callbacks;
- Undo scope.

That is a high-confidence refactoring opportunity because the semantics are already intentionally identical.

### ED-11 — Regeneration debounce is a UI policy that should not leak into generation correctness

**Severity:** P2  
**Confidence:** 96%  
**Owner:** TSK-0098 / TSK-0104

The editor tracks `_autoRegenerateAt` and delay settings. This is a useful interactive policy but it must not become the only scheduler backpressure mechanism.

Editor debounce reduces request rate; scheduler bounding controls computational concurrency. They solve different problems and should remain separate.

### ED-12 — Skeleton overlay should remain a diagnostic projection, not a second skeleton model

**Severity:** P2  
**Confidence:** 97%  
**Owner:** TSK-0188 / editor decomposition

The read-only skeleton overlay is correctly described as a projection of the inferred rest skeleton. Maintain that invariant. It should consume the same immutable snapshot as rig construction rather than re-inferring or reconstructing a second representation.

This is especially important as posed visualization arrives: there must still be one skeleton authority with different projections for rest and posed state.

## Recommended decomposition order

1. Introduce a revision/token abstraction.
2. Extract a generic SceneView gesture session.
3. Extract preview request/lifecycle controller.
4. Extract editor session persistence.
5. Split remaining presentation panels only after state ownership has become explicit.

## Exclusions

Do not reopen the historical structural preview-root/name collision issue. The current recovery model intentionally uses a structural instance handle rather than adopting an arbitrary same-named GameObject.

## Conclusion

The editor is no longer suffering primarily from correctness mistakes; it is suffering from **state-space growth**. The next health improvement should make revisions, gesture lifetimes, and preview ownership explicit. That will make future Spore-like controls substantially easier to add without turning `CreatureEditorWindow` into an unmaintainable state machine.
