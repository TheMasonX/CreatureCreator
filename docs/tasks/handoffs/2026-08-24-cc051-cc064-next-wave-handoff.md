# Handoff: CC-051 + CC-064 + CC-049 hardening (2026-08-24)

**Tasks:** CC-051 (canonical placement/attachment resolution), CC-064 (non-finite field
contract), CC-049 follow-up (canonicalizer blend tests)
**Status:** Done — implemented and validated in the real Unity editor
**Owner:** Next implementation agent
**Date:** 2026-08-24
**Depends on:** CC-049 (done), CC-063 (done)
**Related:** CC-007, CC-046, CC-052, CC-056, CC-062

## Summary

The next wave landed after the CC-049 audit: the architectural anchor CC-051 (precedence
table in ADR-002 + a single canonical part-frame resolver seam), CC-064 (the `+inf`
non-finite field contract documented and enforced at the appearance boundary), and the
CC-049 canonicalizer test gap. One pre-existing stale skeleton test assertion was also
corrected (evidence-backed). Commit made; see `git log`.

## What landed

### CC-051 (architectural anchor)

- `docs/adr/ADR-002-composable-geometry-and-generatedcreature-output.md` §7: the MANDATORY
  placement/attachment precedence table (position/orientation authority per situation) and
  the "exactly one path from semantic DNA to resolved world frame" rule.
- `CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace` is now THE canonical
  part-frame resolver (owns the parent-chain + limb child-at-tip composition).
  `ResolveLocalToCreatureSpace` is a delegating alias — all pre-existing call sites (SDF,
  skeleton, mesh generator, editor) converge on one path.
- Interim contract documented: `ParentAttachment` (`BodySurfaceAnchor`) is RESERVED-but-inert
  until CC-007; the resolver is the single seam CC-007 extends. No code reads anchor fields
  for placement except the resolver.
- Tests (3 new in `CreaturePartWorldTransformResolverTests`): single-canonical-path for a
  Body child, single-canonical-path at a limb tip, anchor-inert-until-CC-007.

### CC-064 (non-finite field contract)

- Contract documented on `SdfProgramEvaluator` (SdfProgram.cs), `DensityGrid`, and
  `CubeContourResolver.InterpolateEdge`: `+inf` = outside/culled/absent; `NaN` = always
  invalid; `-inf` = invalid for field sampling; finite = evaluated.
- `PartAppearanceSampler.Resolver.Resolve` hardened: `+inf` candidates are skipped (never win
  or poison the nearest-part decision), and the Body is selected only on a finite value.
  This fixes the case where every candidate culled (+inf everywhere) wrongly fell through to
  the Body's gradient color.
- Tests (4 new in `SdfNonFiniteFieldContractTests`): culled Fast sample reads exactly +inf;
  all-candidates-inf resolves to default (not Body gradient); +inf never beats a finite part;
  Fast grid corners are +inf while the finite minimum stays interior.

### CC-049 follow-up

- `JsonDnaSerializerLimbTests`: added `Canonicalize_ThrowsOnNegativeLimbBlendRadius`,
  `Canonicalize_ThrowsOnNonFiniteLimbBlendRadius`, `Canonicalize_QuantizesLimbBlendRadius`
  (the audit-flagged gap; all pass).

### Pre-existing fix (unrelated, evidence-backed)

- `SkeletonInferrerTests.Infer_MirroredChain_MirroredChildAttachesToMirroredParent` expected 6
  bones but the non-limb single-bone parts produce 5 (body + leg x2 + foot x2, verified with
  correct foot_mirror -> leg_mirror attachment). The stale expected count was corrected to 5.
  Not introduced by this wave; the runtime suite was never run by CI/MCP before.

## Validation evidence (real Unity editor, connected)

- Resolver: 3 new CC-051 tests + 9 regression tests pass (alias is exact).
- CC-064: 4 new contract tests pass; CC-063 Fast appearance/sample/mesh regressions 3/3.
- CC-049: 3 new canonicalizer tests + round-trip + legacy-default pass.
- Skeleton suites 19/19 pass (11 limb + 8 non-limb), all consuming the resolver.
- EditMode suite 83/83; console clean (0 errors/warnings).
- Real `Assets/Creatures/dino_creature.json` generates watertight with finite colors in both
  Exact and Fast modes (18,712 tris / 9,358 verts with placeholder mesh resolution).

## Next steps (recommended order)

1. **CC-007** — semantic `BodySurfaceProjector` hit-to-anchor projection for Body children,
   implemented as an extension of `ResolvePartFrameToCreatureSpace` (the one seam from
   CC-051). Never persist triangle/vertex/world data as DNA.
2. **CC-046** — instrumented broken-ankle probe (resolve joints, voxel bounds, local field,
   blend radius, components, non-manifold edges).
3. **CC-056A/B** — canonical `ResolvedMorphology` + attachment resolution (incremental).
4. CC-050/052/053/055, then CC-057 interactive proxy on top of resolved morphology.

## Gotchas / lessons

- Runtime asmdef is NOT discovered by the MCP runner; invoke fixtures via `execute_code`
  (assertions throw on failure). Run `[SetUp]` manually for fixtures that initialize fields
  (e.g. `JsonDnaSerializerLimbTests.SetUp()`).
- `execute_code` lives in the `scripting_ext` tool group, which resets to disabled after a
  domain reload — re-activate via `manage_tools(action="activate", group="scripting_ext")`.
- NUnit `Assert.AreEqual(Vector3, matrix.GetColumn(3))` FAILS (Vector4 vs Vector3). Always
  extract to `Vector3 pos = matrix.GetColumn(3)` first — the existing tests do this.
- `CreatureMeshGenerator.Generate` needs a `Func<string, Mesh>` mesh resolver for parts with
  `MeshGeometry`; use `Resources.GetBuiltinResource<Mesh>("Sphere.fbx")` for headless checks.
- The resolver refactor is behavior-preserving; `ResolveLocalToCreatureSpace` is now an alias
  of `ResolvePartFrameToCreatureSpace` and must remain so (CC-007 changes the seam, not the
  alias's meaning).
- Leave `ProjectSettings/EditorBuildSettings.asset` (editor-session change) alone; do not
  include it in commits.
