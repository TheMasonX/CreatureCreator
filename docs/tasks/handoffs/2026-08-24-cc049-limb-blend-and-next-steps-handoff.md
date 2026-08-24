# Handoff: CC-049 limb blend + next-wave status (2026-08-24)

**Task:** CC-049 (remove limb geometry dependence on inert Shape blend state)
**Status:** Done — implemented and validated in the real Unity editor
**Owner:** Next implementation agent
**Date:** 2026-08-24
**Depends on:** CC-018 (done), CC-063 (done)
**Related:** CC-051, CC-007, CC-064, CC-065, CC-039, CC-056

## Summary

The 2026-08-24 external audit (fast-preview reassessment) was reviewed, verified
against HEAD `393087c`, and synthesized into the task plans. Then CC-049 was
implemented and validated end to end. The next wave is contract hardening:
CC-051 first, then CC-007, with CC-064 and CC-065 available as P1 backlog.

## What landed in this session

### Task-plan synthesis (docs only)

- New handoff `docs/tasks/handoffs/2026-08-24-audit-revision-fast-preview-and-contract-synthesis.md`:
  revised sequence, three-tier rendering model (proxy <16 ms -> Fast SDF ~100s ms ->
  Exact), canonical benchmark matrix, and the guardrail "every generated
  representation is downstream of one canonical semantic morphology resolution".
- New tickets: **CC-064** fast-mode non-finite (`+inf`) field contract (P1),
  **CC-065** FastNoise2 binary/submodule repository review gate (P1).
- Updated tickets: CC-051 (mandatory placement precedence table), CC-056 (split
  A/B/C), CC-062 (canonical benchmark matrix), CC-046 (instrumented probe),
  CC-057 (three-tier model).
- `active-tasks.md`: CC-022 -> Done, CC-049 -> Done, added CC-064/CC-065.

### CC-049 implementation

- `Runtime/Definition/LimbChain.cs`: new `BlendRadius` (default `DefaultBlendRadius`
  = 0.1f, matches `ShapeDefinition.DefaultSphere.SmoothBlendRadius`) and copied in
  `Clone()` (so `ClonePartAsChild`/duplication propagate it).
- `Runtime/Morphology/Sdf/SdfProgramBuilder.cs`: new `PartUnionBlendRadius(part)`
  (limbs -> `Limb.BlendRadius`; shape parts -> `Shape.SmoothBlendRadius`), used at
  the part-to-field union in **both** the managed and portable paths. No limb path
  reads the inert `Shape.SmoothBlendRadius` anymore.
- Serialization: additive `"blendRadius"` in the limbChain JSON (no version bump);
  reader defaults to `LimbChain.DefaultBlendRadius` when absent, so pre-CC-049
  files migrate byte-stably.
- `Runtime/Definition/DefinitionCanonicalizer.cs`: quantizes `BlendRadius`, throws
  on negative/non-finite.
- `Runtime/Definition/DefinitionValidator.cs` + `ValidationCode.cs`: new
  `ValidationCode.InvalidLimbBlendRadius` (report-only) for negative/non-finite.
- Editor UI for the new field deliberately deferred (folds into CC-039 authored-blend
  work).

## Validation evidence (real Unity editor, connected)

- New tests (all pass): `Compile_LimbField_IsIndependentOfInertShapeBlendRadius`
  (managed + portable), `Compile_LimbField_ChangesWithLimbChainBlendRadius`,
  `CompilePortable_ExplicitLimbBlend_AgreesWithManaged`,
  `RoundTrip_BlendRadius_IsPreserved`,
  `Deserialize_LimbChainWithoutBlendRadius_DefaultsToStandard`,
  `Validate_LimbWithNegative/NonFiniteBlendRadius_ReportsInvalidLimbBlendRadius`.
- Existing limb fixtures via execute_code: skeleton 11/11, sampler 8/8,
  SDF builder parity 4/4.
- EditMode suite 83/83; console clean (0 errors/warnings).
- Real `Assets/Creatures/dino_creature.json` (2 limbs + 2 mesh parts, no
  `blendRadius` in source): loads, canonicalizes, re-serializes byte-stable WITH
  `blendRadius` (additive migration); vpu-12 Exact generation watertight
  (10,626 tris / 5,315 verts, 0 non-manifold / 0 boundary).
- `ProjectSettings/EditorBuildSettings.asset` shows as modified in the worktree —
  that is an editor-session change, NOT part of this work. Leave it.

## Next steps (recommended order)

1. **CC-051** — record the placement precedence table in ADR-002, then implement the
   smallest shared resolver extension. Mandatory before CC-007. Table is already
   drafted in the CC-051 ticket. Do not let `Transform`, `ParentAttachment`,
   `BodySurfaceAnchor`, limb root/terminal sockets, `GeometryAttachment`, and
   `RigBinding` evolve independently.
2. **CC-007** — semantic `BodySurfaceProjector` hit-to-anchor projection against
   CC-051. Never persist triangle/vertex/world data as DNA.
3. **CC-064** — fast-mode `+inf` non-finite field contract: `+inf` = outside/culled,
   `NaN` always invalid, `-inf` invalid for sampling, finite = evaluated. Audit all
   consumers (appearance selection must treat `+inf` as "no candidate"). Add the
   documented contract + regression tests.
4. **CC-065** — HUMAN REVIEW REQUIRED: the FastNoise2 binary commit (`393087c`)
   followed a documented warning not to commit that dependency. Do not delete
   blindly; answer the 8 review questions in the ticket (license, runtime
   necessity, submodule duplication, platform set, setup-time generation).
5. Then CC-056A/B (ResolvedMorphology), CC-046 (instrumented broken-ankle probe),
   CC-050/052/053/055, CC-057 (interactive proxy on top of resolved morphology).

## Gotchas / lessons for the next agent

- Runtime test asmdef is NOT discovered by the MCP runner. Invoke fixtures directly
  via `execute_code` (assertions throw on failure); run `[SetUp]` manually for
  fixtures that initialize fields.
- `batch_execute` tool-name validation for `execute_code` is FLAKY: put `"tool"`
  BEFORE `"params"` and retry once. The `scripting_ext` tool group must be active —
  it resets to disabled after a domain reload; re-activate via
  `manage_tools(action="activate", group="scripting_ext")`.
- A test fixture with a private method named `LimbChain()` shadows the TYPE name —
  qualify as `ProceduralCreature.Definition.LimbChain.DefaultBlendRadius`.
- Wider smooth-min blends amplify the pre-existing `Mathf.Lerp` (`a+(b-a)h`) vs
  `math.lerp` (`b+(a-b)h`) rounding difference: default 0.1 blend is bit-identical,
  0.4 blend differs ~1.8e-4 at far points. Parity tests use 1e-3 with a comment;
  not a contract break.
- The on-disk `dino_creature.json` is untouched; it will pick up `blendRadius` on
  the next editor save (expected additive migration).
- Do not mutate the live `CreatureEditorWindow._definition` via reflection to stage
  a test creature (domain reload persists it into the session).

## Blockers / residual risk

- No implementation blocker for CC-051/007/064. CC-065 requires a human review
  decision before further work builds on the current repository state.
- No commit was made in this session; the worktree contains the CC-049 code +
  test + doc changes plus the unrelated `EditorBuildSettings.asset` editor change.
