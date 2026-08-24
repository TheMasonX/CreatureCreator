---
id: creature-task-041
key: CC-041
title: Rotated-transform parity test for mirrored limb chains (managed vs portable)
status: Done
type: Task
priority: P2
tags: [runtime, sdf, limbs, symmetry, test-coverage]
dependsOn: [CC-018]
related: [CC-018, CC-014]
links:
  - Assets/Scripts/Tests/Runtime/SdfProgramBuilderLimbTests.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SymmetryNode.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs
  - docs/audits/creaturecreator-cc018-limb-chain-sdf-deep-dive-audit-26-08-23.md
---

## Summary

A codebase audit (docs/audits/creaturecreator-cc018-limb-chain-sdf-deep-dive-audit-26-08-23.md)
found that the only managed-vs-portable parity tests for mirrored limbs do not
cover a rotated part transform:

- `CompilePortable_MatchesManagedGraph_ForLimbChain` builds the part with
  `Transform = TransformData.Identity`.
- The X-offset regression test
  (`CompilePortable_MirroredLimbAtXOffset_AgreesWithManagedOnBothSides`) still
  uses `Rotation = Quaternion.identity`.

No test exercises a mirrored limb under a NON-IDENTITY (rotated) part transform —
the corner where the original per-ball local-X-negation bug was mathematically
wrong for any part whose composed local-to-creature frame does not commute with
the X flip.

The implementation is already correct at HEAD `ff0806d`:
`CompileLimbChainPortable` mirrors via
`mirroredPartMatrix = CreatureMirrorAcrossX * localToCreature` with the ORIGINAL
joint positions, which equals `S · (localToCreature · localPos)` — the same
creature-space reflection the managed `SymmetryNode` path produces for any
transform, rotated or not. This ticket is PURE TEST-COVERAGE: prove the rotated
case with a parity test so the corner stays locked against regression.

## Scope

- Add one runtime test to `SdfProgramBuilderLimbTests`, e.g.
  `CompilePortable_MirroredLimbWithRotatedPartTransform_AgreesWithManagedOnBothSides`.
- The part `Transform.Rotation` must be non-identity (e.g.
  `Quaternion.Euler(0f, 30f, 0f)`), so the composed local-to-creature frame does
  not commute with the X flip.
- Use a mirrored limb with a non-axis-aligned joint path so the rotation actually
  changes the composed ball positions.
- Assert `SdfProgramBuilder.Compile` (managed) equals
  `SdfProgramEvaluator.Evaluate(portable, ...)` within `1e-4` on BOTH the +X and
  -X sides across a sample grid, mirroring the existing regression-test pattern.
- Additive only; keep `CompilePortable_MirroredLimbAtXOffset_AgreesWithManagedOnBothSides`.

## Acceptance Criteria

- The new test passes (managed == portable within `1e-4`) for the rotated part.
- The test FAILS against the pre-fix local-X-negation implementation, proving it
  is a real guard and not a tautology (optionally verify by reverting the mirror
  line locally before finalizing).
- The full runtime limb SDF fixture still passes.

## Validation

- Runtime test assembly is not discovered by the MCP runner; invoke the new test
  via `execute_code` / PlayMode per repo convention, alongside the existing 5
  `SdfProgramBuilderLimbTests`.
- Confirm zero compile warnings after adding the test.
- PlayMode run on 2026-08-24 completed the selected SDF and skeleton fixtures. The
  rotated parity fixture passed. The run reported no failure in this ticket.

## Findings

- `CompileLimbChainPortable` (SdfProgramBuilder.cs) already emits the correct
  mirrored ball matrix `S · localToCreature`; no implementation change expected.
- The X-offset regression test covers translation off the X plane only; rotation
  remains uncovered at the time of writing.
- Added the requested rotated-transform managed-versus-portable SDF parity test.
- A separate skeleton issue was found during review. Mirrored limb forward and
  up axes now use a directly reflected proper rotation basis instead of
  extracting `Matrix4x4.rotation` from an improper reflected matrix. It is
  tracked separately as CC-071.

## Blockers

None for this test-coverage ticket. The runtime test runner can be slow to
initialize after a script refresh, so retry when a fresh PlayMode run is needed.

## Next Step

Continue with the remaining geometry-envelope and binding-contract work.
