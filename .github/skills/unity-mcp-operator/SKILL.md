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

## Batch and payload discipline

- Use `batch_execute` for repeated or independent operations. It is far cheaper
  than sequential calls. The default cap is 25 commands per batch.
- Put `"tool"` before `"params"` in every batch command. Name validation has
  been observed to fail flakily on valid commands otherwise; retry once on
  failure. See
  `docs/tasks/handoffs/2026-08-24-cc049-limb-blend-and-next-steps-handoff.md`.
- Request metadata before properties. Never request a full property payload,
  preview, or thumbnail unless the task needs it.
- Prefer a summary action before a detail action, and keep `page_size` small.

## Pagination

Large queries return a cursor. Follow `next_cursor` until it is null. Do not
raise `page_size` to a large value to avoid pagination.

## Visual verification

Use screenshots to confirm a visual result, not to discover it. Cap
`max_resolution` at 256-512 so the image stays cheap to read.

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
| Tool missing or reports disabled | Group not activated, or reset by a domain reload | Activate the group, retry once |
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
`view_target` for an exact angle.
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
- Read [Assets/Scripts/README.md](../../../Assets/Scripts/README.md) before
  non-trivial editor work.

## Completion criteria

MCP work is complete when the intended state change is applied, the console
shows no new errors, visual results were checked when relevant, and the
MemorySmith task records the action, the evidence, and any Unity check that
could not run.
