---
name: unity-mcp-operator
description: |
  Operate the Unity Editor through the MCP for Unity server (unitymcp/* tools).
  Use when creating or editing GameObjects, components, prefabs, scenes,
  materials, or assets through MCP; when driving C# scripts with script_apply_edits
  or text edits; when reading editor state, the Unity console, or MCP resources;
  when running EditMode or PlayMode tests through MCP; when a tool reports that
  its group is disabled; or when an MCP call times out, returns stale data, or
  fails validation.
argument-hint: 'Unity action, target object or asset, readiness concern, and evidence needed'
user-invocable: false
disable-model-invocation: false
---

# Unity MCP Operator

## Outcome

Perform Unity Editor actions through MCP safely and cheaply: read state before
mutating, batch independent calls, verify with the console and screenshots, and
never claim Unity behavior without an operation result that proves it.

## Scope

- This skill owns MCP call mechanics: readiness, resources, tool groups,
  batching, payload limits, pagination, and error recovery.
- [unity-validation](../unity-validation/SKILL.md) owns the evidence a change
  needs. Read it before claiming validation is complete.
- Never invent a tool or resource name. Verify against the live tool list and
  [references/mcp-surface.md](./references/mcp-surface.md).

## Readiness gate

Complete these checks before the first mutation in a session.

1. Confirm the server is reachable, for example with
   `read_console(action="get", count=5)`. The console banner records the session
   and server version, for example
   `MCP-FOR-UNITY: StdioBridgeHost started on port 6401. (OS=WindowsEditor, server=10.1.2)`.
2. Read `mcpforunity://editor/state` and proceed only when
   `data.advice.ready_for_tools` is true. Do not start work while
   `data.compilation.is_compiling` is true.
3. Read `mcpforunity://instances`. When more than one editor is connected, pin
   the target with `set_active_instance` first.
4. Read `mcpforunity://project/info` before UI, package, input, or render work,
   so package availability and pipeline are known.

A successful tool call is one operation result. It does not prove that the
editor is ready, that a domain reload finished, or that behavior is correct.

## Resource-first loop

1. Read the resources for the target area before calling a tool.
2. Locate objects with `find_gameobjects`, which returns IDs only, then read the
   per-object resource for full data.
3. Mutate with the narrowest tool that changes only the intended state.
4. Verify with `read_console` and, for visual changes, a screenshot.
5. Record the action and the evidence in the MemorySmith task.

## Tool groups

The server exposes tools in groups. Inspect them with
`manage_tools(action="list_groups")` and toggle with
`manage_tools(action="activate", group="<name>")`. Only `core` is on by
default. `scripting_ext`, `testing`, `docs`, `probuilder`, `profiling`, `vfx`,
`ui`, `animation`, and `asset_gen` start off and reset to off after a domain
reload. The verified group table is in
[references/mcp-surface.md](./references/mcp-surface.md).

Activating a group has been observed to trigger a forced recompile and a domain
reload, which drops the bridge for tens of seconds. Activate every group you need
once, early, then retry instead of re-activating.

A reload can also switch exposed groups back off mid-session. Calls then fail
with `... is currently disabled by the user`. Re-run the client-side activator
for that group, then retry; a plain retry does not recover it.

In VS Code, client-side `activate_*` tools expose a whole tool family. One
example is `activate_unity_editor_management_toolkit`. These tools report which
tools they switched on. They are separate from the Unity-side group state that
`manage_tools` reports. Observed 2026-09-13 on server 10.2.0: `list_groups`
reported `probuilder` as `enabled: false` while `manage_probuilder` still
executed. A disabled flag did not block a call once the tool was exposed to the
client.

## Batch and payload discipline

- Use `batch_execute` for repeated or independent operations. It is far cheaper
  than sequential calls. The default cap is 25 commands per batch; 24-command
  batches ran reliably.
- Put `"tool"` before `"params"` in every batch command. Name validation has
  been observed to fail flakily on valid commands otherwise; retry once on
  failure. See
  `docs/tasks/handoffs/2026-08-24-cc049-limb-blend-and-next-steps-handoff.md`.
- A batch can abort mid-flight with `No Unity Editor instances found` when a
  reload starts. Retry the whole batch, then verify what actually applied rather
  than assuming a partial write.
- Request metadata before properties. Never request a full property payload,
  preview, or thumbnail unless the task needs it.
- Prefer a summary action before a detail action, and keep `page_size` small.

## Pagination

Large queries return a cursor. Follow `next_cursor` until it is null. Do not
raise `page_size` to a large value to avoid pagination.

## Visual verification

Use screenshots to confirm a visual result, not to discover it. Cap
`max_resolution` at 256-512 so the image stays cheap to read.

Framing rules, all observed on server 10.2.0:

- A single `screenshot` accepts `view_target` as a coordinate array. A
  `screenshot_multiview` batch capture does not. It fails with
  `view_target '[0, 1.8, 0]' not found for batch capture` and needs a GameObject.
- After a batch capture, an array `view_target` stops resolving for later single
  `screenshot` calls in the same session. Pass `view_position` plus
  `view_rotation` instead, and compute the euler angles from the position and the
  desired target.
- `screenshot_multiview` auto-frames on the whole scene bounds and reports the
  radius it chose. Inside an enclosed interior those bounds are wider than the
  room. The four horizontal frames then sit outside the walls and render
  blocked, so only the top and bird's-eye frames stay usable. Capture interior
  angles one at a time with `view_position`.
- A yaw of 180 degrees mirrors the screen-space X axis, so positive world X
  appears on the left. Confirm the axis before reading placement from an image.
- When the requested file name already exists, the tool writes a `_1` suffix
  instead of overwriting.

## Scene construction with ProBuilder

`manage_probuilder` creates and edits ProBuilder meshes. Shape parameters and
recipes are in
[references/probuilder-and-materials.md](./references/probuilder-and-materials.md).
Four facts cause most of the rework:

- Shape creation uses a **centre pivot**: a cylinder of height `h` at position
  `y` spans `y-h/2 .. y+h/2`. To put a disc top at `Y=0`, place it at `y = -h/2`.
- A new shape has **no material** and renders magenta until `set_face_material`
  runs.
- A new shape has **no collider**. Add one with
  `manage_components(action="add", component_type="MeshCollider")` when the
  surface must be walkable.
- `manage_probuilder` reads its arguments from the `properties` bag.
  `set_face_material` with no `faceIndices` assigns every face.

## Materials, emission, and transparency

- `manage_material(action="create")` takes `material_path`, `shader`, and
  `color`.
- `manage_material` cannot enable a shader keyword. To make a surface emissive,
  enable `_EMISSION` and write an HDR `_EmissionColor` through `execute_code`.
- Emissive materials bloom only with a Bloom volume. Build one with
  `volume_create_profile`, then `volume_create`, then `volume_add_effect`.
  `volume_set_effect` requires a `parameters` dict; `properties` is rejected.
- URP/Lit alpha blending needs a property bundle, not just an alpha colour. The
  exact bundle is in the reference file.

## Verification recipes

Run these through `execute_code`. Each one caught a real fault during the
Test-scene lab work.

- **Bounds readback.** Confirm a placement assumption before building on it.
  Print `Renderer.bounds` per object and compare against the intended extents.
- **Intersection scan.** `Bounds.Intersects` between two named objects finds a
  prop that overlaps another. This located a tank that physically intersected a
  console, which a user reported as "stuck behind stuff".
- **Null-material scan.** After a bulk create, count renderers whose
  `sharedMaterial` is null. A non-zero count is the magenta objects.
- **Feet-on-surface check.** Do not trust `SkinnedMeshRenderer.bounds` for a
  generated or posed creature: it reported bind-pose extents. Read
  `MeshFilter.sharedMesh.bounds` or `MeshCollider.bounds` instead.

## Console and compilation

- After any script or asset change, read the console and fix errors before
  continuing. A new type is unusable until compilation succeeds.
- After `script_apply_edits`, text edits, or script creation, do not add a
  redundant refresh. The editing tools request import and compilation. Use
  `refresh_unity` when a file changed outside those tools, such as after a git
  operation or a generated file, and wait for readiness before the next call.
- Call `manage_script_capabilities` before composing a script edit. It reports
  the supported structured ops and text ops, the maximum edit payload, whether a
  using-guard is active, and whether `get_sha` is available. Prefer the
  structured ops over whole-file rewrites, and use the SHA guard to detect a
  file that changed since it was read.

## Multi-instance routing

`set_active_instance` pins the whole session to one editor. Pass
`unity_instance` on a single call to route only that call. Never let two editors
receive the same mutation.

## Error recovery

| Symptom | Likely cause | Action |
| --- | --- | --- |
| Tool missing or reports disabled | Group not activated, or reset by a domain reload | Re-run the `activate_*` tool, then retry |
| `... is currently disabled by the user` | A reload switched the exposed groups off mid-session | Re-run the `activate_*` tool; a plain retry does not recover it |
| `No Unity Editor instances found` | Bridge re-registering after a reload | Retry; do not re-activate groups |
| `Unity is reloading; please retry` | Assembly reload or import in progress | Retry once the reload finishes |
| `Timeout receiving Unity response` | Unity busy, for example search indexation | Retry the call |
| Busy or stale result | Compilation or domain reload in progress | Wait, then re-read editor state |
| `stale_file` | File changed since the recorded SHA | Re-read the SHA, then reapply the edit |
| Connection lost | Domain reload | Reconnect, then re-read readiness |
| Unexpected scene contents | Wrong editor instance | Check and set the active instance |
| Batch command rejected | Flaky name validation | Reorder to `"tool"` then `"params"`, retry once |

## CreatureCreator gotchas

- The MCP test runner does not discover the runtime test assembly
  (`ProceduralCreature.Tests.Runtime`). Invoke runtime fixtures through
  `execute_code` instead: assertions throw on failure, and call `[SetUp]`
  manually when the fixture initializes fields.
- `execute_code` lives in `scripting_ext`, which starts disabled and resets
after a domain reload. Activate it with
`manage_tools(action="activate", group="scripting_ext")`. Expect a recompile and
a bridge drop, then retry.
- `execute_code` compiles with warnings as errors, so obsolete APIs fail the
call before the snippet runs. `GetInstanceID()` reported
`'Object.GetInstanceID()' is obsolete: 'Use GetEntityId instead.'`. Prefer
`Object.FindObjectsByType` and other current APIs.
- `screenshot_multiview` auto-frames from `view_target` and was observed to
ignore `orbit_distance` and `orbit_elevations`. Use `view_position` with
  `view_rotation` for an exact angle: after a batch capture, an array
  `view_target` no longer resolves.
- A domain reload clears the Unity console, so an empty console is not evidence
of a clean compile. Read `Logs/Editor.log` or re-run the check after readiness.
- Creature preview roots and their rig bindings are editor session state, not
scene state. Read
[references/mcp-surface.md](./references/mcp-surface.md#editor-preview-and-skinning-inspection)
before deleting or posing a preview object.
- Never stage editor-session state through reflection. Mutating the live
  `CreatureEditorWindow` definition survives a domain reload and corrupts the
  session.
- Do not treat a preview or a scene mutation as validation of runtime generation
  behavior. Runtime proof comes from tests and output-fingerprint comparison.

## Boundaries

- Runtime code and `CreatureDefinition` stay authoritative. MCP calls change
  Unity state only.
- Do not persist scene, triangle, vertex, or world data into DNA.
- Do not commit, branch, or revert unrelated worktree changes as part of an MCP
  operation.
- ProBuilder meshes serialize into the scene file. Do not hand-edit a scene to
  change them, and expect `git diff --check` to flag trailing whitespace on the
  serializer's empty `m_Data` lines for ProBuilder components.
- Read [Assets/Scripts/README.md](../../../Assets/Scripts/README.md) before
  non-trivial editor work.

## Completion criteria

MCP work is complete when the intended state change is applied, the console
shows no new errors, visual results were checked when relevant, and the
MemorySmith task records the action, the evidence, and any Unity check that
could not run.
