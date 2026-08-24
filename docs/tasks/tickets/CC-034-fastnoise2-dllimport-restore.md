---
id: creature-task-034
key: CC-034
title: Resolve FastNoise2Bindings compile failure (restore DllImport P/Invoke)
status: Done
type: Task
priority: P1
tags: [submodule, build, bindings, p-invoke]
dependsOn: [CC-033]
related: [CC-023]
links:
  - Assets/Submodules/FastNoise2Bindings/CSharp/FastNoise2.cs
  - Assets/Submodules/FastNoise2Bindings/CSharp/FastNoiseNodeEditorIpc.cs
  - Assets/Submodules/FastNoise2Bindings/CSharp/test/BitmapGenerator.cs
  - .gitmodules
---

## Summary

The `FastNoise2Bindings` submodule was disabled with `#if false` wrappers
(committed as `ef03af7` "Apply local FastNoise2 build patches") because the
C# bindings did not compile in Unity. Resolve the root cause and remove the
temporary disabling.

## Root Cause

The fork commit `fae174e` (the parent-pinned submodule commit) rewrote the
bindings to use the .NET 7+ source-generated `LibraryImport` P/Invoke and
removed `using System;` and `using System.Collections.Generic;`. Unity Mono
does not support `LibraryImport`:

- `CS0122` - `LibraryImportAttribute` is internal in Unity's reference
  assemblies.
- `CS8795` - the `partial` methods have accessibility but no generated
  implementation.
- `CS0246` - missing `using System;` / `using System.Collections.Generic;`
  caused the `IDisposable`, `IntPtr`, `Span<>`, and `Dictionary<>` cascade.

The `#if false` wrapper was added to silence these errors instead of fixing
them.

## Scope

- Restore the three C# files from the upstream `DllImport`-based commit
  `7a8cda8` (the direct parent of the bad patch).
- Re-add the nullable pragma disables (`CS8603`/`CS8604`/`CS8618`, and
  `CS8625` for the IPC file) so Unity 6's nullable-on build stays clean.
- Remove all `#if false` / `#endif` wrappers and the `partial class` +
  `LibraryImport` changes.

## Acceptance Criteria

- `Assets/Submodules/FastNoise2Bindings/CSharp/*.cs` contains no `#if false`
  and no `LibraryImport`.
- Unity compiles the submodule with zero errors and zero warnings.
- `FastNoise` and `FastNoiseNodeEditorIpc` resolve as types in
  `Assembly-CSharp` with their full public API.

## Validation

- Unity MCP `read_console` after compile: 0 errors, 0 warnings.
- `unity_reflect` on `FastNoise`: found in `Assembly-CSharp`, methods include
  `GenUniformGrid2D/3D/4D`, `FromEncodedNodeTree`, `IDisposable`.
- `unity_reflect` on `FastNoiseNodeEditorIpc`: found in `Assembly-CSharp` with
  `IsAvailable`, `PollMessage`, `SendImportRequest`, `StartNodeEditor`.
- `git diff --check` in the submodule exits 0.

## Findings

- `NATIVE_LIB` constants resolve to `"FastNoise"` and `"NodeEditorIpc"` in
  their files.
- The submodule was amended: old `ef03af7` (the temporary `#if false`
  disabling) was rewritten to `32c2546` ("Restore classic DllImport P/Invoke
  for Unity"). Its parent `fae174e` never contained the `#if false`, so the
  disabling no longer exists in submodule history. The parent gitlink is
  bumped to `32c2546`.
- The duplicate native plugin error (same `FastNoise.dll` / `NodeEditorIpc.dll`
  in `Assets/Includes/FastNoise2/bin/` and the submodule `CSharp/test/`) was
  resolved by setting the submodule test DLLs' `PluginImporter` to exclude
  them from every platform (Editor disabled). These metas stay local because
  the submodule `.gitignore` ignores `*.meta`; they must be re-applied after a
  fresh clone, like the local `Assets/Includes` deployment.

## Blockers

None. Unity validation passed in the real editor (Unity 6000.5.9f1): zero
errors and warnings, `FastNoise` and `FastNoiseNodeEditorIpc` resolve in
`Assembly-CSharp`.

## Next Step

No push was performed (per user). The submodule amend and parent gitlink bump
are local. Before pushing the submodule, confirm whether the fork should keep
the C# restore (it reverts the author's WPF-oriented `LibraryImport` patch).
