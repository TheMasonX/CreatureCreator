using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class SdfPrimitiveTests
    {
        private static float Evaluate(ShapeType type, Vector3 point, Vector3 parameters)
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = "primitive", Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = type, PrimarySize = parameters.x, Radius = parameters.x,
                    CapsuleHeight = parameters.y, CapsuleAxis = ShapeAxis.Y, EllipsoidRadii = parameters,
                    BoxHalfExtents = parameters }, Appearance = AppearanceDefinition.Default });
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
                return SdfProgramEvaluator.EvaluateReference(program, new float3(point.x, point.y, point.z));
        }

        [Test]
        public void Sphere_HasExpectedSignedDistance()
        {
            Assert.AreEqual(-1f, Evaluate(ShapeType.Sphere, Vector3.zero, Vector3.one), 1e-4f);
            Assert.AreEqual(0f, Evaluate(ShapeType.Sphere, Vector3.right * 2f, Vector3.one * 2f), 1e-4f);
            Assert.AreEqual(4f, Evaluate(ShapeType.Sphere, Vector3.right * 5f, Vector3.one), 1e-4f);
        }

        [Test]
        public void Box_HasExpectedSignedDistance()
        {
            Assert.Less(Evaluate(ShapeType.Box, Vector3.zero, Vector3.one), 0f);
            Assert.AreEqual(0f, Evaluate(ShapeType.Box, Vector3.right, Vector3.one), 1e-4f);
            Assert.AreEqual(Mathf.Sqrt(3f), Evaluate(ShapeType.Box, Vector3.one * 2f, Vector3.one), 1e-4f);
        }

        [Test]
        public void Capsule_HasExpectedSignedDistance()
        {
            Assert.AreEqual(-0.5f, Evaluate(ShapeType.Capsule, Vector3.zero, new Vector3(0.5f, 1f, 0f)), 1e-4f);
            Assert.AreEqual(0f, Evaluate(ShapeType.Capsule, Vector3.up, new Vector3(0.5f, 1f, 0f)), 1e-4f);
            Assert.AreEqual(0f, Evaluate(ShapeType.Capsule, Vector3.right * 0.5f, new Vector3(0.5f, 1f, 0f)), 1e-4f);
        }

        [Test]
        public void Ellipsoid_UsesAllThreeRadii()
        {
            Vector3 radii = new Vector3(2f, 1f, 0.5f);
            Assert.AreEqual(0f, Evaluate(ShapeType.Ellipsoid, Vector3.up, radii), 1e-4f);
            Assert.AreEqual(0f, Evaluate(ShapeType.Ellipsoid, Vector3.forward * 0.5f, radii), 1e-4f);
            Assert.Greater(Evaluate(ShapeType.Ellipsoid, Vector3.forward * 0.6f, radii), 0f);
        }
    }
}