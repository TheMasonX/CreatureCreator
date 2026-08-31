---
id: creature-task-005
key: CC-005
title: Add preview material and automatic regeneration settings
status: Done
type: Task
priority: P1
tags: [editor, preview, materials, settings, performance]
dependsOn: [CC-002]
related: [CC-004]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Scripts/Runtime/Definition/GenerationSettings.cs
---

## Summary
Make generated previews render with an assigned material and add controlled automatic regeneration.

## Scope
Assign a material to the generated preview renderer and use it for every regenerated preview. Add an Auto checkbox beside Regenerate. When Auto is enabled, changes schedule regeneration with a configurable delay. The default delay is at least one second between updates. Add a settings area for non-creature-specific options, including mesh quality and automatic regeneration rate. Keep generation requests serialized or coalesced so repeated edits cannot overload the editor.

Add a preview material picker in the same settings area. When set, the picker is authoritative for the preview renderer. When unset, the default preview material prefers the URP lit shader, then Standard, then Unlit/Color, because Standard does not render reliably under URP.

## Acceptance Criteria
- Every generated preview has a non-null assigned material.
- The preview material is preserved after regeneration and reload.
- The editor shows a preview material picker in the non-creature settings area; an assigned picker material applies immediately and survives regeneration and reload.
- The default preview material prefers the URP lit shader (Standard does not render reliably under URP).
- The editor shows an Auto checkbox beside Regenerate.
- Auto regeneration is disabled by default unless the existing workflow specifies otherwise.
- Automatic updates enforce a default minimum interval of one second.
- The interval can be changed in the non-creature settings area, with validation preventing values below the supported minimum.
- Mesh quality is configurable in the same settings area and is not stored as creature-specific DNA.
- Repeated edits during a pending update produce no overlapping generation jobs.
- Manual Regenerate remains available when Auto is disabled.

## Validation
- Static diagnostics report no errors for the changed editor and runtime files.
- Unity compilation completed without script errors; `_previewMaterial` and `ResolvePreviewMaterial` present on the compiled type.
- EditMode tests `ProceduralCreature.Tests.Editor` passed 38/38, including `CreatureUndoStateTests.OnUndoRedoPerformed_SchedulesAutoRegenerationWhenEnabled` (undo re-arms the timer).
- Manual editor check (2026-08-23, Unity 6000.0.35f1): default preview material resolves to `Universal Render Pipeline/Lit`; preview regenerated with a non-null URP Lit material (20,624 triangles); the picker resolves to an assigned material; Auto arms a regeneration with the default 1.00s delay; the preview material survives a Play Mode cycle.
- Play Mode smoke test on `CreatureCreatorTestScene`: `[CreatureCreator] Runtime preview generated: 14776 triangles.` with a non-null URP Lit material on the Preview Anchor renderer.
- Console shows no script errors or warnings after the changes.

## Findings
The shared runtime generator and editor preview path already exist. The material assignment must be explicit at the preview renderer boundary, while quality and regeneration timing belong to editor or generation settings, not creature DNA.

A regression was identified in the editor loop: undo and redo restored the creature definition but did not re-arm the pending auto-regeneration timer, so the editor could sit idle even while Auto remained enabled. The fix is localized to the native undo callback and is covered by a focused EditMode regression test for the undo path.

The default preview material falls back through Standard, then URP lit, then Unlit/Color. Under URP, Standard does not render reliably, so the order must prefer the URP lit shader. A dedicated picker makes the material an explicit non-creature editor setting (stored by asset path in EditorPrefs). The baked vertex colors currently only tint the base color; a later step is a lit shader that uses vertex color as extra data (tracked as a follow-up, out of scope for this ticket).

## Blockers
No code or validation blockers. The Unity MCP bridge is reconnected and all automated and scripted manual checks pass. The user visually confirmed the preview material renders correctly on 2026-08-23.

## Next Step
Complete. User confirmed the preview material renders correctly in the editor on 2026-08-23. The vertex-color lit shader follow-up (baked vertex colors used as extra data, currently only a tint) is captured as CC-024.