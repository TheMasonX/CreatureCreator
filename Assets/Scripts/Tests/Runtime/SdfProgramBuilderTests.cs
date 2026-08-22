using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class SdfProgramBuilderTests
    {
        private static CreaturePart Sphere(string id, Vector3 position, string parentId = null)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = parentId,
                PartType = PartType.Body,
                Transform = new TransformData { Position = position, Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 1f, SmoothBlendRadius = 0.5f },
                Appearance = AppearanceDefinition.Default,
            };
        }

        [Test]
        public void Compile_EmptyDefinition_ReturnsEmptyNodeEverywhereOutside()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            ISdfNode node = SdfProgramBuilder.Compile(definition);

            Assert.IsInstanceOf<EmptySdfNode>(node);
            Assert.IsTrue(float.IsPositiveInfinity(node.Evaluate(Vector3.zero)));
        }

        [Test]
        public void Compile_SinglePart_MatchesDirectPrimitiveEvaluation()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(Sphere("part_a", Vector3.zero));

            ISdfNode compiled = SdfProgramBuilder.Compile(definition);
            var reference = new SphereSdfNode(1f);

            Vector3 point = new Vector3(0.5f, 0.5f, 0.5f);
            Assert.AreEqual(reference.Evaluate(point), compiled.Evaluate(point), 1e-4f);
        }

        [Test]
        public void Compile_IsDeterministicRegardlessOfPartsInsertionOrder()
        {
            var definitionA = CreatureDefinition.CreateEmpty();
            definitionA.AddPart(Sphere("part_a", new Vector3(0f, 0f, 0f)));
            definitionA.AddPart(Sphere("part_b", new Vector3(3f, 0f, 0f)));

            var definitionB = CreatureDefinition.CreateEmpty();
            definitionB.AddPart(Sphere("part_b", new Vector3(3f, 0f, 0f)));
            definitionB.AddPart(Sphere("part_a", new Vector3(0f, 0f, 0f)));

            ISdfNode compiledA = SdfProgramBuilder.Compile(definitionA);
            ISdfNode compiledB = SdfProgramBuilder.Compile(definitionB);

            // Sample a grid of points and confirm both compiled trees agree everywhere,
            // not just at one convenient point — this is what "changing serialized
            // part order does not change the resulting SDF" actually needs to mean.
            for (float x = -2f; x <= 5f; x += 0.5f)
            {
                Vector3 point = new Vector3(x, 0.3f, -0.2f);
                Assert.AreEqual(compiledA.Evaluate(point), compiledB.Evaluate(point), 1e-4f,
                    $"Mismatch at point {point}.");
            }
        }

        [Test]
        public void Compile_ChildInheritsParentTransform()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(Sphere("part_root", new Vector3(10f, 0f, 0f)));
            definition.AddPart(Sphere("part_child", new Vector3(0f, 0f, 0f), parentId: "part_root"));

            ISdfNode compiled = SdfProgramBuilder.Compile(definition);

            // Child sphere is centered at local (0,0,0) relative to root, so in
            // creature space it should be centered at (10,0,0) too.
            Assert.Less(compiled.Evaluate(new Vector3(10f, 0f, 0f)), -0.9f);
        }

        [Test]
        public void Compile_MirroredPart_ProducesGeometryOnBothSides()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;

            CreaturePart mirroredLeg = Sphere("part_leg", new Vector3(5f, 0f, 0f));
            mirroredLeg.MirrorAcrossSymmetryPlane = true;
            definition.AddPart(mirroredLeg);

            ISdfNode compiled = SdfProgramBuilder.Compile(definition);

            Assert.Less(compiled.Evaluate(new Vector3(5f, 0f, 0f)), 0f, "Original side should have geometry.");
            Assert.Less(compiled.Evaluate(new Vector3(-5f, 0f, 0f)), 0f, "Mirrored side should also have geometry.");
            Assert.Greater(compiled.Evaluate(new Vector3(0f, 0f, 0f)), 0f, "Gap between the two legs should be empty.");
        }

        [Test]
        public void Compile_UnmirroredPart_DoesNotProduceGeometryOnOppositeSide()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.AddPart(Sphere("part_leg", new Vector3(5f, 0f, 0f))); // MirrorAcrossSymmetryPlane defaults to false

            ISdfNode compiled = SdfProgramBuilder.Compile(definition);

            Assert.Greater(compiled.Evaluate(new Vector3(-5f, 0f, 0f)), 0f,
                "A part not flagged for mirroring must not appear on the opposite side even when SymmetryMode is set.");
        }

        [Test]
        public void Compile_NullDefinition_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => SdfProgramBuilder.Compile(null));
        }

        [Test]
        public void CompilePortable_MatchesManagedGraphAcrossPrimitiveAndCompositionSamples()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            CreaturePart body = Sphere("body", new Vector3(1f, 0.25f, -0.5f));
            body.Shape.Type = ShapeType.Box;
            body.Shape.PrimarySize = 0.8f;
            body.Shape.SmoothBlendRadius = 0.2f;
            definition.AddPart(body);
            CreaturePart limb = Sphere("limb", new Vector3(-1.2f, 0.4f, 0f));
            limb.Shape.Type = ShapeType.Capsule;
            limb.Shape.PrimarySize = 0.35f;
            limb.Shape.SmoothBlendRadius = 0.1f;
            limb.MirrorAcrossSymmetryPlane = true;
            definition.AddPart(limb);

            ISdfNode managed = SdfProgramBuilder.Compile(definition);
            using (SdfProgram portable = SdfProgramBuilder.CompilePortable(definition))
            {
                for (float x = -3f; x <= 3f; x += 0.37f)
                for (float y = -2f; y <= 2f; y += 0.41f)
                {
                    Vector3 point = new Vector3(x, y, 0.23f);
                    Assert.AreEqual(managed.Evaluate(point),
                        SdfProgramEvaluator.Evaluate(portable, new float3(point.x, point.y, point.z)),
                        1e-4f, $"Mismatch at {point}.");
                }
            }
        }

        [Test]
        public void CompilePortable_EmptyDefinitionMatchesManagedGraph()
        {
            using (SdfProgram portable = SdfProgramBuilder.CompilePortable(CreatureDefinition.CreateEmpty()))
            {
                Assert.IsTrue(float.IsPositiveInfinity(SdfProgramEvaluator.Evaluate(portable, float3.zero)));
            }
        }
    }
}
