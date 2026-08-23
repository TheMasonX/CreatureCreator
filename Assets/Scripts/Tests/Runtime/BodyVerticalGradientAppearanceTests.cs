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
    /// Tests for the Body vertical-gradient appearance model (CC-025): gradient
    /// evaluation, the vertical-sample projection + offset math, top/bottom
    /// blending, validation, canonicalization, JSON round-trip, and the baked
    /// per-vertex colors that the vertex-color lit shader surfaces.
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

        // ---- gradient evaluation -----------------------------------------------

        [Test]
        public void ColorGradient_Evaluate_SingleStop_ReturnsSameColorEverywhere()
        {
            var gradient = ColorGradient.Solid(new Color(0.2f, 0.3f, 0.4f));

            Assert.AreEqual(new Color(0.2f, 0.3f, 0.4f), gradient.Evaluate(0f));
            Assert.AreEqual(new Color(0.2f, 0.3f, 0.4f), gradient.Evaluate(0.5f));
            Assert.AreEqual(new Color(0.2f, 0.3f, 0.4f), gradient.Evaluate(1f));
        }

        [Test]
        public void ColorGradient_Evaluate_InterpolatesBetweenTwoStops()
        {
            var gradient = new ColorGradient();
            gradient.Stops.Add(new GradientColorStop(0f, Color.black));
            gradient.Stops.Add(new GradientColorStop(1f, Color.white));

            Color mid = gradient.Evaluate(0.5f);
            Assert.AreEqual(0.5f, mid.r, 1e-3f);
            Assert.AreEqual(0.5f, mid.g, 1e-3f);
            Assert.AreEqual(0.5f, mid.b, 1e-3f);
            Assert.AreEqual(0.25f, gradient.Evaluate(0.25f).r, 1e-3f);
        }

        [Test]
        public void ColorGradient_Evaluate_ClampsTOutsideUnitRange()
        {
            var gradient = new ColorGradient();
            gradient.Stops.Add(new GradientColorStop(0f, Color.black));
            gradient.Stops.Add(new GradientColorStop(1f, Color.white));

            Assert.AreEqual(Color.black, gradient.Evaluate(-0.5f));
            Assert.AreEqual(Color.white, gradient.Evaluate(1.5f));
        }

        [Test]
        public void ColorGradient_Evaluate_WhiteMidStopCreatesBelly()
        {
            // White in the middle of the body length — the authoring case the
            // ticket calls out for creating a belly.
            var gradient = new ColorGradient();
            gradient.Stops.Add(new GradientColorStop(0f, Color.black));
            gradient.Stops.Add(new GradientColorStop(0.5f, Color.white));
            gradient.Stops.Add(new GradientColorStop(1f, Color.black));

            Assert.AreEqual(Color.black, gradient.Evaluate(0f));
            Assert.AreEqual(Color.white, gradient.Evaluate(0.5f));
            Assert.AreEqual(Color.black, gradient.Evaluate(1f));
            Assert.AreEqual(0.5f, gradient.Evaluate(0.25f).r, 1e-3f);
            Assert.AreEqual(0.5f, gradient.Evaluate(0.75f).r, 1e-3f);
        }

        [Test]
        public void ColorGradient_Evaluate_EmptyReturnsWhiteRatherThanThrowing()
        {
            var gradient = new ColorGradient();
            Assert.AreEqual(Color.white, gradient.Evaluate(0.5f));
        }

        // ---- vertical offset shift --------------------------------------------

        [Test]
        public void ApplyVerticalOffset_ZeroOffset_IsIdentity()
        {
            for (float v = -1f; v <= 1f; v += 0.25f)
            {
                Assert.AreEqual(v, BodyVerticalGradientSampler.ApplyVerticalOffset(v, 0f), 1e-4f);
            }
        }

        [Test]
        public void ApplyVerticalOffset_TopBoundary_IsExactlyOne()
        {
            Assert.AreEqual(1f, BodyVerticalGradientSampler.ApplyVerticalOffset(1f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, BodyVerticalGradientSampler.ApplyVerticalOffset(1f, -0.5f), 1e-5f);
            Assert.AreEqual(1f, BodyVerticalGradientSampler.ApplyVerticalOffset(1f, 1f), 1e-5f);
            Assert.AreEqual(1f, BodyVerticalGradientSampler.ApplyVerticalOffset(1f, -1f), 1e-5f);
        }

        [Test]
        public void ApplyVerticalOffset_BottomBoundary_IsExactlyMinusOne()
        {
            Assert.AreEqual(-1f, BodyVerticalGradientSampler.ApplyVerticalOffset(-1f, 0.5f), 1e-5f);
            Assert.AreEqual(-1f, BodyVerticalGradientSampler.ApplyVerticalOffset(-1f, -0.5f), 1e-5f);
            Assert.AreEqual(-1f, BodyVerticalGradientSampler.ApplyVerticalOffset(-1f, 1f), 1e-5f);
            Assert.AreEqual(-1f, BodyVerticalGradientSampler.ApplyVerticalOffset(-1f, -1f), 1e-5f);
        }

        [Test]
        public void ApplyVerticalOffset_ZeroPointLandsOnOffset()
        {
            Assert.AreEqual(0.5f, BodyVerticalGradientSampler.ApplyVerticalOffset(0f, 0.5f), 1e-5f);
            Assert.AreEqual(-0.3f, BodyVerticalGradientSampler.ApplyVerticalOffset(0f, -0.3f), 1e-5f);
            Assert.AreEqual(1f, BodyVerticalGradientSampler.ApplyVerticalOffset(0f, 1f), 1e-5f);
        }

        [Test]
        public void ApplyVerticalOffset_IsMonotonicInVerticalSample()
        {
            float[] offsets = { -1f, -0.5f, 0f, 0.5f, 1f };
            foreach (float offset in offsets)
            {
                float previous = -1f;
                for (float v = -1f; v <= 1f; v += 0.1f)
                {
                    float result = BodyVerticalGradientSampler.ApplyVerticalOffset(v, offset);
                    Assert.GreaterOrEqual(result, previous - 1e-5f, $"offset {offset}, v {v}");
                    previous = result;
                }
            }
        }

        [Test]
        public void ApplyVerticalOffset_ClampsOutOfRangeOffsetAndSample()
        {
            Assert.AreEqual(1f, BodyVerticalGradientSampler.ApplyVerticalOffset(1f, 5f), 1e-5f);
            Assert.AreEqual(-1f, BodyVerticalGradientSampler.ApplyVerticalOffset(-1f, -5f), 1e-5f);
            Assert.AreEqual(1f, BodyVerticalGradientSampler.ApplyVerticalOffset(9f, 0f), 1e-5f);
            Assert.AreEqual(-1f, BodyVerticalGradientSampler.ApplyVerticalOffset(-9f, 0f), 1e-5f);
        }

        // ---- vertical sample + top/bottom blend -------------------------------

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
            definition.Body.Appearance.TopGradient = ColorGradient.Solid(Color.red);
            definition.Body.Appearance.BottomGradient = ColorGradient.Solid(Color.blue);

            Assert.AreEqual(Color.red, BodyVerticalGradientSampler.EvaluateColor(definition, new Vector3(0f, 1f, 0f)));
        }

        [Test]
        public void EvaluateColor_BottomOfTube_UsesBottomColor()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = ColorGradient.Solid(Color.red);
            definition.Body.Appearance.BottomGradient = ColorGradient.Solid(Color.blue);

            Assert.AreEqual(Color.blue, BodyVerticalGradientSampler.EvaluateColor(definition, new Vector3(0f, -1f, 0f)));
        }

        [Test]
        public void EvaluateColor_Centerline_BlendsHalfway()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = ColorGradient.Solid(Color.red);
            definition.Body.Appearance.BottomGradient = ColorGradient.Solid(Color.blue);

            // vertical sample 0 -> blend 0.5 -> red/blue midpoint (purple).
            Color c = BodyVerticalGradientSampler.EvaluateColor(definition, Vector3.zero);
            Assert.AreEqual(0.5f, c.r, 1e-3f);
            Assert.AreEqual(0f, c.g, 1e-3f);
            Assert.AreEqual(0.5f, c.b, 1e-3f);
        }

        [Test]
        public void EvaluateColor_PositiveOffset_BiasesCenterlineTowardTop()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = ColorGradient.Solid(Color.white);
            definition.Body.Appearance.BottomGradient = ColorGradient.Solid(Color.black);
            definition.Body.Appearance.VerticalOffset = 0.5f;

            // At the geometric center the shifted sample is 0.5 -> blend 0.75.
            Color c = BodyVerticalGradientSampler.EvaluateColor(definition, Vector3.zero);
            Assert.AreEqual(0.75f, c.r, 1e-3f);
        }

        [Test]
        public void EvaluateColor_GradientsKeyedOverLength_HeadDiffersFromTail()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            var top = new ColorGradient();
            top.Stops.Add(new GradientColorStop(0f, Color.white)); // head white
            top.Stops.Add(new GradientColorStop(1f, Color.black)); // tail black
            definition.Body.Appearance.TopGradient = top;
            definition.Body.Appearance.BottomGradient = ColorGradient.Solid(Color.gray);

            // Top-of-surface points near the head vs near the tail.
            Color nearHead = BodyVerticalGradientSampler.EvaluateColor(definition, new Vector3(0f, 1f, -0.9f));
            Color nearTail = BodyVerticalGradientSampler.EvaluateColor(definition, new Vector3(0f, 1f, 0.9f));

            Assert.GreaterOrEqual(nearHead.r, 0.9f, "Near the head the top gradient is white.");
            Assert.LessOrEqual(nearTail.r, 0.1f, "Near the tail the top gradient is black.");
            Assert.Greater(nearHead.r, nearTail.r + 0.5f);
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
            definition.Body.Appearance.TopGradient = ColorGradient.Solid(Color.green);
            definition.Body.Appearance.BottomGradient = ColorGradient.Solid(Color.green);
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
            definition.Body.Appearance.TopGradient = ColorGradient.Solid(Color.red);
            definition.Body.Appearance.BottomGradient = ColorGradient.Solid(Color.blue);

            ISdfNode sdf = SdfProgramBuilder.Compile(definition);
            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 4f };
            DensityGrid grid = DensityGrid.Sample(sdf, bounds, settings);
            MeshExtractionResult mesh = MarchingCubesExtractor.Extract(sdf, grid);

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
        public void Validate_EmptyBottomGradient_ReportsInvalidBodyAppearance()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.BottomGradient = new ColorGradient(); // no stops

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidBodyAppearance));
        }

        [Test]
        public void Validate_OutOfRangeOffset_ReportsInvalidBodyAppearance()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.VerticalOffset = 1.5f;

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidBodyAppearance));
        }

        [Test]
        public void Validate_StopTOutsideUnitRange_ReportsInvalidBodyAppearance()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient.Stops.Add(new GradientColorStop(1.3f, Color.white));

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(HasCode(result, ValidationCode.InvalidBodyAppearance));
        }

        [Test]
        public void Validate_NonFiniteStop_ReportsNonFiniteBodyAppearance()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient.Stops[0] =
                new GradientColorStop(0f, new Color(float.NaN, 0f, 0f, 1f));

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(HasCode(result, ValidationCode.NonFiniteBodyAppearance));
        }

        // ---- canonicalization --------------------------------------------------

        [Test]
        public void Canonicalize_SortsAndQuantizesGradientStopsAndOffset()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient.Stops.Clear();
            definition.Body.Appearance.TopGradient.Stops.Add(new GradientColorStop(1f, new Color(0.123456f, 0f, 0f, 1f)));
            definition.Body.Appearance.TopGradient.Stops.Add(new GradientColorStop(0f, Color.white));
            definition.Body.Appearance.VerticalOffset = 0.123456f;

            CreatureDefinition result = DefinitionCanonicalizer.Canonicalize(definition);

            Assert.AreEqual(2, result.Body.Appearance.TopGradient.Stops.Count);
            Assert.AreEqual(0f, result.Body.Appearance.TopGradient.Stops[0].T, 1e-6f, "Stops must sort ascending by T.");
            Assert.AreEqual(1f, result.Body.Appearance.TopGradient.Stops[1].T, 1e-6f);
            Assert.AreEqual(0.1235f, result.Body.Appearance.TopGradient.Stops[1].Color.r, 1e-6f,
                "Stop colors quantize to 4 decimal places.");
            Assert.AreEqual(0.1235f, result.Body.Appearance.VerticalOffset, 1e-6f);
        }

        [Test]
        public void Canonicalize_NullBodyAppearance_Throws()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance = null;

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition));
        }

        // ---- JSON round-trip ----------------------------------------------------

        [Test]
        public void RoundTrip_PreservesBodyVerticalGradientAppearance()
        {
            CreatureDefinition definition = HorizontalBodyDefinition();
            definition.Body.Appearance.TopGradient = new ColorGradient
            {
                Stops =
                {
                    new GradientColorStop(0f, Color.red),
                    new GradientColorStop(1f, Color.white),
                },
            };
            definition.Body.Appearance.BottomGradient = new ColorGradient
            {
                Stops =
                {
                    new GradientColorStop(0f, Color.blue),
                    new GradientColorStop(0.5f, Color.green),
                    new GradientColorStop(1f, Color.cyan),
                },
            };
            definition.Body.Appearance.VerticalOffset = 0.35f;

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
            definition.Body.Appearance.TopGradient = new ColorGradient
            {
                Stops = { new GradientColorStop(0f, new Color(0.1f, 0.2f, 0.3f)), new GradientColorStop(0.7f, Color.white) },
            };
            definition.Body.Appearance.BottomGradient = ColorGradient.Solid(new Color(0.9f, 0.8f, 0.7f));
            definition.Body.Appearance.VerticalOffset = -0.2f;

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
            Assert.AreEqual(0f, loaded.Body.Appearance.VerticalOffset, 1e-6f);
            Assert.AreEqual(1, loaded.Body.Appearance.TopGradient.Stops.Count);
            Assert.AreEqual(Color.gray, loaded.Body.Appearance.TopGradient.Stops[0].Color);
            Assert.AreEqual(1, loaded.Body.Appearance.BottomGradient.Stops.Count);
            Assert.AreEqual(Color.gray, loaded.Body.Appearance.BottomGradient.Stops[0].Color);
        }
    }
}
