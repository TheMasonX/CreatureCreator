# MCP Surface Observed in This Workspace

This file is a snapshot, not a contract. Tool availability depends on the MCP for
Unity version, the connected session, and the active tool groups. If a name is
absent, activate its group or re-read the live tool list.

## Resource URIs

| URI | Use |
| --- | --- |
| `mcpforunity://instances` | List connected editors (`Name@hash`) |
| `mcpforunity://editor/state` | Readiness: `data.advice.ready_for_tools`, `data.compilation.is_compiling`, blocking reasons |
| `mcpforunity://project/info` | Project root, packages, pipeline, input and UI availability |
| `mcpforunity://project/tags` | Available tags |
| `mcpforunity://tests` | Discoverable tests |
| `mcpforunity://menu-items` | Menu items for `execute_menu_item` |
| `mcpforunity://scene/gameobject-api` | GameObject query API |
| `mcpforunity://scene/gameobject/{id}` | Full GameObject data |
| `mcpforunity://scene/gameobject/{id}/components` | Component data |
| `mcpforunity://scene/cameras` | Camera inventory |
| `mcpforunity://scene/volumes` | Volume inventory |
| `mcpforunity://rendering/stats` | Rendering counters |
| `mcpforunity://pipeline/renderer-features` | URP renderer features |

Resource payloads are wrapped: the content is under a top-level `data` object,
so field paths are `data.<section>.<field>`.

## Tool groups and activation

Inspect the authoritative group state with `manage_tools(action="list_groups")`.
Toggle with `manage_tools(action="activate"|"deactivate", group="<name>")`.
Observed 2026-09-12 on MCP for Unity 10.1.2:

| Group | Default | Tools |
| --- | --- | --- |
| `core` | on | `batch_execute`, `execute_menu_item`, `find_gameobjects`, `find_in_file`, `manage_asset`, `manage_build`, `manage_camera`, `manage_components`, `manage_editor`, `manage_gameobject`, `manage_graphics`, `manage_material`, `manage_packages`, `manage_physics`, `manage_prefabs`, `manage_scene`, `refresh_unity`, `apply_text_edits`, `create_script`, `delete_script`, `validate_script`, `manage_script`, `get_sha`, `read_console`, `script_apply_edits` |
| `scripting_ext` | off | `execute_code`, `manage_scriptable_object` |
| `testing` | off | `run_tests`, `get_test_job` |
| `docs` | off | `unity_docs`, `unity_reflect` |
| `probuilder` | off | `manage_probuilder` |
| `profiling` | off | `manage_profiler` |
| `vfx` | off | `manage_shader`, `manage_texture`, `manage_vfx` |
| `ui` | off | `manage_ui` |
| `animation` | off | `manage_animation` |
| `asset_gen` | off | `generate_audio`, `generate_image`, `generate_model`, `import_model`, `import_model_file` |

Group state is Unity-side and resets to the defaults above after a domain
reload, so `scripting_ext`, `testing`, and `docs` must be re-activated in a new
session.

### Activating a group costs a reload

Activation has been observed to trigger a forced synchronous recompile and a
domain reload. The bridge then drops for tens of seconds. Symptoms in order:
`No Unity Editor instances found`, then `Unity is reloading; please retry`, then
normal service. Activate every group you need once, early, and retry rather than
re-activating.

### Client-side gating is separate

In VS Code, tools can also be gated by client-side activators named
`activate_*` (for example `activate_unity_editor_management_toolkit`). Those
change which tools the agent can see; they are not the Unity-side group state.
Both exist in this workspace, so the upstream `manage_tools` guidance is valid
here.

## `manage_script_capabilities` output (observed 2026-09-12, MCP server 10.1.2)

```json
{
  "ops": ["replace_class", "delete_class", "replace_method", "delete_method",
          "insert_method", "anchor_insert", "anchor_delete", "anchor_replace"],
  "text_ops": ["replace_range", "regex_replace", "prepend", "append"],
  "max_edit_payload_bytes": 262144,
  "guards": { "using_guard": true },
  "extras": { "get_sha": true }
}
```

## Payload and paging rules

- Follow `next_cursor` until null; keep `page_size` modest, for example 50 for
  hierarchies and 10-25 for component lists.
- Start component queries with `include_properties=false`.
- Keep `generate_preview=false` on asset searches unless a thumbnail is needed.
- Screenshots: `include_image=true` only when the image must be read; cap
  `max_resolution` at 256-512.

## `execute_code` compiles with warnings as errors

Obsolete APIs fail the call outright. Observed: `GetInstanceID()` returned
`'Object.GetInstanceID()' is obsolete: 'Use GetEntityId instead.'` and the whole
snippet failed to run. Use current APIs such as `Object.FindObjectsByType` and
avoid obsolete members in `execute_code` snippets.

## Screenshot framing

`screenshot_multiview` and `screenshot` with `batch="surround"` auto-frame from
`view_target` and were observed to ignore `orbit_distance` and
`orbit_elevations`. Use `view_position` plus `view_target` when an exact angle
matters.

## Transient bridge errors

These appear during a reload or while Unity is busy, and clear on retry. They
are logged in the console as warnings, not errors.

| Message | Meaning |
| --- | --- |
| `No Unity Editor instances found` | Bridge is re-registering after a reload |
| `Unity is reloading; please retry` | Assembly reload or import in progress |
| `Timeout receiving Unity response` | Unity busy, for example indexation |
| `Command TCS timed out (N consecutive)` | Console warning for the above |

## Hierarchy quirk

`manage_scene(action="get_hierarchy", parent=<id>)` failed with
`Could not connect to Unity` for the negative instance IDs that preview objects
carry. Query those roots through the full hierarchy or `execute_code` instead.

## Editor preview and skinning inspection

- `CreaturePreviewController` owns its preview root through `SessionState`, not
  by name: `ProceduralCreature.Editor.Preview.RootEntityId` and
  `...GeometryEntityIds`. A root left in the scene by an earlier editor session
  is never adopted, modified, or destroyed, so stale `CreatureCreator Preview*`
  roots accumulate. Compare a root's `GetEntityId()` against the session key
  before deleting anything, and expect the orphan to hold a doubled rig.
- Regeneration is UI-only. `CreatureEditorWindow` has a `Regenerate Preview`
  toolbar button and auto-regenerates after definition edits
  (`ScheduleAutoRegeneration` then `ProcessAutoRegeneration`). There is no menu
  item, so an agent cannot trigger it; ask the user to press the button.
- The preview root is persisted in the saved scene, but its `CreatureRig`
  bindings are runtime-only. After a domain reload `rig.Bones.Count == 0` and
  `rig.RestSkeleton == null` until the preview regenerates.
- `RigSkinningSweepDiagnostic` (`Tools/Creature Creator/Animation/Skinning
  Sweep Selected Rig`) validates on `RestSkeleton != null &&
  IndexedBones.Count > 0`, so it is disabled while the rig is unbuilt and
  `EditorApplication.ExecuteMenuItem` returns `false`.
- To pose without a built rig, write `SkinnedMeshRenderer.bones[i].rotation`
  directly. Those transforms are the live deformation drivers, and the sweep
  diagnostic poses the rig the same way.
- Measure deformation with `SkinnedMeshRenderer.BakeMesh(mesh)` and compare
  vertex arrays. Snapshot and restore a pose across `execute_code` calls through
  `SessionState`; an euler round-trip restored with 0.0000 degrees of deviation.

## Console resets

A domain reload clears the Unity console. An empty console is not evidence of a
clean compile. Check `Logs/Editor.log` or re-run the check after readiness.

## Drift from the upstream operator guide

The upstream `unity-mcp-skill` text is accurate for this workspace: both
`manage_tools(action="activate", group=...)` and the direct tools exist. Its
main gap is that activation triggers a domain reload, and that `execute_code`
rejects obsolete APIs. Prefer the live tool list and `list_groups` over any
written list when they disagree.

## Package source

`Packages/manifest.json` pins `com.coplaydev.unity-mcp` to
`https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`. The upstream
repository ships a `mcp-source` skill that switches this value between `main`
(stable), `beta` (pre-release), a remote branch, or a local `file:` checkout.
Changing it forces a package re-resolve; only change it to test a specific
server build, and record the old value first.
