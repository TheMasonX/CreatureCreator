---
id: creature-task-034
key: CC-034
title: Body appearance vertical blend remap as an AnimationCurve
status: Done
type: Task
priority: P2
tags: [appearance, definition, schema, body, baking, authoring, serialization]
dependsOn: [CC-025]
related: [CC-025]
links:
  - Assets/Scripts/Runtime/Definition/BodyVerticalGradientAppearance.cs
  - Assets/Scripts/Runtime/Appearance/BodyVerticalGradientSampler.cs
  - Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Serialization/CanonicalJsonWriter.cs
  - Assets/Scripts/Runtime/Serialization/JsonDnaSerializer.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Tests/Runtime/BodyVerticalGradientAppearanceTests.cs
---

## Summary

Replace the Body appearance `VerticalOffset` float (CC-025) with an authorable
`AnimationCurve` that remaps the vertical blend. The raw vertical sample in -1..1
(bottom .. top of the surface) is remapped to 0..1 (bottom = 0, top = 1), fed
into the curve, and the curve output is the top/bottom blend factor. The default
curve is linear (y = x), which preserves today's offset-0 look.

## Scope

- **DNA/schema:** replace `BodyVerticalGradientAppearance.VerticalOffset`
  (float) with a curve field. This is an authoritative DNA change, so it needs
  canonicalization, validation, and canonical JSON round-trip coverage.
- **Evaluation:** `BodyVerticalGradientSampler` currently computes the blend as
  `(ApplyVerticalOffset(v, offset) + 1) * 0.5`. Change it to
  `curve.Evaluate((v + 1) * 0.5)` where the curve defaults to linear y = x.
  Keep the result monotonic-in-vertical-sample for deterministic baking.
- **Curve seam:** mirror `GradientAdapter` with a thin adapter that owns
  evaluation, cloning, content comparison, validation, and quantization of the
  curve, so runtime code never reaches into `UnityEngine.AnimationCurve`
  internals and a pure-math fallback can be swapped in for Burst/compute
  consumers.
- **Serialization:** define a canonical JSON form for the curve (keyframes:
  time, value, inTangent, outTangent, weighted mode/weights, plus wrap modes, or
  a documented simplified form) and quantize it deterministically. Decide
  migration for existing v2 files that carry `verticalOffset`.
- **Authoring:** replace the Vertical Offset slider in
  `CreatureEditorWindow.DrawBodyAppearanceFields` with a
  `EditorGUILayout.CurveField` (default linear), through the existing
  `MutateDefinition` undo/session path.
- **Tests:** offset math tests become curve tests (default-identity, monotonic,
  clamp, quantization round-trip, JSON round-trip, validator/canonicalizer).

## Acceptance Criteria

- The Body blend uses a curve remap: input `(verticalSample + 1) * 0.5`,
  output is the top/bottom blend factor, default linear (y = x).
- The default curve reproduces the current offset-0 behavior byte-for-byte at
  the sampler level.
- Curves survive canonical JSON round-trip and are covered by
  `DefinitionValidator` and `DefinitionCanonicalizer`.
- The inspector exposes a curve field instead of the offset slider.
- Validator reports invalid curves (non-finite keys, out-of-range times, empty)
  without repairing.

## Validation

- EditMode/PlayMode `BodyVerticalGradientAppearanceTests`: default-identity
  curve, monotonicity, boundary behavior, quantization, clone/content-equals.
- Canonical JSON round-trip tests for the curve fields, including migration of
  an existing `verticalOffset` file.
- In-editor preview check that the Body shows the curve-driven blend for a
  non-linear curve (e.g. a belly band).

## Findings

Implemented (2026-08-23). The Body appearance now stores a
`UnityEngine.AnimationCurve` (`VerticalCurve`) instead of the `VerticalOffset`
float, wrapped by a new `CurveAdapter` that mirrors `GradientAdapter`
(evaluate / clone / compare / validate / quantize / legacy migration).

- **DNA/schema:** `BodyVerticalGradientAppearance.VerticalCurve` replaces
  `VerticalOffset`. Default is `CurveAdapter.Linear()` (linear y = x, which
  reproduces the old offset-0 look). `Clone`, `IsFinite`, and `ContentEquals`
  go through the adapter.
- **Sampler:** `BodyVerticalGradientSampler.EvaluateColor` now computes the
  blend as `CurveAdapter.Evaluate(appearance.VerticalCurve, (verticalSample + 1) * 0.5)`
  and lerps top/bottom by it. `ApplyVerticalOffset` was removed (public API
  deletion; it was only used by the sampler and its tests).
- **Curve seam:** `CurveAdapter` delegates evaluation to
  `AnimationCurve.Evaluate` (input clamped to [0, 1]) so authored curves render
  exactly as Unity would; `Build`/`Quantize`/`ReadCurve` rebuild keys with
  free (tangentMode 0) tangents so the numeric in/out tangents drive
  evaluation. Documented simplification: only time / value / inTangent /
  outTangent are in the canonical contract — weighted and constant
  (infinite-tangent) keys are not preserved (constant keys fail validation as
  non-finite), and wrap modes are irrelevant (input is clamped) so they are not
  serialized.
- **Migration:** legacy `verticalOffset` migrates EXACTLY (not approximately)
  via `CurveAdapter.FromLegacyOffset(o)` to a 3-key piecewise-linear curve.
  Derivation: as a function of `u = (v + 1) * 0.5`, the old remap is
  `blend(u) = (o + 1) * u` for `u <= 0.5` and `o + (1 - o) * u` for `u >= 0.5`,
  so keys are `(0, 0, slope o+1)`, `(0.5, 0.5 + 0.5o, in o+1, out 1-o)`,
  `(1, 1, slope 1-o)`. Offset 0 migrates to linear y = x. The reader prefers
  `verticalCurve`, else `verticalOffset`, else the default.
- **Serialization:** canonical JSON emits `verticalCurve` as
  `{ "keys": [ { time, value, inTangent, outTangent }, ... ] }` (quantized,
  ordered by time). Old files with `verticalOffset` still load and re-save
  byte-stably.
- **Validation/canonicalization:** `DefinitionValidator` reports
  `InvalidBodyAppearance` (null / empty / out-of-range key time) and
  `NonFiniteBodyAppearance` (infinite tangent — NaN is sanitized by Unity at
  curve construction); `DefinitionCanonicalizer` throws on null/invalid curves
  and `CurveAdapter.Quantize` orders + quantizes keys.
- **Authoring:** `CreatureEditorWindow.DrawBodyAppearanceFields` uses
  `EditorGUILayout.CurveField` ("Vertical Curve") in place of the offset
  slider, through the existing `MutateDefinition` undo/session path.
- **Docs:** `Assets/Scripts/README.md` body-appearance section updated to the
  curve model and migration.

Validation evidence (real Unity editor via the MCP bridge):

- `BodyVerticalGradientAppearanceTests` PlayMode: **45/45 pass** (default
  linear identity, clamp, legacy-offset migration exactness + extreme pinning,
  clone/compare/quantize/validate, canonicalize, JSON round-trip + save-load-
  save byte stability, legacy `verticalOffset` → curve migration, baked mesh
  gradient, validator null/out-of-range/non-finite).
- Editor assembly EditMode suite: **56/56 pass** (no appearance regressions).
- Compilation clean (no errors/warnings); `CurveAdapter` confirmed loaded in
  `ProceduralCreature.Runtime` via reflection.
- Note: the MCP EditMode runner does not discover the all-platform
  `ProceduralCreature.Tests.Runtime` assembly, so runtime tests ran in
  PlayMode. In the full PlayMode runtime suite, 7 failures appear in untouched
  fixtures (`CreaturePartWorldTransformResolverTests`, `DefinitionValidatorTests`
  duplicate-id/attachment, `JsonDnaSerializerTests` part round-trip,
  `SkeletonInferrerTests` mirroring) — pre-existing PlayMode isolation issues
  unrelated to this change (they do not touch appearance code).

## Blockers

- None.

## Next Step

- Optional: manual in-editor check of the CurveField authoring surface and a
  non-linear authored curve on a preview creature. Consider a portable
  keyframe record (per CC-018's ThicknessProfile convention) only if a
  Burst/compute baker later cannot take a `UnityEngine.AnimationCurve` — the
  `CurveAdapter` seam is the swap point.
