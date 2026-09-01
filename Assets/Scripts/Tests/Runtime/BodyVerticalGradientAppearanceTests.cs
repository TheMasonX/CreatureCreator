using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Appearance;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;
using ProceduralCreature.Serialization;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Tests for the Body vertical-gradient appearance model (CC-025/CC-034):
    /// Unity Gradient adapter behavior, the vertical-sample projection + the
    /// vertical-blend curve remap, top/bottom blending, validation,
    /// canonicalization, JSON round-trip (including the legacy verticalOffset
    /// migration), and the baked per-vertex colors that the vertex-color lit
    /// shader surfaces.
    /// </summary>
    [TestFixture]
    public class BodyVerticalGradientAppearanceTests
    {
        // ---- fixtures ---------------------------------------------------------

        /// <summary>
        /// A straight horizontal Body along Z at y = 0 with constant radius 1.
        /// Forward = Z, so the transported body frame's Normal is world up (Y):
        /// a point at y = +1 is the top of the surface, y = -1 the bottom.
        /// </summary>
        private static CreatureDefinition HorizontalBodyDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 1f });
            return definition;
        }

        private static bool HasCode(ValidationResult result, ValidationCode code)
        {
            for (int i = 0; i < result.Issues.Count; i++)
            {
                if (result.Issues[i].Code == code) return true;
            }
            return false;
        }

        /// <summary>Builds a UnityEngine.Gradient with the given color keys (>= 2) and constant opaque alpha.</summary>
        private static UnityEngine.Gradient GradientWith(params (float Time, Color Color)[] keys)
        {
            var gradient = new UnityEngine.Gradient();
            var colorKeys = new UnityEngine.GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                colorKeys[i] = new UnityEngine.GradientColorKey(keys[i].Color, keys[i].Time);
            }
            gradient.colorKeys = colorKeys;
            gradient.alphaKeys = new[]
            {
                new UnityEngine.GradientAlphaKey(1f, 0f),
                new UnityEngine.GradientAlphaKey(1f, 1f),
            };
            return gradient;
        }

        /// <summary>
        /// Builds a curve from explicit time/value/tangent keys with free tangents
        /// (matching CurveAdapter), so evaluation uses exactly the given tangents.
        /// </summary>
        private static UnityEngine.AnimationCurve CurveFrom(params (float Time, float Value, float In, float Out)[] keys)
        {
            // New Keyframes default to tangentMode 0 (Free), matching
            // CurveAdapter, so evaluation uses exactly the given tangents.
            var keyframes = new UnityEngine.Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                keyframes[i] = new UnityEngine.Keyframe(keys[i].Time, keys[i].Value, keys[i].In, keys[i].Out);
            }
            return new UnityEngine.AnimationCurve(keyframes);
        }

        // ---- Unity Gradient adapter -------------------------------------------

        [Test]
        public void GradientAdapter_Evaluate_Solid_ReturnsSameColorEverywhere()
        {
            var gradient = GradientAdapter.Solid(new Color(0.2f, 0.3f, 0.4f));

            Assert.AreEqual(new Color(0.2f, 0.3f, 0.4f), GradientAdapter.Evaluate(gradient, 0f));
            Assert.AreEqual(new Color(0.2f, 0.3f, 0.4f), GradientAdapter.Evaluate(gradient, 0.5f));
            Assert.AreEqual(new Color(0.2f, 0.3f, 0.4f), GradientAdapter.Evaluate(gradient, 1f));
        }

        [Test]
        public void GradientAdapter_Evaluate_InterpolatesBetweenTwoKeys()
        {
            UnityEngine.Gradient gradient = GradientWith((0f, Color.black), (1f, Color.white));

            Color mid = GradientAdapter.Evaluate(gradient, 0.5f);
            Assert.AreEqual(0.5f, mid.r, 1e-3f);
            Assert.AreEqual(0.5f, mid.g, 1e-3f);
            Assert.AreEqual(0.5f, mid.b, 1e-3f);
            Assert.AreEqual(0.25f, GradientAdapter.Evaluate(gradient, 0.25f).r, 1e-3f);
        }

        [Test]
        public void GradientAdapter_Evaluate_ClampsToUnitRange()
        {
            UnityEngine.Gradient gradient = GradientWith((0f, Color.black), (1f, Color.white));

            Assert.AreEqual(Color.black, GradientAdapter.Evaluate(gradient, -0.5f));
            Assert.AreEqual(Color.white, GradientAdapter.Evaluate(gradient, 1.5f));
        }

        [Test]
        public void GradientAdapter_Evaluate_WhiteMidKeyCreatesBelly()
        {
            // White in the middle of the body length — the authoring case the
            // ticket calls out for creating a belly. Tolerance-based channel
            // checks because Unity's Gradient snaps key times to 1/65535
            // increments, so an interior key time is never exactly 0.5.
            UnityEngine.Gradient gradient = GradientWith((0f, Color.black), (0.5f, Color.white), (1f, Color.black));

            Assert.AreEqual(0f, GradientAdapter.Evaluate(gradient, 0f).r, 1e-3f);
            Assert.AreEqual(1f, GradientAdapter.Evaluate(gradient, 0.5f).r, 1e-3f);
            Assert.AreEqual(1f, GradientAdapter.Evaluate(gradient, 0.5f).g, 1e-3f);
            Assert.AreEqual(1f, GradientAdapter.Evaluate(gradient, 0.5f).b, 1e-3f);
            Assert.AreEqual(0f, GradientAdapter.Evaluate(gradient, 1f).r, 1e-3f);
            Assert.AreEqual(0.5f, GradientAdapter.Evaluate(gradient, 0.25f).r, 1e-3f);
            Assert.AreEqual(0.5f, GradientAdapter.Evaluate(gradient, 0.75f).r, 1e-3f);
        }

        [Test]
        public void GradientAdapter_Evaluate_NullGradient_ReturnsWhiteRatherThanThrowing()
        {
            Assert.AreEqual(Color.white, GradientAdapter.Evaluate(null, 0.5f));
        }

        [Test]
        public void GradientAdapter_Clone_DeepCopiesKeys()
        {
            UnityEngine.Gradient original = GradientAdapter.Solid(Color.red);
            UnityEngine.Gradient clone = GradientAdapter.Clone(original);

            Assert.IsTrue(GradientAdapter.ContentEquals(original, clone));
            Assert.AreNotSame(original.colorKeys, clone.colorKeys, "Clone must copy the key arrays, not share them.");
            Assert.AreNotSame(original.alphaKeys, clone.alphaKeys);
        }

        [Test]
        public void GradientAdapter_ContentEquals_DetectsKeyAndModeDifferences()
        {
            UnityEngine.Gradient a = GradientAdapter.Solid(Color.red);
            UnityEngine.Gradient same = GradientAdapter.Clone(a);
            UnityEngine.Gradient differentColor = GradientAdapter.Solid(Color.blue);
            UnityEngine.Gradient differentMode = GradientAdapter.Clone(a);
            differentMode.mode = UnityEngine.GradientMode.Fixed;

            Assert.IsTrue(GradientAdapter.ContentEquals(a, same));
            Assert.IsFalse(GradientAdapter.ContentEquals(a, differentColor));
            Assert.IsFalse(GradientAdapter.ContentEquals(a, differentMode));
        }

        [Test]
        public void GradientAdapter_Quantize_SortsAndQuantizesKeys()
        {
            var gradient = new UnityEngine.Gradient();
            gradient.colorKeys = new[]
            {
                new UnityEngine.GradientColorKey(new Color(0.123456f, 0f, 0f, 1f), 0.123456f),
                new UnityEngine.GradientColorKey(Color.white, 1f),
            };
            gradient.alphaKeys = new[]
            {
                new UnityEngine.GradientAlphaKey(0.123456f, 0f),
                new UnityEngine.GradientAlphaKey(1f, 1f),
            };

            GradientAdapter.Quantize(gradient);

            Assert.AreEqual(0.1235f, gradient.colorKeys[0].color.r, 1e-6f, "Key colors quantize to 4 decimal places.");
            // Unity's Gradient stores key times in 1/65535 increments, so the
            // stored time is the nearest snap; re-quantizing it must give the
            // canonical 4-decimal value the writer emits.
            Assert.AreEqual(0.1235f, GenerationTolerances.Quantize(gradient.colorKeys[0].time), 1e-6f,
                "Key times must re-quantize to 4 decimal places after Unity's time snapping.");
            Assert.AreEqual(0.1235f, GenerationTolerances.Quantize(gradient.alphaKeys[0].alpha), 1e-6f,
                "Alpha key values quantize to 4 decimal places.");
            Assert.LessOrEqual(gradient.colorKeys[0].time, gradient.colorKeys[1].time, "Keys remain ordered by time.");
        }

        // ---- vertical blend curve (CC-034) ------------------------------------

        [Test]
        public void CurveAdapter_DefaultLinear_EvaluatesAsIdentity()
        {
            UnityEngine.AnimationCurve curve = BodyVerticalGradientAppearance.CreateDefault().VerticalCurve;
            for (float u = 0f; u <= 1f; u += 0.1f)
            {
                Assert.AreEqual(u, CurveAdapter.Evaluate(curve, u), 1e-4f, $"linear curve at u = {u}");
            }
        }

        [Test]
        public void CurveAdapter_Evaluate_ClampsInputToUnitRange()
        {
            UnityEngine.AnimationCurve curve = CurveAdapter.Linear();
            Assert.AreEqual(0f, CurveAdapter.Evaluate(curve, -0.5f), 1e-5f);
            Assert.AreEqual(1f, CurveAdapter.Evaluate(curve, 1.5f), 1e-5f);
        }

        [Test]
        public void CurveAdapter_FromLegacyOffset_ZeroBecomesLinear()
        {
            UnityEngine.AnimationCurve curve = CurveAdapter.FromLegacyOffset(0f);
            for (float u = 0f; u <= 1f; u += 0.1f)
            {
                Assert.AreEqual(u, CurveAdapter.Evaluate(curve, u), 1e-4f, $"offset-0 curve at u = {u}");
            }
        }

        [Test]
        public void CurveAdapter_FromLegacyOffset_ReproducesOffsetRemapExactly()
        {
            // The pre-CC-034 offset remap, as a blend factor over the remapped
            // input u = (v + 1) * 0.5, is exactly:
            //   blend(u) = (o + 1) * u        for u <= 0.5
            //   blend(u) = o + (1 - o) * u    for u >= 0.5
            foreach (float o in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            {
                UnityEngine.AnimationCurve curve = CurveAdapter.FromLegacyOffset(o);
                for (float u = 0f; u <= 1f; u += 0.05f)
                {
                    float expected = u <= 0.5f ? (o + 1f) * u : o + (1f - o) * u;
                    Assert.AreEqual(expected, CurveAdapter.Evaluate(curve, u), 1e-4f, $"offset {o}, u {u}");
                }
            }
        }

        [Test]
        public void CurveAdapter_FromLegacyOffset_PinsSurfaceExtremes()
        {
            foreach (float o in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            {
                UnityEngine.AnimationCurve curve = CurveAdapter.FromLegacyOffset(o);
                Assert.AreEqual(0f, CurveAdapter.Evaluate(curve, 0f), 1e-5f, $"bottom pinned, offset {o}");
                Assert.AreEqual(1f, CurveAdapter.Evaluate(curve, 1f), 1e-5f, $"top pinned, offset {o}");
            }
        }

        [Test]
        public void CurveAdapter_Clone_DeepCopiesKeys()
        {
            UnityEngine.AnimationCurve original = CurveFrom((0f, 0f, 1f, 1f), (0.5f, 0.75f, 1f, 0.5f), (1f, 1f, 0.5f, 0.5f));
            UnityEngine.AnimationCurve clone = CurveAdapter.Clone(original);

            Assert.IsTrue(CurveAdapter.ContentEquals(original, clone));
            Assert.AreNotSame(original.keys, clone.keys, "Clone must copy the key array, not share it.");
        }

        [Test]
        public void CurveAdapter_ContentEquals_DetectsKeyAndTangentDifferences()
        {
            UnityEngine.AnimationCurve a = CurveFrom((0f, 0f, 1f, 1f), (1f, 1f, 1f, 1f));
            UnityEngine.AnimationCurve same = CurveFrom((0f, 0f, 1f, 1f), (1f, 1f, 1f, 1f));
            UnityEngine.AnimationCurve differentValue = CurveFrom((0f, 0f, 1f, 1f), (1f, 0.8f, 1f, 1f));
            UnityEngine.AnimationCurve differentTangent = CurveFrom((0f, 0f, 1f, 1f), (1f, 1f, 2f, 1f));

            Assert.IsTrue(CurveAdapter.ContentEquals(a, same));
            Assert.IsFalse(CurveAdapter.ContentEquals(a, differentValue));
            Assert.IsFalse(CurveAdapter.ContentEquals(a, differentTangent));
        }

        [Test]
        public void CurveAdapter_Quantize_SortsAndQuantizesKeys()
        {
            var curve = new UnityEngine.AnimationCurve(new[]
            {
                new UnityEngine.Keyframe(0.123456f, 0.987654f, 1.234567f, 0.5f),
                new UnityEngine.Keyframe(1f, 1f, 1f, 1f),
            });

            CurveAdapter.Quantize(curve);

            UnityEngine.Keyframe first = curve.keys[0];
            Assert.AreEqual(0.1235f, first.time, 1e-6f, "Key time quantizes to 4 decimal places.");
            Assert.AreEqual(0.9877f, first.value, 1e-6f, "Key value quantizes to 4 decimal places.");
            Assert.AreEqual(1.2346f, first.inTangent, 1e-6f, "Key inTangent quantizes to 4 decimal places.");
            Assert.LessOrEqual(curve.keys[0].time, curve.keys[1].time, "Keys remain ordered by time.");
        }

        [Test]
        public void CurveAdapter_HasValidKeys_RejectsEmptyOutOfRangeAndNonFinite()
        {
            Assert.IsFalse(CurveAdapter.HasValidKeys(null));
            Assert.IsFalse(CurveAdapter.HasValidKeys(new UnityEngine.AnimationCurve()), "A curve with no keys is invalid.");
            Assert.IsFalse(CurveAdapter.HasValidKeys(CurveFrom((0f, 0f, 1f, 1f), (1.5f, 1f, 1f, 1f))), "A key time beyond [0, 1] is invalid.");
            // NaN values are sanitized by Unity at curve construction, so the
            // realistic non-finite case is an infinite (constant-key) tangent.
            Assert.IsFalse(CurveAdapter.HasValidKeys(CurveFrom((0f, 0f, float.PositiveInfinity, 1f), (1f, 1f, 1f, 1f))), "A non-finite tangent is invalid.");
            Assert.IsTrue(CurveAdapter.HasValidKeys(CurveFrom((0f, 0f, 1f, 1f), (1f, 1f, 1f, 1f))));
        }

        [Test]
        public void CurveAdapter_SingleKey_IsValid()
        {
            Assert.IsTrue(CurveAdapter.HasValidKeys(CurveFrom((0.5f, 0.25f, 0f, 0f))));
        }

        // ---- vertical sample + top/bottom blend -------------------------------

        [Test]
        public void TryGetBodySample_HeadIsForwardEnd()
        {
            // The head is the +Forward end. For the horizontal test body the last
            // sample (z = +1) is the head, so a point near it has t near 0 and a
            // point near the first sample (the tail) has t near 1.
            CreatureDefinition definition = HorizontalBodyDefinition();
            Assert.IsTrue(BodyVerticalGradientSampler.TryGetBodySample(definition, new Vector3(0f, 1f, 0.9f), out float nearHeadT, out _));
            Assert.IsTrue(BodyVerticalGradientSampler.TryGetBodySample(definition, new Vector3(0f, 1f, -0.9f), out float nearTailT, out _));
            Assert.LessOrEqual(nearHeadT, 0.1f, "The +Forward end is the head (t = 0).");
            Assert.GreaterOrEqual(nearTailT, 0.9f, "The -Forward end is the tail (t = 1).");
        }

        [Test]
        public void TryGetBodySample_OnTopOfTube_ReturnsPlusOne()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            Assert.IsTrue(BodyVerticalGradientSampler.TryGetBodySample(definition, new Vector3(0f, 1f, 0f), out float t, out float v));
            Assert.AreEqual(1f, v, 1e-4f);
            Assert.AreEqual(0.5f, t, 1e-2f); // middle of the 3-sample spline
        }

        [Test]
        public void TryGetBodySample_OnBottomOfTube_ReturnsMinusOne()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            Assert.IsTrue(BodyVerticalGradientSampler.TryGetBodySample(definition, new Vector3(0f, -1f, 0f), out _, out float v));
            Assert.AreEqual(-1f, v, 1e-4f);
        }

        [Test]
        public void TryGetBodySample_NoBody_ReturnsFalse()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            Assert.IsFalse(BodyVerticalGradientSampler.TryGetBodySample(definition, Vector3.zero, out _, out _));
        }

        [Test]
        public void EvaluateColor_TopOfTube_UsesTopColor()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = GradientAdapter.Solid(Color.red);
            definition.Body.Appearance.BottomGradient = GradientAdapter.Solid(Color.blue);

            Assert.AreEqual(Color.red, BodyVerticalGradientSampler.EvaluateColor(definition, new Vector3(0f, 1f, 0f)));
        }

        [Test]
        public void EvaluateColor_BottomOfTube_UsesBottomColor()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = GradientAdapter.Solid(Color.red);
            definition.Body.Appearance.BottomGradient = GradientAdapter.Solid(Color.blue);

            Assert.AreEqual(Color.blue, BodyVerticalGradientSampler.EvaluateColor(definition, new Vector3(0f, -1f, 0f)));
        }

        [Test]
        public void EvaluateColor_Centerline_BlendsHalfway()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = GradientAdapter.Solid(Color.red);
            definition.Body.Appearance.BottomGradient = GradientAdapter.Solid(Color.blue);

            // vertical sample 0 -> blend 0.5 -> red/blue midpoint (purple).
            Color c = BodyVerticalGradientSampler.EvaluateColor(definition, Vector3.zero);
            Assert.AreEqual(0.5f, c.r, 1e-3f);
            Assert.AreEqual(0f, c.g, 1e-3f);
            Assert.AreEqual(0.5f, c.b, 1e-3f);
        }

        [Test]
        public void EvaluateColor_PositiveOffsetCurve_BiasesCenterlineTowardTop()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = GradientAdapter.Solid(Color.white);
            definition.Body.Appearance.BottomGradient = GradientAdapter.Solid(Color.black);
            // The migrated offset-0.5 curve keeps the old remap: at the geometric
            // center (u = 0.5) the blend is 0.75.
            definition.Body.Appearance.VerticalCurve = CurveAdapter.FromLegacyOffset(0.5f);

            Color c = BodyVerticalGradientSampler.EvaluateColor(definition, Vector3.zero);
            Assert.AreEqual(0.75f, c.r, 1e-3f);
        }

        [Test]
        public void EvaluateColor_GradientsKeyedOverLength_HeadDiffersFromTail()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = GradientWith((0f, Color.white), (1f, Color.black)); // head white, tail black
            definition.Body.Appearance.BottomGradient = GradientAdapter.Solid(Color.gray);

            // Top-of-surface points near the head (+Forward end, z = +1) vs near the tail.
            Color nearHead = BodyVerticalGradientSampler.EvaluateColor(definition, new Vector3(0f, 1f, 0.9f));
            Color nearTail = BodyVerticalGradientSampler.EvaluateColor(definition, new Vector3(0f, 1f, -0.9f));

            Assert.GreaterOrEqual(nearHead.r, 0.9f, "Near the head the top gradient is white.");
            Assert.LessOrEqual(nearTail.r, 0.1f, "Near the tail the top gradient is black.");
            Assert.Greater(nearHead.r, nearTail.r + 0.5f);
        }

        [Test]
        public void EvaluateColor_SlopedBody_TopUsesTopGradient()
        {
            // A body that slopes upward toward +Z. Its frame Normal would point
            // downward (the old vertical axis made the gradient flip), but the
            // vertical sample uses WORLD up, so the highest side of the body must
            // still take the top gradient.
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0.3f, 0f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0.6f, 1f), Radius = 0.5f });
            definition.Body.Appearance.TopGradient = GradientAdapter.Solid(Color.white);
            definition.Body.Appearance.BottomGradient = GradientAdapter.Solid(Color.black);

            Color above = BodyVerticalGradientSampler.EvaluateColor(definition, new Vector3(0f, 0.8f, 0f));
            Color below = BodyVerticalGradientSampler.EvaluateColor(definition, new Vector3(0f, -0.2f, 0f));

            Assert.GreaterOrEqual(above.r, 0.8f, "The world-up side of the body must take the top gradient.");
            Assert.LessOrEqual(below.r, 0.2f, "The world-down side of the body must take the bottom gradient.");
        }

        [Test]
        public void EvaluateColor_NoBodyAppearance_FallsBackToGray()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body.Appearance = null;
            Assert.AreEqual(Color.gray, BodyVerticalGradientSampler.EvaluateColor(definition, Vector3.zero));
        }

        // ---- part resolver + bake integration ----------------------------------

        [Test]
        public void Resolve_PointOnBody_UsesBodyGradientNotNearestPart()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = GradientAdapter.Solid(Color.green);
            definition.Body.Appearance.BottomGradient = GradientAdapter.Solid(Color.green);
            definition.AddPart(new CreaturePart
            {
                Id = "part_far",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = new TransformData { Position = new Vector3(5f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = new AppearanceDefinition { BaseColor = Color.red, NoiseSeed = 0, NoiseScale = 1f },
            });

            ResolvedAppearance resolved = PartAppearanceSampler.Resolve(definition, new Vector3(0f, 1f, 0f));
            Assert.AreEqual(Color.green, resolved.BaseColor);
        }

        [Test]
        public void Bake_BodyOnlyMesh_ReflectsVerticalGradient()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = GradientAdapter.Solid(Color.red);
            definition.Body.Appearance.BottomGradient = GradientAdapter.Solid(Color.blue);

            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 4f };
            MeshExtractionResult mesh;
            using (DensityGrid grid = DensityGrid.SamplePortable(SdfProgramBuilder.CompilePortable(definition), bounds, settings))
            {
                mesh = MarchingCubesExtractor.Extract(grid);
            }

            Color[] colors = AppearanceBaker.Bake(definition, mesh);

            int topCount = 0;
            int bottomCount = 0;
            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                Vector3 p = mesh.Positions[i];
                if (p.y > 0.5f)
                {
                    topCount++;
                    Assert.GreaterOrEqual(colors[i].r, colors[i].b,
                        "A top-of-body vertex must be redder than blue.");
                }
                else if (p.y < -0.5f)
                {
                    bottomCount++;
                    Assert.LessOrEqual(colors[i].r, colors[i].b,
                        "A bottom-of-body vertex must be bluer than red.");
                }
            }

            Assert.Greater(topCount, 0, "Expected some extracted vertices on the top of the Body.");
            Assert.Greater(bottomCount, 0, "Expected some extracted vertices on the bottom of the Body.");
        }

        // ---- validation --------------------------------------------------------

        [Test]
        public void Validate_BodyOnlyDefinitionWithAppearance_Passes()
        {
            ValidationResult result = DefinitionValidator.Validate(HorizontalBodyDefinition());
            Assert.IsTrue(result.IsValid, "A body-only creature with the default gradient appearance must validate.");
        }

        [Test]
        public void Validate_NullTopGradient_ReportsInvalidBodyAppearance()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = null;

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidBodyAppearance));
        }

        [Test]
        public void Validate_CurveKeyOutOfRange_ReportsInvalidBodyAppearance()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.VerticalCurve =
                CurveFrom((0f, 0f, 1f, 1f), (1.5f, 1f, 1f, 1f));

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidBodyAppearance));
        }

        [Test]
        public void Validate_NonFiniteCurveValue_ReportsNonFiniteBodyAppearance()
        {
            // NaN values are sanitized by Unity at curve construction, so the
            // realistic non-finite case is an infinite (constant-key) tangent.
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.VerticalCurve =
                CurveFrom((0f, 0f, float.PositiveInfinity, 1f), (1f, 1f, 1f, 1f));

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(HasCode(result, ValidationCode.NonFiniteBodyAppearance));
        }

        [Test]
        public void Validate_NullCurve_ReportsInvalidBodyAppearance()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.VerticalCurve = null;

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidBodyAppearance));
        }

        [Test]
        public void Validate_NonFiniteKeyColor_ReportsNonFiniteBodyAppearance()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            UnityEngine.GradientColorKey[] keys = definition.Body.Appearance.TopGradient.colorKeys;
            keys[0] = new UnityEngine.GradientColorKey(new Color(float.NaN, 0f, 0f, 1f), 0f);
            definition.Body.Appearance.TopGradient.colorKeys = keys;

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(HasCode(result, ValidationCode.NonFiniteBodyAppearance));
        }

        // ---- canonicalization --------------------------------------------------

        [Test]
        public void Canonicalize_QuantizesGradientKeysAndCurve()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = new UnityEngine.Gradient
            {
                colorKeys = new[]
                {
                    new UnityEngine.GradientColorKey(new Color(0.123456f, 0f, 0f, 1f), 0.123456f),
                    new UnityEngine.GradientColorKey(Color.white, 1f),
                },
                alphaKeys = new[]
                {
                    new UnityEngine.GradientAlphaKey(1f, 0f),
                    new UnityEngine.GradientAlphaKey(1f, 1f),
                },
            };
            definition.Body.Appearance.VerticalCurve = new UnityEngine.AnimationCurve(new[]
            {
                new UnityEngine.Keyframe(0.123456f, 0.5f, 1f, 1f),
                new UnityEngine.Keyframe(1f, 1f, 1f, 1f),
            });

            CreatureDefinition result = DefinitionCanonicalizer.Canonicalize(definition);

            Assert.AreEqual(2, result.Body.Appearance.TopGradient.colorKeys.Length);
            Assert.AreEqual(0.1235f, result.Body.Appearance.TopGradient.colorKeys[0].color.r, 1e-6f,
                "Key colors quantize to 4 decimal places.");
            Assert.AreEqual(0.1235f, GenerationTolerances.Quantize(result.Body.Appearance.TopGradient.colorKeys[0].time), 1e-6f,
                "Key times re-quantize to 4 decimal places after Unity's time snapping.");
            Assert.LessOrEqual(result.Body.Appearance.TopGradient.colorKeys[0].time,
                result.Body.Appearance.TopGradient.colorKeys[1].time, "Keys remain ordered by time.");
            Assert.AreEqual(0.1235f, result.Body.Appearance.VerticalCurve.keys[0].time, 1e-6f,
                "Curve key times quantize to 4 decimal places.");
            Assert.LessOrEqual(result.Body.Appearance.VerticalCurve.keys[0].time,
                result.Body.Appearance.VerticalCurve.keys[1].time, "Curve keys remain ordered by time.");
        }

        [Test]
        public void Canonicalize_NullBodyAppearance_Throws()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance = null;

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition));
        }

        [Test]
        public void Canonicalize_NullGradient_Throws()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = null;

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition));
        }

        // ---- JSON round-trip ----------------------------------------------------

        [Test]
        public void RoundTrip_PreservesBodyVerticalGradientAppearance()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = GradientWith((0f, Color.red), (1f, Color.white));
            UnityEngine.Gradient bottom = GradientWith((0f, Color.blue), (0.5f, Color.green), (1f, Color.cyan));
            bottom.mode = UnityEngine.GradientMode.Fixed; // mode must survive the round-trip too
            definition.Body.Appearance.BottomGradient = bottom;
            definition.Body.Appearance.VerticalCurve =
                CurveFrom((0f, 0f, 1f, 1f), (0.5f, 0.75f, 1f, 0.5f), (1f, 1f, 0.5f, 0.5f));

            var serializer = new JsonDnaSerializer();
            string json = serializer.Serialize(definition);
            CreatureDefinition loaded = serializer.Deserialize(json);

            BodyVerticalGradientAppearance loadedAppearance = loaded.Body.Appearance;
            Assert.IsNotNull(loadedAppearance);
            Assert.IsTrue(definition.Body.Appearance.ContentEquals(loadedAppearance),
                "The vertical-gradient appearance must survive a canonical JSON round-trip.");
        }

        [Test]
        public void RoundTrip_SaveLoadSave_IsByteStableWithBodyAppearance()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = GradientWith((0f, new Color(0.1f, 0.2f, 0.3f)), (0.7f, Color.white));
            definition.Body.Appearance.BottomGradient = GradientAdapter.Solid(new Color(0.9f, 0.8f, 0.7f));
            definition.Body.Appearance.VerticalCurve =
                CurveFrom((0f, 0f, 1.25f, 1.25f), (0.5f, 0.75f, 1f, 0.5f), (1f, 1f, 0.5f, 0.5f));

            var serializer = new JsonDnaSerializer();
            string first = serializer.Serialize(definition);
            CreatureDefinition loaded = serializer.Deserialize(first);
            string second = serializer.Serialize(loaded);

            Assert.AreEqual(first, second, "Save -> load -> save must stay byte-stable with the new fields.");
        }

        [Test]
        public void Deserialize_BodyWithoutAppearance_DefaultsToFlatGray()
        {
            const string json =
                "{\"schemaVersion\":2,\"symmetryMode\":\"None\",\"bounds\":{\"maxX\":4,\"maxY\":4,\"maxZ\":4}," +
                "\"generation\":{\"voxelsPerUnit\":16},\"forward\":{\"x\":0,\"y\":0,\"z\":1}," +
                "\"body\":{\"samples\":[{\"id\":1,\"position\":{\"x\":0,\"y\":0,\"z\":-1},\"radius\":0.75}," +
                "{\"id\":2,\"position\":{\"x\":0,\"y\":0,\"z\":1},\"radius\":0.9}]},\"parts\":[]}";

            var serializer = new JsonDnaSerializer();
            CreatureDefinition loaded = serializer.Deserialize(json);

            Assert.IsNotNull(loaded.Body.Appearance, "An old v2 file without a body appearance must still load.");
            Assert.IsTrue(CurveAdapter.ContentEquals(CurveAdapter.Linear(), loaded.Body.Appearance.VerticalCurve),
                "The default vertical curve is linear y = x.");
            Assert.AreEqual(2, loaded.Body.Appearance.TopGradient.colorKeys.Length, "Unity gradients store at least two color keys.");
            Assert.AreEqual(Color.gray, loaded.Body.Appearance.TopGradient.colorKeys[0].color);
            Assert.AreEqual(2, loaded.Body.Appearance.BottomGradient.colorKeys.Length);
            Assert.AreEqual(Color.gray, loaded.Body.Appearance.BottomGradient.colorKeys[0].color);
        }

        [Test]
        public void Deserialize_LegacyArrayGradientFormat_LoadsAsGradient()
        {
            // The pre-CC-025-refactor format stored gradients as an array of
            // { t, color } stops. These must still load (the committed dino
            // creature used this shape) and normalize to a Unity Gradient.
            const string json =
                "{\"schemaVersion\":2,\"symmetryMode\":\"None\",\"bounds\":{\"maxX\":4,\"maxY\":4,\"maxZ\":4}," +
                "\"generation\":{\"voxelsPerUnit\":16},\"forward\":{\"x\":0,\"y\":0,\"z\":1}," +
                "\"body\":{\"samples\":[{\"id\":1,\"position\":{\"x\":0,\"y\":0,\"z\":-1},\"radius\":0.75}," +
                "{\"id\":2,\"position\":{\"x\":0,\"y\":0,\"z\":1},\"radius\":0.9}]," +
                "\"appearance\":{\"topGradient\":[{\"t\":0,\"color\":{\"r\":0.5,\"g\":0.5,\"b\":0.5,\"a\":1}}]," +
                "\"bottomGradient\":[{\"t\":0,\"color\":{\"r\":0.2,\"g\":0.2,\"b\":0.2,\"a\":1}}],\"verticalOffset\":0}}," +
                "\"parts\":[]}";

            var serializer = new JsonDnaSerializer();
            CreatureDefinition loaded = serializer.Deserialize(json);

            Assert.IsNotNull(loaded.Body.Appearance.TopGradient, "Legacy array gradients must load.");
            Assert.AreEqual(2, loaded.Body.Appearance.TopGradient.colorKeys.Length,
                "A single-stop legacy gradient expands to a solid Unity Gradient.");
            Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f, 1f), loaded.Body.Appearance.TopGradient.colorKeys[0].color);
            Assert.AreEqual(new Color(0.2f, 0.2f, 0.2f, 1f), loaded.Body.Appearance.BottomGradient.colorKeys[0].color);

            // The legacy verticalOffset: 0 migrates to the linear y = x curve.
            Assert.AreEqual(0.25f, CurveAdapter.Evaluate(loaded.Body.Appearance.VerticalCurve, 0.25f), 1e-4f);
            Assert.AreEqual(0.75f, CurveAdapter.Evaluate(loaded.Body.Appearance.VerticalCurve, 0.75f), 1e-4f);

            // And it round-trips to the current canonical form byte-stably.
            CreatureDefinition resaved = serializer.Deserialize(serializer.Serialize(loaded));
            Assert.IsTrue(loaded.Body.Appearance.ContentEquals(resaved.Body.Appearance));
        }

        [Test]
        public void Deserialize_LegacyVerticalOffset_MigratesToEquivalentCurve()
        {
            // A CC-025 file carries verticalOffset instead of verticalCurve. It
            // must load with a curve that reproduces the old remap exactly:
            //   blend(u) = (o + 1) * u        for u <= 0.5
            //   blend(u) = o + (1 - o) * u    for u >= 0.5
            const string json =
                "{\"schemaVersion\":2,\"symmetryMode\":\"None\",\"bounds\":{\"maxX\":4,\"maxY\":4,\"maxZ\":4}," +
                "\"generation\":{\"voxelsPerUnit\":16},\"forward\":{\"x\":0,\"y\":0,\"z\":1}," +
                "\"body\":{\"samples\":[{\"id\":1,\"position\":{\"x\":0,\"y\":0,\"z\":-1},\"radius\":0.75}," +
                "{\"id\":2,\"position\":{\"x\":0,\"y\":0,\"z\":1},\"radius\":0.9}]," +
                "\"appearance\":{\"topGradient\":[{\"t\":0,\"color\":{\"r\":0.5,\"g\":0.5,\"b\":0.5,\"a\":1}}]," +
                "\"bottomGradient\":[{\"t\":0,\"color\":{\"r\":0.2,\"g\":0.2,\"b\":0.2,\"a\":1}}],\"verticalOffset\":0.5}}," +
                "\"parts\":[]}";

            var serializer = new JsonDnaSerializer();
            CreatureDefinition loaded = serializer.Deserialize(json);

            UnityEngine.AnimationCurve curve = loaded.Body.Appearance.VerticalCurve;
            Assert.IsNotNull(curve, "A legacy verticalOffset must migrate to a curve.");
            Assert.AreEqual(0f, CurveAdapter.Evaluate(curve, 0f), 1e-5f);
            Assert.AreEqual(0.375f, CurveAdapter.Evaluate(curve, 0.25f), 1e-4f);
            Assert.AreEqual(0.75f, CurveAdapter.Evaluate(curve, 0.5f), 1e-4f);
            Assert.AreEqual(0.875f, CurveAdapter.Evaluate(curve, 0.75f), 1e-4f);
            Assert.AreEqual(1f, CurveAdapter.Evaluate(curve, 1f), 1e-5f);
        }
    }
}
