---
id: creature-task-005
key: CC-005
title: Add preview material and automatic regeneration settings
status: In Progress
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

## Acceptance Criteria
- Every generated preview has a non-null assigned material.
- The preview material is preserved after regeneration and reload.
- The editor shows an Auto checkbox beside Regenerate.
- Auto regeneration is disabled by default unless the existing workflow specifies otherwise.
- Automatic updates enforce a default minimum interval of one second.
- The interval can be changed in the non-creature settings area, with validation preventing values below the supported minimum.
- Mesh quality is configurable in the same settings area and is not stored as creature-specific DNA.
- Repeated edits during a pending update produce no overlapping generation jobs.
- Manual Regenerate remains available when Auto is disabled.

## Validation
- Static diagnostics report no errors for the changed editor and runtime files.
- Unity compilation completed without script errors before the bridge disconnected.
- Manual editor check confirms the material is visible on the generated mesh and Auto updates do not run more often than the configured interval.
- Play Mode smoke test confirms the shared runtime preview still renders with its assigned material.

## Findings
The shared runtime generator and editor preview path already exist. The material assignment must be explicit at the preview renderer boundary, while quality and regeneration timing belong to editor or generation settings, not creature DNA.

## Blockers
The Unity manual check and Play Mode smoke test remain pending because the Unity MCP bridge disconnected. The preview currently uses a generated built-in shader material when no material is assigned.

## Next Step
Reconnect Unity and manually verify material assignment, Auto throttling, preview quality, and Play Mode rendering.