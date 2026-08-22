using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Morphology.Extraction;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Every expected result in this file was worked out by hand from
    /// CubeContourResolver's own rules (see the class-level derivation comments
    /// there and in AsymptoticDecider) — these are not "does it look plausible"
    /// smoke tests, they are specific predicted outputs for specific inputs.
    /// </summary>
    [TestFixture]
    public class CubeContourResolverTests
    {
        private static Vector3[] UnitCubePositions()
        {
            var positions = new Vector3[8];
            for (int i = 0; i < 8; i++) positions[i] = CubeTopology.CornerOffsets[i];
            return positions;
        }

        [Test]
        public void SingleCornerInside_ProducesOneTriangleUsingItsThreeIncidentEdges()
        {
            // Corner 0 alone is inside (negative); everything else is outside.
            float[] densities = { -1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

            List<List<CubeContourResolver.LoopVertex>> loops =
                CubeContourResolver.ResolveLoops(densities, UnitCubePositions());

            Assert.AreEqual(1, loops.Count);
            Assert.AreEqual(3, loops[0].Count);

            var edgeSet = new HashSet<int>(loops[0].Select(v => v.EdgeIndex));
            // Edges (0,1)=0, (0,2)=4, (0,4)=8 are exactly the 3 edges touching corner 0.
            CollectionAssert.AreEquivalent(new[] { 0, 4, 8 }, edgeSet);
        }

        [Test]
        public void AmbiguousFace_WeakDiagonalDominant_ProducesTwoSeparateTriangles()
        {
            // Corners 0 and 3 inside (diagonal on the NegZ face), corners 1,2
            // outside with SMALL magnitude relative to 0,3 -> AsymptoticDecider
            // resolves this as "separated" (see AsymptoticDeciderTests for the
            // matching direct math). Top face (4-7) is uniformly outside.
            float[] densities = { -1f, 1f, 1f, -1f, 1f, 1f, 1f, 1f };

            List<List<CubeContourResolver.LoopVertex>> loops =
                CubeContourResolver.ResolveLoops(densities, UnitCubePositions());

            Assert.AreEqual(2, loops.Count, "Expected two separate corner-cutoff triangles.");
            Assert.IsTrue(loops.All(loop => loop.Count == 3));

            var loopEdgeSets = loops.Select(loop => new HashSet<int>(loop.Select(v => v.EdgeIndex))).ToList();

            // One loop cuts off corner 0 (edges 0,4,8), the other cuts off corner 3 (edges 1,5,11).
            var expectedA = new HashSet<int> { 0, 4, 8 };
            var expectedB = new HashSet<int> { 1, 5, 11 };

            bool matchesExpected =
                (loopEdgeSets[0].SetEquals(expectedA) && loopEdgeSets[1].SetEquals(expectedB)) ||
                (loopEdgeSets[0].SetEquals(expectedB) && loopEdgeSets[1].SetEquals(expectedA));

            Assert.IsTrue(matchesExpected, "Loops did not match the two expected corner-cutoff triangles.");
        }

        [Test]
        public void AmbiguousFace_StrongDiagonalDominant_ProducesOneConnectedHexagon()
        {
            // Same sign pattern as the previous test, but corners 0,3 now have
            // LARGER magnitude than 1,2 -> AsymptoticDecider flips to "connected
            // through the middle": the same two inside corners now form a single
            // connected band instead of two separate cutoffs.
            float[] densities = { -10f, 1f, 1f, -10f, 1f, 1f, 1f, 1f };

            List<List<CubeContourResolver.LoopVertex>> loops =
                CubeContourResolver.ResolveLoops(densities, UnitCubePositions());

            Assert.AreEqual(1, loops.Count,
                "Expected a single connected loop once the decider flips to 'connected through the middle'.");
            Assert.AreEqual(6, loops[0].Count, "Expected a hexagonal loop using all 6 crossed edges.");

            var edgeSet = new HashSet<int>(loops[0].Select(v => v.EdgeIndex));
            CollectionAssert.AreEquivalent(new[] { 0, 1, 4, 5, 8, 11 }, edgeSet);
        }

        [Test]
        public void SharedFace_BothOrientationsOfTheSameDataProduceTheSameFaceDecision()
        {
            // The property that actually eliminates holes: the decider's result
            // for a face depends only on that face's 4 corner values, not on
            // which cube (or which order the corners are supplied in) is asking.
            // This directly exercises that by calling the decider with the same
            // four physical values however a neighboring cube might present them.
            float v00 = -10f, v10 = 1f, v11 = -10f, v01 = 1f;

            bool decisionFromThisCube = AsymptoticDecider.DiagonalConnectsThroughMiddle(v00, v10, v11, v01);
            bool decisionFromNeighborCube = AsymptoticDecider.DiagonalConnectsThroughMiddle(v11, v01, v00, v10);

            Assert.AreEqual(decisionFromThisCube, decisionFromNeighborCube,
                "A shared face must resolve identically regardless of which cube evaluates it.");
        }

        [Test]
        public void AllCornersSameSign_ProducesNoLoops()
        {
            float[] densities = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
            List<List<CubeContourResolver.LoopVertex>> loops =
                CubeContourResolver.ResolveLoops(densities, UnitCubePositions());
            Assert.AreEqual(0, loops.Count);
        }
    }
}
