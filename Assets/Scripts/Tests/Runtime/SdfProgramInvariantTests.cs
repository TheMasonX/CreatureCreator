using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class SdfProgramInvariantTests
    {
        [Test]
        public void Constructor_RejectsUnknownOperationType()
        {
            var operations = new NativeArray<SdfOperation>(1, Allocator.Persistent);
            operations[0] = SdfOperation.Primitive((SdfOperationType)999, float3.zero);

            Assert.Throws<DomainException>(() => new SdfProgram(
                operations, 0, 0f));
            Assert.IsFalse(operations.IsCreated, "Rejected program construction must dispose owned native operations.");
        }

        [Test]
        public void Constructor_RejectsForwardChildReference()
        {
            var operations = new NativeArray<SdfOperation>(2, Allocator.Persistent);
            operations[0] = new SdfOperation
            {
                Type = SdfOperationType.Transform,
                A = 1,
            };
            operations[1] = SdfOperation.Primitive(SdfOperationType.Sphere, new float3(1f, 0f, 0f));

            Assert.Throws<DomainException>(() => new SdfProgram(
                operations, 0, 0f));
            Assert.IsFalse(operations.IsCreated);
        }

        [Test]
        public void Constructor_RejectsNegativeInfluenceRadius()
        {
            var operations = new NativeArray<SdfOperation>(1, Allocator.Persistent);
            operations[0] = SdfOperation.Primitive(SdfOperationType.Sphere, new float3(1f, 0f, 0f));

            Assert.Throws<DomainException>(() => new SdfProgram(
                operations, 0, -1f));
            Assert.IsFalse(operations.IsCreated);
        }

        [Test]
        public void Constructor_RejectsInvalidPotentialBounds()
        {
            var operations = new NativeArray<SdfOperation>(1, Allocator.Persistent);
            operations[0] = SdfOperation.Primitive(SdfOperationType.Sphere, new float3(1f, 0f, 0f));

            Assert.Throws<DomainException>(() => new SdfProgram(
                operations,
                0,
                0f,
                hasPotentialBounds: true,
                potentialMinBound: new float3(1f, 2f, 3f),
                potentialMaxBound: new float3(0f, 2f, 3f)));
            Assert.IsFalse(operations.IsCreated);
        }
    }
}
