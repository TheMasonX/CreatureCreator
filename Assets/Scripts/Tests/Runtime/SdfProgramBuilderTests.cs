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
                    operations, 5, new float3(-4f, 0f, 0f), 0f, allowCulling: false);
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
        public void DensityGrid_EstimateGradient_UsesOneSidedFiniteDifferenceAtCullBoundary()
        {
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(Sphere("sphere", Vector3.zero)))
            using (DensityGrid grid = DensityGrid.SamplePortable(
                program,
                new BoundsDefinition { MaxX = 2f, MaxY = 2f, MaxZ = 2f },
                new GenerationSettings { VoxelsPerUnit = 2f }))
            {
                Vector3 gradient = grid.EstimateGradient(new Vector3(1f, 0f, 0f));

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
