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

- Unity editor version: `6000.0.35f1`.
- The project uses URP, Input System, and Unity Test Framework.
- Runtime tests are under `Assets/Scripts/Tests/Runtime`.
- Editor tests are under `Assets/Scripts/Tests/Editor`.
- Editor code references UnityEditor and must remain outside runtime assemblies.

## Procedure

1. Identify the changed assembly and nearest test fixture.
2. Run the narrowest matching Unity Test Framework test.
3. For editor changes, run the relevant EditMode test or perform the manual
   editor check in the actual Unity session.
4. For mesh changes, inspect triangle count, vertex welding, winding, and
   `MeshTopologyValidator` results.
5. For DNA changes, test validation, canonical ordering, and save-load-save
   byte stability.
6. For SDF changes, test signed distances, transforms, empty definitions, and
   deterministic composition.
7. For appearance changes, test deterministic noise, normal generation, part
   selection, and color bounds.
8. For skeleton or IK changes, test parent links, symmetry, root pinning, link
   lengths, and non-mutation of input poses.
9. Record the exact check, result, and environment in the task tracker.

Before relying on an editor result, confirm that compilation has finished and
inspect the Unity console for errors and warnings. Record the Unity version,
test mode, selected test or manual action, and any unavailable validation.
Treat a successful tool call as an operation result, not proof that the editor
is ready or that the behavior is correct.

## When Unity is unavailable

Do not invent a successful Unity result. State that Unity execution is
unavailable. Use only applicable alternatives, such as reviewing assembly
definitions, checking source references, or running an available project
validation command. Mark the task incomplete when the requested behavior needs
runtime or editor evidence.

## Completion criteria

Validation is complete only when the focused behavior passes, relevant broader
tests pass when available, and any Unity-only manual checks are recorded.