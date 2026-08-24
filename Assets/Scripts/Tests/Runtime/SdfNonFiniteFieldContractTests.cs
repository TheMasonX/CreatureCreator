using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using ProceduralCreature.Appearance;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// CC-064 non-finite field contract: a sampled scalar field treats
    ///   +inf = outside / culled / semantically absent (never a giant valid distance)
    ///   NaN  = always invalid
    ///   -inf = invalid for field sampling
    ///   finite = the evaluated field
    /// Fast culling (CC-063) writes +inf for skipped operations. These tests pin
    /// the contract at the sampling, appearance-selection, and grid boundaries so
    /// +inf never leaks into geometry or colors as a "giant finite distance".
    /// </summary>
    [TestFixture]
    public class SdfNonFiniteFieldContractTests
    {
        private static CreatureDefinition DefinitionWithBodyAndPart()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 8f };
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 0.6f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 0.5f });
            definition.AddPart(new CreaturePart
            {
                Id = "part_leg",
                PartType = PartType.Part,
                ParentId = CreatureDefinition.BodyId,
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.4f, SmoothBlendRadius = 0.15f },
                Appearance = AppearanceDefinition.Default,
            });
            return definition;
        }

        [Test]
        public void FastCulling_CulledSample_ReadsExactlyPositiveInfinity()
        {
            var definition = DefinitionWithBodyAndPart();
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            {
                // A point far outside every AABB (and its blend-inflated cull box)
                // must read exactly +inf in Fast mode — the "outside/culled" sentinel,
                // never a large finite distance and never NaN.
                float far = SdfProgramEvaluator.Evaluate(
                    program.Operations, program.RootIndex, new float3(5f, 5f, 5f),
                    program.InfluenceRadius, SdfCullingMode.Fast);

                Assert.AreEqual(float.PositiveInfinity, far,
                    "A far Fast-mode sample must read exactly +inf (the culled sentinel).");
            }
        }

        /// <summary>
        /// THE CC-064 no-candidate fix: when every candidate — every part AND the
        /// Body — reads +inf at a point (all culled), appearance selection must
        /// return the default appearance, not the Body's gradient color. Without
        /// the finite-body guard, `+inf <= +inf` made the Body "win" and the point
        /// took the Body gradient even though nothing was actually there.
        /// </summary>
        [Test]
        public void AppearanceSelection_AllCandidatesInf_ReturnsDefaultAppearanceNotBodyGradient()
        {
            var definition = DefinitionWithBodyAndPart();
            // A solid red Body gradient makes the old fallthrough observable:
            // BodyVerticalGradientSampler projects ANY point onto the Body spline
            // (no distance cutoff), so the far point would read red if the Body
            // were wrongly selected.
            definition.Body.Appearance.TopGradient = GradientAdapter.Solid(Color.red);
            definition.Body.Appearance.BottomGradient = GradientAdapter.Solid(Color.red);

            using (PartAppearanceSampler.Resolver resolver =
                PartAppearanceSampler.CreateResolver(definition, SdfCullingMode.Fast))
            {
                ResolvedAppearance result = resolver.Resolve(new Vector3(5f, 5f, 5f));

                Assert.AreEqual(AppearanceDefinition.Default.BaseColor, result.BaseColor,
                    "With every candidate culled (+inf), appearance must fall back to the default, not the Body gradient.");
                Assert.AreNotEqual(Color.red, result.BaseColor,
                    "The Body must not be selected as the nearest candidate when it reads +inf.");
            }
        }

        /// <summary>
        /// A culled (+inf) candidate must never beat a finite candidate in the
        /// nearest-part decision, so a point near one part keeps that part's
        /// appearance even when a far part is culled.
        /// </summary>
        [Test]
        public void AppearanceSelection_InfCandidate_NeverWinsOverFinitePart()
        {
            var definition = CreatureDefinition.CreateEmpty(); // no Body — Body program is Empty (+inf)
            definition.AddPart(new CreaturePart
            {
                Id = "part_near",
                ParentId = CreatureDefinition.BodyId,
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.4f, SmoothBlendRadius = 0.1f },
                Appearance = new AppearanceDefinition { BaseColor = Color.red, NoiseSeed = 0, NoiseScale = 1f },
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_far",
                ParentId = CreatureDefinition.BodyId,
                Transform = new TransformData { Position = new Vector3(2f, 2f, 2f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.4f, SmoothBlendRadius = 0.1f },
                Appearance = new AppearanceDefinition { BaseColor = Color.blue, NoiseSeed = 0, NoiseScale = 1f },
            });

            using (PartAppearanceSampler.Resolver resolver =
                PartAppearanceSampler.CreateResolver(definition, SdfCullingMode.Fast))
            {
                // Near part_near: finite for it, culled (+inf) for part_far and the Body.
                ResolvedAppearance result = resolver.Resolve(new Vector3(0.2f, 0f, 0f));

                Assert.AreEqual(Color.red, result.BaseColor,
                    "A finite candidate must win over culled (+inf) candidates.");
            }
        }

        /// <summary>
        /// Over a Fast-sampled grid, culled corners read +inf while interior
        /// samples stay finite; a min-scan over the grid must ignore +inf so the
        /// minimum is always a real interior value (never a polluted +inf or NaN).
        /// </summary>
        [Test]
        public void FastGrid_CulledCornersArePositiveInfinity_AndMinIsFiniteInterior()
        {
            var definition = DefinitionWithBodyAndPart();
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            {
                DensityGrid grid = DensityGrid.SamplePortable(
                    program, definition.Bounds, definition.Generation, SdfCullingMode.Fast);

                bool sawInf = false;
                float min = float.PositiveInfinity;
                for (int z = 0; z <= grid.CellsZ; z++)
                for (int y = 0; y <= grid.CellsY; y++)
                for (int x = 0; x <= grid.CellsX; x++)
                {
                    float s = grid.GetSample(x, y, z);
                    Assert.IsFalse(float.IsNaN(s), $"Fast sample must never be NaN at ({x},{y},{z}).");
                    if (float.IsPositiveInfinity(s))
                    {
                        sawInf = true;
                        continue;
                    }
                    if (s < min) min = s;
                }

                Assert.IsTrue(sawInf, "Fast mode must cull far corners to +inf within the grid.");
                Assert.IsTrue(min < 0f,
                    "The finite minimum over the grid must be an interior value; +inf must not pollute the min.");
            }
        }
    }
}
