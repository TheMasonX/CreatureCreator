using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Phase 5 (CC-018) SDF integration for limb chains: managed + portable
    /// compile, symmetry, and parity. Runtime assembly — invoke via execute_code,
    /// not the MCP runner.
    /// </summary>
    [TestFixture]
    public class SdfProgramBuilderLimbTests
    {
        private static CreatureDefinition DefinitionWithLimb(LimbChain chain, bool mirror)
        {
            var definition = CreatureDefinition.CreateEmpty();
            if (mirror) definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(new CreaturePart
            {
                Id = "part_leg",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = mirror,
                Limb = chain,
            });
            return definition;
        }

        private static LimbChain StraightDown()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });
            return chain;
        }

        [Test]
        public void Compile_LimbPart_ProducesFieldInsideChainAndOutsideFarAway()
        {
            ISdfNode compiled = SdfProgramBuilder.Compile(DefinitionWithLimb(StraightDown(), mirror: false));

            Assert.Less(compiled.Evaluate(new Vector3(0f, -0.5f, 0f)), 0f,
                "A point mid-chain must be inside the limb field.");
            Assert.Greater(compiled.Evaluate(new Vector3(0f, -0.5f, 3f)), 0f,
                "A point far from the limb must be outside.");
        }

        [Test]
        public void Compile_MirroredLimb_ProducesGeometryOnBothSides()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(2f, 0f, 0f) });

            ISdfNode compiled = SdfProgramBuilder.Compile(DefinitionWithLimb(chain, mirror: true));

            Assert.Less(compiled.Evaluate(new Vector3(1.9f, 0f, 0f)), 0f, "Original-side geometry.");
            Assert.Less(compiled.Evaluate(new Vector3(-1.9f, 0f, 0f)), 0f,
                "Mirroring across the X plane must reproduce the limb on the opposite side.");
        }

        [Test]
        public void Compile_UnmirroredLimb_DoesNotAppearOnOppositeSide()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(2f, 0f, 0f) });

            ISdfNode compiled = SdfProgramBuilder.Compile(DefinitionWithLimb(chain, mirror: false));

            Assert.Less(compiled.Evaluate(new Vector3(1.9f, 0f, 0f)), 0f);
            Assert.Greater(compiled.Evaluate(new Vector3(-1.9f, 0f, 0f)), 0f,
                "An unmirrored limb must not produce geometry on the opposite side.");
        }

        [Test]
        public void CompilePortable_MatchesManagedGraph_ForLimbChain()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1.2f, 0f) });
            chain.Joints.Add(new LimbJoint { Id = 3, Position = new Vector3(0.8f, -1.2f, 0f) });

            CreatureDefinition definition = DefinitionWithLimb(chain, mirror: true);

            ISdfNode managed = SdfProgramBuilder.Compile(definition);
            using (SdfProgram portable = SdfProgramBuilder.CompilePortable(definition))
            {
                for (float x = -3f; x <= 3f; x += 0.4f)
                for (float y = -2.5f; y <= 0.5f; y += 0.4f)
                for (float z = -1.5f; z <= 1.5f; z += 0.4f)
                {
                    Vector3 point = new Vector3(x, y, z);
                    Assert.AreEqual(
                        managed.Evaluate(point),
                        SdfProgramEvaluator.Evaluate(portable, new float3(point.x, point.y, point.z)),
                        1e-4f,
                        $"Managed and portable limb fields must agree at {point}.");
                }
            }
        }
    }
}
