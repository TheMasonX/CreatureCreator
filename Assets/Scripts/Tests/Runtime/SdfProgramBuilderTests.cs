using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class SdfProgramBuilderTests
    {
        private static CreatureDefinition Sphere(string id, Vector3 position, string parentId = null)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = id, ParentId = parentId,
                Transform = new TransformData { Position = position, Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default });
            return definition;
        }

        private static float Evaluate(SdfProgram program, Vector3 point)
        {
            return SdfProgramEvaluator.Evaluate(program, new float3(point.x, point.y, point.z));
        }

        [Test]
        public void CompilePortable_EmptyDefinitionIsOutside()
        {
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(CreatureDefinition.CreateEmpty()))
                Assert.IsTrue(float.IsPositiveInfinity(Evaluate(program, Vector3.zero)));
        }

        [Test]
        public void CompilePortable_SinglePartProducesSphereField()
        {
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(Sphere("part", Vector3.zero)))
                Assert.Less(Evaluate(program, Vector3.zero), 0f);
        }

        [Test]
        public void CompilePortable_BodySplineIsPrimaryField()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            {
                Assert.Less(Evaluate(program, new Vector3(0f, 0f, -1f)), 0f);
                Assert.Less(Evaluate(program, new Vector3(0f, 0f, 1f)), 0f);
                Assert.Greater(Evaluate(program, new Vector3(0f, 0f, 4f)), 0f);
            }
        }

        [Test]
        public void CompilePortable_IsDeterministicRegardlessOfPartOrder()
        {
            CreatureDefinition first = Sphere("part_a", Vector3.zero);
            first.AddPart(new CreaturePart { Id = "part_b", Transform = new TransformData { Position = Vector3.right * 3f,
                Rotation = Quaternion.identity, Scale = Vector3.one }, Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default });
            CreatureDefinition second = Sphere("part_b", Vector3.right * 3f);
            second.AddPart(new CreaturePart { Id = "part_a", Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default });
            using (SdfProgram a = SdfProgramBuilder.CompilePortable(first))
            using (SdfProgram b = SdfProgramBuilder.CompilePortable(second))
            {
                for (float x = -2f; x <= 5f; x += 0.5f)
                    Assert.AreEqual(Evaluate(a, new Vector3(x, 0.3f, -0.2f)), Evaluate(b, new Vector3(x, 0.3f, -0.2f)), 1e-4f);
            }
        }

        [Test]
        public void CompilePortable_AndIndividualPartCompilationSharePrimitiveOperationData()
        {
            CreatureDefinition definition = Sphere("part", new Vector3(2f, 0f, 0f));
            using (SdfProgram wholeProgram = SdfProgramBuilder.CompilePortable(definition))
            {
                var individualPrograms = SdfProgramBuilder.CompileIndividualPartsPortable(definition);
                try
                {
                    Assert.AreEqual(1, individualPrograms.Count);
                    SdfProgram partProgram = individualPrograms[0].Program;
                    Assert.AreEqual(wholeProgram.Operations.Length, partProgram.Operations.Length);

                    for (int i = 0; i < wholeProgram.Operations.Length; i++)
                    {
                        SdfOperation whole = wholeProgram.Operations[i];
                        SdfOperation part = partProgram.Operations[i];
                        Assert.AreEqual(whole.Type, part.Type);
                        Assert.AreEqual(whole.A, part.A);
                        Assert.AreEqual(whole.B, part.B);
                        Assert.AreEqual(whole.Parameters, part.Parameters);
                        Assert.AreEqual(whole.Matrix, part.Matrix);
                        Assert.AreEqual(whole.DistanceScale, part.DistanceScale);
                        Assert.AreEqual(whole.MinBound, part.MinBound);
                        Assert.AreEqual(whole.MaxBound, part.MaxBound);
                        Assert.AreEqual(whole.Cullable, part.Cullable);
                    }
                }
                finally
                {
                    foreach (ResolvedPartProgram partProgram in individualPrograms)
                        partProgram.Program.Dispose();
                }
            }
        }

        [Test]
        public void CompilePortable_ChildInheritsParentTransform()
        {
            CreatureDefinition definition = Sphere("root", new Vector3(10f, 0f, 0f));
            definition.AddPart(new CreaturePart { Id = "child", ParentId = "root", Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default });
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
                Assert.Less(Evaluate(program, new Vector3(10f, 0f, 0f)), -0.5f);
        }

        [Test]
        public void CompilePortable_MirroredPartProducesBothSides()
        {
            CreatureDefinition definition = Sphere("leg", new Vector3(5f, 0f, 0f));
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.FindPart("leg").MirrorAcrossSymmetryPlane = true;
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            {
                Assert.Less(Evaluate(program, new Vector3(5f, 0f, 0f)), 0f);
                Assert.Less(Evaluate(program, new Vector3(-5f, 0f, 0f)), 0f);
                Assert.Greater(Evaluate(program, Vector3.zero), 0f);
            }
        }

        [Test]
        public void CompilePortable_UnmirroredPartStaysOnOriginalSide()
        {
            CreatureDefinition definition = Sphere("leg", new Vector3(5f, 0f, 0f));
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
                Assert.Greater(Evaluate(program, new Vector3(-5f, 0f, 0f)), 0f);
        }

        [Test]
        public void Evaluate_SymmetryMirrorsCompositeSubtree()
        {
            var operations = new NativeArray<SdfOperation>(6, Allocator.Temp);
            operations[0] = SdfOperation.Primitive(SdfOperationType.Sphere, new float3(1f, 0f, 0f));
            operations[1] = new SdfOperation
            {
                Type = SdfOperationType.Transform,
                A = 0,
                Matrix = float4x4.Translate(new float3(2f, 0f, 0f)),
                DistanceScale = 1f,
            };
            operations[2] = SdfOperation.Primitive(SdfOperationType.Sphere, new float3(1f, 0f, 0f));
            operations[3] = new SdfOperation
            {
                Type = SdfOperationType.Transform,
                A = 2,
                Matrix = float4x4.Translate(new float3(4f, 0f, 0f)),
                DistanceScale = 1f,
            };
            operations[4] = new SdfOperation
            {
                Type = SdfOperationType.SmoothUnion,
                A = 1,
                B = 3,
                Parameters = new float3(0f, 0f, 0f),
            };
            operations[5] = new SdfOperation { Type = SdfOperationType.Symmetry, A = 4 };

            try
            {
                float mirrored = SdfProgramEvaluator.Evaluate(
                    operations.AsReadOnly(), 5, new float3(-4f, 0f, 0f), 0f, allowCulling: false);
                Assert.Less(mirrored, 0f);
            }
            finally
            {
                operations.Dispose();
            }
        }

        [Test]
        public void CompilePortable_EllipsoidOutsideAabbStillMatchesReference()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart
            {
                Id = "ellipsoid",
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition
                {
                    Type = ShapeType.Ellipsoid,
                    PrimarySize = 1f,
                    EllipsoidRadii = new Vector3(10f, 1f, 1f),
                    SmoothBlendRadius = 0f,
                },
                Appearance = AppearanceDefinition.Default,
            });

            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            {
                Vector3 point = new Vector3(15f, 5f, 0f);
                float reference = SdfProgramEvaluator.EvaluateReference(
                    program, new float3(point.x, point.y, point.z));
                float fast = Evaluate(program, point);

                Assert.IsTrue(!float.IsNaN(reference) && !float.IsInfinity(reference),
                    "The approximate ellipsoid SDF must be finite at the regression point.");
                Assert.AreEqual(reference, fast, 1e-5f,
                    "An ellipsoid's AABB is not a safe culling proof; fast evaluation must not return +inf here.");
            }
        }

        [Test]
        public void SamplePortable_EllipsoidRoot_RegionShortcutNeverEarlyExits()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.Bounds = new BoundsDefinition { MaxX = 4f, MaxY = 4f, MaxZ = 4f };
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 1f };
            definition.AddPart(new CreaturePart
            {
                Id = "ellipsoid",
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition
                {
                    Type = ShapeType.Ellipsoid,
                    PrimarySize = 1f,
                    EllipsoidRadii = new Vector3(10f, 1f, 1f),
                    SmoothBlendRadius = 0f,
                },
                Appearance = AppearanceDefinition.Default,
            });

            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            using (DensityGrid grid = DensityGrid.SamplePortable(program, definition.Bounds, definition.Generation))
            {
                // The ellipsoid's world AABB is (10,1,1); many sampled corners (for
                // example (0,3,0)) lie outside that AABB yet carry a finite
                // approximate SDF. If the root-region shortcut used the AABB alone,
                // ignoring Cullable, it would pre-fill +inf at those corners and open
                // a hole. The shortcut must be disabled for a non-Cullable root, so
                // no sample may be +inf where the reference field is finite.
                for (int z = 0; z <= grid.CellsZ; z++)
                for (int y = 0; y <= grid.CellsY; y++)
                for (int x = 0; x <= grid.CellsX; x++)
                {
                    float sample = grid.GetSample(x, y, z);
                    Vector3 point = grid.CornerPosition(x, y, z);
                    float reference = SdfProgramEvaluator.EvaluateReference(
                        program, new float3(point.x, point.y, point.z));
                    Assert.IsFalse(float.IsInfinity(sample) && !float.IsInfinity(reference),
                        $"Ellipsoid-root region shortcut early-exited corner ({x},{y},{z}).");
                }
            }
        }

        [Test]
        public void CompilePortable_CompositeEllipsoid_ProvidesPotentialInfluenceEnvelope()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart
            {
                Id = "ellipsoid",
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition
                {
                    Type = ShapeType.Ellipsoid,
                    EllipsoidRadii = new Vector3(2f, 1f, 1f),
                    SmoothBlendRadius = 0.25f,
                },
                Appearance = AppearanceDefinition.Default,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "sphere",
                Transform = new TransformData { Position = new Vector3(2.5f, 0f, 0f) },
                Shape = new ShapeDefinition
                {
                    Type = ShapeType.Sphere,
                    Radius = 0.5f,
                    SmoothBlendRadius = 0.25f,
                },
                Appearance = AppearanceDefinition.Default,
            });

            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            {
                Assert.IsTrue(program.HasPotentialBounds,
                    "A composite root must expose a conservative potential-influence envelope.");

                float3 outsideOrdinaryEllipsoidAabb = new float3(0f, 1.2f, 0f);
                float reference = SdfProgramEvaluator.EvaluateReference(program, outsideOrdinaryEllipsoidAabb);
                Assert.IsTrue(!float.IsNaN(reference) && !float.IsInfinity(reference));
                Assert.That(outsideOrdinaryEllipsoidAabb.y, Is.GreaterThan(1f));
                Assert.That(outsideOrdinaryEllipsoidAabb.y, Is.LessThan(program.PotentialMaxBound.y));
                float fast = SdfProgramEvaluator.Evaluate(program, outsideOrdinaryEllipsoidAabb);
                Assert.IsTrue(!float.IsNaN(fast) && !float.IsInfinity(fast),
                    "The exact ellipsoid field must remain evaluable inside the potential envelope.");
            }
        }

        [Test]
        public void DensityGrid_EstimateGradient_UsesOneSidedFiniteDifferenceAtCullBoundary()
        {
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(Sphere("sphere", Vector3.zero)))
            using (DensityGrid grid = DensityGrid.SamplePortable(
                program,
                new BoundsDefinition { MaxX = 2f, MaxY = 2f, MaxZ = 2f },
                new GenerationSettings { VoxelsPerUnit = 2f }))
            {
                // DefaultSphere has Radius 0.5, so the finite surface is at x = 0.5.
                // The neighboring corner at world x = 1.0 lies outside the sphere's
                // inflated cull AABB and reads +inf. Sampling at the surface therefore
                // exercises the one-sided finite difference (finite center, +inf next).
                Vector3 gradient = grid.EstimateGradient(new Vector3(0.5f, 0f, 0f));

                Assert.IsTrue(!float.IsNaN(gradient.x) && !float.IsInfinity(gradient.x));
                Assert.IsTrue(!float.IsNaN(gradient.y) && !float.IsInfinity(gradient.y));
                Assert.IsTrue(!float.IsNaN(gradient.z) && !float.IsInfinity(gradient.z));
                Assert.Greater(gradient.x, 0.5f,
                    "The finite one-sided derivative at the sphere surface must preserve the outward direction.");
            }
        }

        [Test]
        public void CompilePortable_NullDefinitionThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => SdfProgramBuilder.CompilePortable(null));
        }

        [Test]
        public void CompilePortable_CurrentSchemaSphereIgnoresLegacyPrimarySize()
        {
            ShapeDefinition shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.25f, Radius = 1.5f, SmoothBlendRadius = 0f };
            CreatureDefinition first = CreatureDefinition.CreateEmpty();
            first.AddPart(new CreaturePart { Id = "sphere", Transform = TransformData.Identity, Shape = shape, Appearance = AppearanceDefinition.Default });
            shape.PrimarySize = 4f;
            CreatureDefinition second = CreatureDefinition.CreateEmpty();
            second.AddPart(new CreaturePart { Id = "sphere", Transform = TransformData.Identity, Shape = shape, Appearance = AppearanceDefinition.Default });
            using (SdfProgram a = SdfProgramBuilder.CompilePortable(first))
            using (SdfProgram b = SdfProgramBuilder.CompilePortable(second))
                Assert.AreEqual(Evaluate(a, Vector3.right), Evaluate(b, Vector3.right), 1e-4f);
        }
    }
}
