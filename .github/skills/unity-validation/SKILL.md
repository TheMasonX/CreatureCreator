---
name: unity-validation
description: |
   Validate CreatureCreator changes in Unity. Use for Unity C# compilation,
   EditMode and PlayMode tests, assembly definitions, editor scripts, procedural
   mesh topology, deterministic DNA serialization, SDF generation, appearance
   baking, skeleton inference, and IK.
argument-hint: "Name the changed slice, focused test, Unity check, or known environment limitation"
---

# Unity Validation

## Outcome

Produce evidence that matches the changed behavior. Prefer the real Unity
editor and Unity Test Framework over source inspection.

## Project facts

- Unity editor version: `6000.5.9f1`.
- The project uses URP, Input System, and Unity Test Framework.
- Runtime tests are under `Assets/Scripts/Tests/Runtime`.
- Editor tests are under `Assets/Scripts/Tests/Editor`.
- Editor code references UnityEditor and must remain outside runtime assemblies.

## Procedure

1. Identify the changed assembly and nearest test fixture.
2. Before running the focused test, confirm the whole solution/project compiles. A narrow test proves the targeted behavior; it does not prove adjacent files still reference this one correctly.
3. Run the narrowest matching Unity Test Framework test.
4. For editor changes, run the relevant EditMode test or perform the manual
   editor check in the actual Unity session.
5. For mesh changes, inspect triangle count, vertex welding, winding, and
   `MeshTopologyValidator` results.
6. For DNA changes, test validation, canonical ordering, and save-load-save
   byte stability.
7. For SDF changes, test signed distances, transforms, empty definitions, and
   deterministic composition.
8. For appearance changes, test deterministic noise, normal generation, part
   selection, and color bounds.
9. For skeleton or IK changes, test parent links, symmetry, root pinning, link
   lengths, and non-mutation of input poses.
10. Record the exact check, result, and environment in the task tracker.

Before relying on an editor result, confirm that compilation has finished and
inspect the Unity console for errors and warnings. Record the Unity version,
test mode, selected test or manual action, and any unavailable validation.
Treat a successful tool call as an operation result, not proof that the editor
is ready or that the behavior is correct.

Before trusting new debug or visualization tooling as evidence for diagnosing a
different bug, validate the tool itself against a posed, non-identity state.
`RigDebugView` mixed rest-space and posed-space coordinates during the
shoulder-pinch investigation, which confounded the screenshots used as
diagnostic evidence.

## When Unity is unavailable

Do not invent a successful Unity result. State that Unity execution is
unavailable. Use only applicable alternatives, such as reviewing assembly
definitions, checking source references, or running an available project
validation command. Mark the task incomplete when the requested behavior needs
runtime or editor evidence.

## Completion criteria

Validation is complete only when the focused behavior passes, relevant broader
tests pass when available, and any Unity-only manual checks are recorded.