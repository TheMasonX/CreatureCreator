using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class SdfProgramBuilderLimbTests
    {
        private static LimbChain Chain()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });
            return chain;
        }

        private static CreatureDefinition Definition(LimbChain chain, bool mirror)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = mirror ? SymmetryMode.MirrorAcrossXAxis : SymmetryMode.None;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(new CreaturePart { Id = "limb", ParentId = CreatureDefinition.BodyId, PartType = PartType.Limb,
                Transform = TransformData.Identity, Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default, MirrorAcrossSymmetryPlane = mirror, Limb = chain });
            return definition;
        }

        private static float Evaluate(SdfProgram program, Vector3 point)
        {
            return SdfProgramEvaluator.Evaluate(program, new float3(point.x, point.y, point.z));
        }

        [Test]
        public void CompilePortable_LimbProducesInsideAndOutsideSamples()
        {
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(Definition(Chain(), false)))
            {
                Assert.Less(Evaluate(program, new Vector3(0f, -0.5f, 0f)), 0f);
                Assert.Greater(Evaluate(program, new Vector3(0f, -0.5f, 3f)), 0f);
            }
        }

        [Test]
        public void CompilePortable_MirroredLimbProducesBothSides()
        {
            LimbChain chain = Chain();
            chain.Joints[1].Position = new Vector3(2f, 0f, 0f);
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(Definition(chain, true)))
            {
                Assert.Less(Evaluate(program, new Vector3(1.9f, 0f, 0f)), 0f);
                Assert.Less(Evaluate(program, new Vector3(-1.9f, 0f, 0f)), 0f);
            }
        }

        [Test]
        public void CompilePortable_LimbIgnoresShapeBlendRadius()
        {
            CreatureDefinition low = Definition(Chain(), false);
            CreatureDefinition high = Definition(Chain(), false);
            low.FindPart("limb").Shape.SmoothBlendRadius = 0.05f;
            high.FindPart("limb").Shape.SmoothBlendRadius = 0.5f;
            using (SdfProgram a = SdfProgramBuilder.CompilePortable(low))
            using (SdfProgram b = SdfProgramBuilder.CompilePortable(high))
                Assert.AreEqual(Evaluate(a, new Vector3(0f, -0.5f, 0f)), Evaluate(b, new Vector3(0f, -0.5f, 0f)), 1e-5f);
        }

        [Test]
        public void CompilePortable_LimbUsesAuthoredBlendRadius()
        {
            LimbChain hard = Chain();
            LimbChain soft = Chain();
            hard.BlendRadius = 0f;
            soft.BlendRadius = 0.5f;
            using (SdfProgram a = SdfProgramBuilder.CompilePortable(Definition(hard, false)))
            using (SdfProgram b = SdfProgramBuilder.CompilePortable(Definition(soft, false)))
            {
                bool differs = false;
                for (float y = -0.6f; y <= 0.2f && !differs; y += 0.1f)
                    differs = Mathf.Abs(Evaluate(a, new Vector3(0.5f, y, 0f)) - Evaluate(b, new Vector3(0.5f, y, 0f))) > 1e-3f;
                Assert.IsTrue(differs);
            }
        }
    }
}
