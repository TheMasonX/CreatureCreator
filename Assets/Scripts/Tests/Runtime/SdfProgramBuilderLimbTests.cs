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

        /// <summary>
        /// REGRESSION (2026-08-23): the portable mirrored-limb path originally
        /// negated each ball's LOCAL x and reused the UNMIRRORED part matrix. For
        /// a limb placed away from the creature X plane (the normal authored case,
        /// e.g. a leg at x = 0.5), that put the "mirror" back on the SAME side —
        /// the creature-space mirror silently no-oped. The fix left-multiplies the
        /// part matrix by the X reflection, so the mirrored side must land across
        /// the creature X plane and agree with the managed SymmetryNode path.
        /// </summary>
        [Test]
        public void CompilePortable_MirroredLimbAtXOffset_AgreesWithManagedOnBothSides()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });

            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(new CreaturePart
            {
                Id = "part_arm",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Arm,
                // The limb is placed away from the X = 0 plane, as a real arm/leg is.
                Transform = new TransformData
                {
                    Position = new Vector3(0.5f, 0f, 0f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
                Limb = chain,
            });

            ISdfNode managed = SdfProgramBuilder.Compile(definition);
            using (SdfProgram portable = SdfProgramBuilder.CompilePortable(definition))
            {
                // The mirrored side must be inside BOTH paths (the original bug
                // left the portable mirrored side outside).
                Assert.Less(managed.Evaluate(new Vector3(-0.5f, -0.5f, 0f)), 0f, "Managed mirror must exist on the -X side.");
                Assert.Less(
                    SdfProgramEvaluator.Evaluate(portable, new float3(-0.5f, -0.5f, 0f)), 0f,
                    "Portable mirror must exist on the -X side (regression: it used to no-op for an x-offset part).");

                for (float x = -2.5f; x <= 2.5f; x += 0.25f)
                for (float y = -1.8f; y <= 0.5f; y += 0.25f)
                {
                    Vector3 point = new Vector3(x, y, 0f);
                    Assert.AreEqual(
                        managed.Evaluate(point),
                        SdfProgramEvaluator.Evaluate(portable, new float3(point.x, point.y, point.z)),
                        1e-4f,
                        $"Managed and portable mirrored fields must agree at {point}.");
                }
            }
        }

        [Test]
        public void CompilePortable_MirroredLimbWithRotatedPartTransform_AgreesWithManaged()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = new Vector3(0f, 0f, 0f) });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0.6f, -1.1f, 0.2f) });
            chain.Joints.Add(new LimbJoint { Id = 3, Position = new Vector3(1.1f, -1.4f, 0.8f) });

            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(new CreaturePart
            {
                Id = "part_rotated_limb",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Arm,
                Transform = new TransformData
                {
                    Position = new Vector3(0.7f, 0.4f, 0.2f),
                    Rotation = Quaternion.Euler(20f, 35f, 15f),
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
                Limb = chain,
            });

            ISdfNode managed = SdfProgramBuilder.Compile(definition);
            using (SdfProgram portable = SdfProgramBuilder.CompilePortable(definition))
            {
                for (float x = -2.5f; x <= 2.5f; x += 0.25f)
                for (float y = -2.0f; y <= 1.0f; y += 0.25f)
                for (float z = -1.5f; z <= 1.5f; z += 0.25f)
                {
                    Vector3 point = new Vector3(x, y, z);
                    Assert.AreEqual(
                        managed.Evaluate(point),
                        SdfProgramEvaluator.Evaluate(portable, new float3(point.x, point.y, point.z)),
                        1e-4f,
                        $"Managed and portable rotated mirrored fields must agree at {point}.");
                }
            }
        }

        /// <summary>
        /// CC-049: a limb's part-to-field union must not depend on the inert
        /// Shape.SmoothBlendRadius. Changing that inert field must have zero
        /// observable effect on the generated field (managed and portable).
        /// </summary>
        [Test]
        public void Compile_LimbField_IsIndependentOfInertShapeBlendRadius()
        {
            var shapeLow = ShapeDefinition.DefaultSphere;
            shapeLow.SmoothBlendRadius = 0.05f;
            var shapeHigh = ShapeDefinition.DefaultSphere;
            shapeHigh.SmoothBlendRadius = 0.5f;

            CreatureDefinition low = DefinitionWithLimb(StraightDown(), mirror: false);
            low.FindPart("part_leg").Shape = shapeLow;
            CreatureDefinition high = DefinitionWithLimb(StraightDown(), mirror: false);
            high.FindPart("part_leg").Shape = shapeHigh;

            ISdfNode managedLow = SdfProgramBuilder.Compile(low);
            ISdfNode managedHigh = SdfProgramBuilder.Compile(high);
            using (SdfProgram portableLow = SdfProgramBuilder.CompilePortable(low))
            using (SdfProgram portableHigh = SdfProgramBuilder.CompilePortable(high))
            {
                for (float x = -2f; x <= 2f; x += 0.3f)
                for (float y = -2f; y <= 0.5f; y += 0.3f)
                for (float z = -1.5f; z <= 1.5f; z += 0.3f)
                {
                    Vector3 point = new Vector3(x, y, z);
                    Assert.AreEqual(managedLow.Evaluate(point), managedHigh.Evaluate(point), 1e-5f,
                        $"Managed limb field must be independent of inert Shape blend at {point}.");
                    Assert.AreEqual(
                        SdfProgramEvaluator.Evaluate(portableLow, new float3(point.x, point.y, point.z)),
                        SdfProgramEvaluator.Evaluate(portableHigh, new float3(point.x, point.y, point.z)),
                        1e-5f,
                        $"Portable limb field must be independent of inert Shape blend at {point}.");
                }
            }
        }

        /// <summary>
        /// CC-049: the explicit LimbChain.BlendRadius is the authority for the
        /// limb's part-to-field union. Changing it must change the field near the
        /// body/limb seam.
        /// </summary>
        [Test]
        public void Compile_LimbField_ChangesWithLimbChainBlendRadius()
        {
            LimbChain hard = StraightDown();
            hard.BlendRadius = 0f;
            LimbChain soft = StraightDown();
            soft.BlendRadius = 0.5f;

            ISdfNode hardField = SdfProgramBuilder.Compile(DefinitionWithLimb(hard, mirror: false));
            ISdfNode softField = SdfProgramBuilder.Compile(DefinitionWithLimb(soft, mirror: false));

            bool differs = false;
            for (float x = -0.8f; x <= 0.8f && !differs; x += 0.1f)
            for (float y = -0.6f; y <= 0.2f && !differs; y += 0.1f)
            {
                float a = hardField.Evaluate(new Vector3(x, y, 0f));
                float b = softField.Evaluate(new Vector3(x, y, 0f));
                if (Mathf.Abs(a - b) > 1e-3f)
                {
                    differs = true;
                    break;
                }
            }
            Assert.IsTrue(differs,
                "The authored LimbChain.BlendRadius must affect the field near the body/limb seam.");
        }

        /// <summary>
        /// CC-049: managed and portable limb fields must agree when an explicit
        /// LimbChain.BlendRadius is authored. Unmirrored on purpose — mirrored
        /// limb parity at far points has its own regression test
        /// (CompilePortable_MirroredLimbAtXOffset_AgreesWithManagedOnBothSides);
        /// this test isolates the explicit-blend union contract. Tolerance 1e-3:
        /// wider authored blends amplify the known Mathf.Lerp (a+(b-a)h) vs
        /// math.lerp (b+(a-b)h) rounding difference in the smooth-min polynomial
        /// to ~2e-4 at far points (verified: default 0.1 blend is bit-identical;
        /// 0.4 blend differs by 1.8e-4). A real contract break would differ by O(1).
        /// </summary>
        [Test]
        public void CompilePortable_ExplicitLimbBlend_AgreesWithManaged()
        {
            var chain = new LimbChain();
            chain.BlendRadius = 0.4f;
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1.2f, 0f) });

            CreatureDefinition definition = DefinitionWithLimb(chain, mirror: false);

            ISdfNode managed = SdfProgramBuilder.Compile(definition);
            using (SdfProgram portable = SdfProgramBuilder.CompilePortable(definition))
            {
                for (float x = -3f; x <= 3f; x += 0.35f)
                for (float y = -2.5f; y <= 0.5f; y += 0.35f)
                for (float z = -1.5f; z <= 1.5f; z += 0.35f)
                {
                    Vector3 point = new Vector3(x, y, z);
                    Assert.AreEqual(
                        managed.Evaluate(point),
                        SdfProgramEvaluator.Evaluate(portable, new float3(point.x, point.y, point.z)),
                        1e-3f,
                        $"Managed and portable limb fields with explicit blend must agree at {point}.");
                }
            }
        }
    }
}
