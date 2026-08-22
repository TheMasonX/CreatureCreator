using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Morphology.Extraction;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class CubeTopologyTests
    {
        [Test]
        public void EveryEdge_AppearsInExactlyTwoFaces()
        {
            var edgeFaceCount = new Dictionary<int, int>();
            foreach (int[] faceEdges in CubeTopology.FaceEdges)
            {
                foreach (int edge in faceEdges)
                {
                    edgeFaceCount[edge] = edgeFaceCount.TryGetValue(edge, out int c) ? c + 1 : 1;
                }
            }

            Assert.AreEqual(12, edgeFaceCount.Count, "All 12 edges should appear across the 6 faces.");
            foreach (KeyValuePair<int, int> entry in edgeFaceCount)
            {
                Assert.AreEqual(2, entry.Value, $"Edge {entry.Key} should border exactly 2 faces.");
            }
        }

        [Test]
        public void FaceEdges_MatchTheirCyclicFaceCorners()
        {
            for (int f = 0; f < CubeTopology.FaceCorners.Length; f++)
            {
                int[] corners = CubeTopology.FaceCorners[f];
                int[] edges = CubeTopology.FaceEdges[f];

                for (int k = 0; k < 4; k++)
                {
                    int expectedA = corners[k];
                    int expectedB = corners[(k + 1) % 4];
                    (int a, int b) actual = CubeTopology.EdgeCorners[edges[k]];

                    bool matches = (actual.a == expectedA && actual.b == expectedB)
                                   || (actual.a == expectedB && actual.b == expectedA);

                    Assert.IsTrue(matches,
                        $"Face {f} position {k}: expected edge between {expectedA} and {expectedB}, " +
                        $"but FaceEdges[{f}][{k}] (edge {edges[k]}) connects {actual.a}-{actual.b}.");
                }
            }
        }

        [Test]
        public void CornerGridOffsets_MatchCornerOffsets()
        {
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual((int)CubeTopology.CornerOffsets[i].x, CubeTopology.CornerGridOffsets[i].x);
                Assert.AreEqual((int)CubeTopology.CornerOffsets[i].y, CubeTopology.CornerGridOffsets[i].y);
                Assert.AreEqual((int)CubeTopology.CornerOffsets[i].z, CubeTopology.CornerGridOffsets[i].z);
            }
        }

        [Test]
        public void EveryFace_HasFourDistinctCorners()
        {
            foreach (int[] corners in CubeTopology.FaceCorners)
            {
                Assert.AreEqual(4, new HashSet<int>(corners).Count);
            }
        }
    }
}
