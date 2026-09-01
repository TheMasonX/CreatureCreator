using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Appearance;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class MeshExtractionResultNormalsTests
    {
        [Test]
        public void ComputeAngleWeightedNormals_SingleTriangle_PointsInExpectedDirection()
        {
            var mesh = new MeshExtractionResult();
            mesh.Positions.Add(Vector3.zero);
            mesh.Positions.Add(Vector3.right);
            mesh.Positions.Add(Vector3.up);
            mesh.Triangles.AddRange(new[] { 0, 1, 2 });

            mesh.ComputeAngleWeightedNormals();

            Assert.AreEqual(3, mesh.Normals.Count);
            Vector3 expected = Vector3.Cross(Vector3.right, Vector3.up).normalized;
            foreach (Vector3 normal in mesh.Normals)
            {
                Assert.AreEqual(expected, normal, "All three vertices of a single flat triangle share its face normal.");
            }
        }

        [Test]
        public void ComputeAngleWeightedNormals_IsIdempotent()
        {
            var mesh = new MeshExtractionResult();
            mesh.Positions.Add(Vector3.zero);
            mesh.Positions.Add(Vector3.right);
            mesh.Positions.Add(Vector3.up);
            mesh.Triangles.AddRange(new[] { 0, 1, 2 });

            mesh.ComputeAngleWeightedNormals();
            var first = new System.Collections.Generic.List<Vector3>(mesh.Normals);
            mesh.ComputeAngleWeightedNormals();

            CollectionAssert.AreEqual(first, mesh.Normals);
        }
    }

    [TestFixture]
    public class TriplanarNoiseTests
    {
        [Test]
        public void Evaluate_IsDeterministic()
        {
            Vector3 position = new Vector3(1.5f, -0.3f, 2.1f);
            Vector3 normal = new Vector3(0.5f, 0.7f, 0.5f).normalized;

            float first = TriplanarNoise.Evaluate(position, normal, seed: 7, scale: 1.5f);
            float second = TriplanarNoise.Evaluate(position, normal, seed: 7, scale: 1.5f);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void Evaluate_DifferentSeeds_ProduceDifferentValues()
        {
            Vector3 position = new Vector3(1.5f, -0.3f, 2.1f);
            Vector3 normal = Vector3.up;

            float a = TriplanarNoise.Evaluate(position, normal, seed: 1, scale: 1f);
            float b = TriplanarNoise.Evaluate(position, normal, seed: 99, scale: 1f);

            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void Evaluate_ReturnsValueInUnitRange()
        {
            Vector3 normal = new Vector3(0.3f, 0.9f, 0.3f).normalized;
            for (float x = -3f; x <= 3f; x += 0.7f)
            {
                float value = TriplanarNoise.Evaluate(new Vector3(x, x * 0.5f, -x), normal, seed: 3, scale: 2f);
                Assert.GreaterOrEqual(value, 0f);
                Assert.LessOrEqual(value, 1f);
            }
        }

        [Test]
        public void Evaluate_DegenerateZeroNormal_DoesNotThrowOrProduceNaN()
        {
            float value = TriplanarNoise.Evaluate(Vector3.zero, Vector3.zero, seed: 0, scale: 1f);
            Assert.IsFalse(float.IsNaN(value));
        }
    }

    [TestFixture]
    public class PartAppearanceSamplerTests
    {
        private static CreaturePart ColoredSphere(string id, Vector3 position, Color color)
        {
            return new CreaturePart
            {
                Id = id,
                PartType = PartType.Body,
                Transform = new TransformData { Position = position, Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 1f, SmoothBlendRadius = 0.1f },
                Appearance = new AppearanceDefinition { BaseColor = color, NoiseSeed = 0, NoiseScale = 1f },
            };
        }

        [Test]
        public void Resolve_PicksNearestPart()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(ColoredSphere("part_a", new Vector3(-5f, 0f, 0f), Color.red));
            definition.AddPart(ColoredSphere("part_b", new Vector3(5f, 0f, 0f), Color.blue));

            ResolvedAppearance nearA = PartAppearanceSampler.Resolve(definition, new Vector3(-5f, 0f, 0f));
            ResolvedAppearance nearB = PartAppearanceSampler.Resolve(definition, new Vector3(5f, 0f, 0f));

            Assert.AreEqual(Color.red, nearA.BaseColor);
            Assert.AreEqual(Color.blue, nearB.BaseColor);
        }

        [Test]
        public void Resolve_FarPartOutsideAabb_IsSkippedByBroadPhase()
        {
            // Part B's world AABB is ~10 units from the query point at A's
            // surface, so the broad phase must skip it without evaluating its
            // program. The result must still be A's color (no over-skip).
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(ColoredSphere("part_a", new Vector3(-5f, 0f, 0f), Color.red));
            definition.AddPart(ColoredSphere("part_b", new Vector3(5f, 0f, 0f), Color.blue));

            ResolvedAppearance atA = PartAppearanceSampler.Resolve(definition, new Vector3(-5f, 0f, 0f));
            ResolvedAppearance atB = PartAppearanceSampler.Resolve(definition, new Vector3(5f, 0f, 0f));

            Assert.AreEqual(Color.red, atA.BaseColor);
            Assert.AreEqual(Color.blue, atB.BaseColor);
        }

        [Test]
        public void Resolve_OverlappingAabbs_StillPicksNearerPart()
        {
            // Both part AABBs overlap near the origin, so the broad phase cannot
            // skip either; the nearer surface must still win.
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(ColoredSphere("part_a", new Vector3(-0.6f, 0f, 0f), Color.red));
            definition.AddPart(ColoredSphere("part_b", new Vector3(0.6f, 0f, 0f), Color.blue));

            ResolvedAppearance nearA = PartAppearanceSampler.Resolve(definition, new Vector3(-0.9f, 0f, 0f));
            ResolvedAppearance nearB = PartAppearanceSampler.Resolve(definition, new Vector3(0.9f, 0f, 0f));

            Assert.AreEqual(Color.red, nearA.BaseColor);
            Assert.AreEqual(Color.blue, nearB.BaseColor);
        }

        [Test]
        public void Resolve_BroadPhase_MatchesUnconditionalReferenceAcrossPoints()
        {
            // The broad phase must be behavior-preserving: for a grid of query
            // points over a multi-part definition (with a Body field active), the
            // broad-phase Resolver must return the exact same color + material key
            // as a reference Resolver that evaluates every part unconditionally.
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 0.8f });
            definition.AddPart(ColoredSphere("part_a", new Vector3(-2.5f, 0f, 0f), Color.red));
            definition.AddPart(ColoredSphere("part_b", new Vector3(2.5f, 0f, 0f), Color.blue));
            definition.AddPart(ColoredSphere("part_c", new Vector3(0f, 2.2f, 0f), Color.green));
            definition.AddPart(ColoredSphere("part_d", new Vector3(0f, -2.2f, 0f), Color.yellow));

            using (PartAppearanceSampler.Resolver broadPhase = PartAppearanceSampler.CreateResolver(definition))
            using (PartAppearanceSampler.Resolver reference = PartAppearanceSampler.CreateResolver(definition))
            {
                reference.EnableBroadPhase = false;

                for (float x = -4f; x <= 4f; x += 0.5f)
                for (float y = -3.5f; y <= 3.5f; y += 0.5f)
                for (float z = -1.5f; z <= 1.5f; z += 0.5f)
                {
                    var point = new Vector3(x, y, z);
                    ResolvedAppearance expected = reference.Resolve(point);
                    ResolvedAppearance actual = broadPhase.Resolve(point);
                    Assert.AreEqual(expected.BaseColor, actual.BaseColor, $"BaseColor mismatch at {point}.");
                    Assert.AreEqual(expected.MaterialKey, actual.MaterialKey, $"MaterialKey mismatch at {point}.");
                }
            }
        }

        [Test]
        public void Resolve_EmptyDefinition_ReturnsDefaultRatherThanThrowing()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            ResolvedAppearance resolved = PartAppearanceSampler.Resolve(definition, Vector3.zero);
            Assert.AreEqual(AppearanceDefinition.Default.BaseColor, resolved.BaseColor);
        }

        [Test]
        public void Resolve_NearestPartWithMaterialKey_SurfacesKey()
        {
            var definition = CreatureDefinition.CreateEmpty();
            CreaturePart eye = ColoredSphere("eye", new Vector3(0f, 0f, 0f), Color.white);
            eye.Appearance.MaterialKey = "eye_white";
            definition.AddPart(eye);

            ResolvedAppearance resolved = PartAppearanceSampler.Resolve(definition, Vector3.zero);

            Assert.AreEqual("eye_white", resolved.MaterialKey,
                "a part with a submaterial override surfaces its key through appearance resolution");
        }

        [Test]
        public void Resolve_PartWithoutMaterialKey_SurfacesNull()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(ColoredSphere("part", new Vector3(0f, 0f, 0f), Color.red));

            ResolvedAppearance resolved = PartAppearanceSampler.Resolve(definition, Vector3.zero);

            Assert.IsNull(resolved.MaterialKey,
                "a part without an override keeps the nearest-part fallback (no material key)");
        }

        [Test]
        public void Resolve_NullDefinition_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => PartAppearanceSampler.Resolve(null, Vector3.zero));
        }

        [Test]
        public void Resolver_PortableProgramsMatchManagedAppearanceSelection()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 0.8f });
            definition.AddPart(ColoredSphere("part", new Vector3(2f, 0f, 0f), Color.red));

            using (PartAppearanceSampler.Resolver resolver = PartAppearanceSampler.CreateResolver(definition))
            {
                foreach (Vector3 point in new[] { Vector3.zero, new Vector3(2f, 0f, 0f), new Vector3(-1.5f, 0f, 0f) })
                {
                    ResolvedAppearance expected = ResolveManaged(definition, point);
                    ResolvedAppearance actual = resolver.Resolve(point);
                    Assert.AreEqual(expected.BaseColor, actual.BaseColor, $"Mismatch at {point}.");
                    Assert.AreEqual(expected.MaterialKey, actual.MaterialKey, $"Material mismatch at {point}.");
                }
            }
        }

        private static ResolvedAppearance ResolveManaged(CreatureDefinition definition, Vector3 position)
        {
            using (PartAppearanceSampler.Resolver portableResolver = PartAppearanceSampler.CreateResolver(definition))
            {
                return portableResolver.Resolve(position);
            }
        }
    }

    [TestFixture]
    public class AppearanceBakerTests
    {
        [Test]
        public void Bake_ProducesOneColorPerVertex()
        {
            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 4f };
            var samplingDefinition = CreatureDefinition.CreateEmpty();
            samplingDefinition.AddPart(new CreaturePart { Id = "sphere_geometry", PartType = PartType.Body, Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 1f, SmoothBlendRadius = 0f }, Appearance = AppearanceDefinition.Default });
            MeshExtractionResult mesh;
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(samplingDefinition))
            using (DensityGrid grid = DensityGrid.SamplePortable(program, bounds, settings))
            {
                mesh = MarchingCubesExtractor.Extract(grid);
            }

            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart
            {
                Id = "part_body",
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 1f, SmoothBlendRadius = 0.1f },
                Appearance = new AppearanceDefinition { BaseColor = Color.green, NoiseSeed = 5, NoiseScale = 2f },
            });

            Color[] colors = AppearanceBaker.Bake(definition, mesh);

            Assert.AreEqual(mesh.Positions.Count, colors.Length);
        }

        [Test]
        public void Bake_ColorsStayNearBaseColorWithinBrightnessVariation()
        {
            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 4f };
            var samplingDefinition = CreatureDefinition.CreateEmpty();
            samplingDefinition.AddPart(new CreaturePart { Id = "sphere_geometry", PartType = PartType.Body, Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 1f, SmoothBlendRadius = 0f }, Appearance = AppearanceDefinition.Default });
            MeshExtractionResult mesh;
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(samplingDefinition))
            using (DensityGrid grid = DensityGrid.SamplePortable(program, bounds, settings))
            {
                mesh = MarchingCubesExtractor.Extract(grid);
            }

            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart
            {
                Id = "part_body",
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 1f, SmoothBlendRadius = 0.1f },
                Appearance = new AppearanceDefinition { BaseColor = new Color(0.5f, 0.5f, 0.5f), NoiseSeed = 1, NoiseScale = 1f },
            });

            Color[] colors = AppearanceBaker.Bake(definition, mesh);

            foreach (Color c in colors)
            {
                Assert.GreaterOrEqual(c.r, 0.5f * 0.84f);
                Assert.LessOrEqual(c.r, 0.5f * 1.16f);
            }
        }

        [Test]
        public void Bake_NullArguments_ThrowDomainException()
        {
            var mesh = new MeshExtractionResult();
            var definition = CreatureDefinition.CreateEmpty();

            Assert.Throws<DomainException>(() => AppearanceBaker.Bake(null, mesh));
            Assert.Throws<DomainException>(() => AppearanceBaker.Bake(definition, null));
        }

        [Test]
        public void BakePart_SolidNonWhiteAppearance_ProducesMatchingColors()
        {
            var positions = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 0f) };
            var normals = new[] { Vector3.up, Vector3.up };
            var part = new CreaturePart
            {
                Id = "part",
                Appearance = new AppearanceDefinition { BaseColor = new Color(1f, 0f, 0f), NoiseSeed = 0, NoiseScale = 1f },
            };

            Color[] colors = AppearanceBaker.BakePart(part, positions, normals);

            Assert.AreEqual(positions.Length, colors.Length);
            foreach (Color c in colors)
            {
                Assert.GreaterOrEqual(c.r, 0.84f, "red channel stays near the authored base within the brightness band");
                Assert.LessOrEqual(c.r, 1.16f);
                Assert.AreEqual(0f, c.g, "a pure-red authored color must not gain green");
                Assert.AreEqual(0f, c.b, "a pure-red authored color must not gain blue");
                Assert.AreEqual(1f, c.a);
            }
        }

        [Test]
        public void BakePart_IgnoresNearestPartAndBodyGradient_ByDesign()
        {
            // A mesh-asset part is not part of the implicit SDF field, so its color
            // must come from the part itself even when another part or the Body
            // surface is nearer. BakePart resolves the part's own appearance only.
            var positions = new[] { Vector3.zero };
            var normals = new[] { Vector3.up };
            var part = new CreaturePart
            {
                Id = "mesh_part",
                Appearance = new AppearanceDefinition { BaseColor = new Color(0f, 0f, 1f), NoiseSeed = 0, NoiseScale = 1f },
            };

            Color[] colors = AppearanceBaker.BakePart(part, positions, normals);

            Assert.AreEqual(0f, colors[0].r);
            Assert.AreEqual(0f, colors[0].g);
            Assert.GreaterOrEqual(colors[0].b, 0.84f);
        }

        [Test]
        public void BakePart_NullArguments_ThrowDomainException()
        {
            Assert.Throws<DomainException>(() => AppearanceBaker.BakePart(null, new Vector3[1], new Vector3[1]));
            Assert.Throws<DomainException>(() => AppearanceBaker.BakePart(new CreaturePart(), null, new Vector3[1]));
            Assert.Throws<DomainException>(() => AppearanceBaker.BakePart(new CreaturePart(), new Vector3[1], null));
            Assert.Throws<DomainException>(() =>
                AppearanceBaker.BakePart(new CreaturePart(), new Vector3[1], new Vector3[2]));
        }
    }
}
