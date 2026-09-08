using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology.Extraction;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class MeshExtractionResultContractTests
    {
        [Test]
        public void ComputeAngleWeightedNormals_RejectsDuplicateTriangleVertexIndices()
        {
            var result = new MeshExtractionResult();
            result.Positions.Add(Vector3.zero);
            result.Positions.Add(Vector3.right);
            result.Triangles.Add(0);
            result.Triangles.Add(1);
            result.Triangles.Add(1);

            Assert.Throws<DomainException>(() => result.ComputeAngleWeightedNormals());
        }

        [Test]
        public void ToUnityMesh_RejectsDuplicateTriangleVertexIndices()
        {
            var result = new MeshExtractionResult();
            result.Positions.Add(Vector3.zero);
            result.Positions.Add(Vector3.right);
            result.Triangles.Add(0);
            result.Triangles.Add(1);
            result.Triangles.Add(1);

            Assert.Throws<DomainException>(() => result.ToUnityMesh());
        }
    }
}
