using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Phase 4 (CC-018) derived metaball sampling. Runtime assembly — invoke via
    /// execute_code, not the MCP runner.
    /// </summary>
    [TestFixture]
    public class LimbMetaballSamplerTests
    {
        private static LimbChain StraightChain()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });
            return chain;
        }

        [Test]
        public void Sample_StraightChain_ProducesExpectedCountAndEndpoints()
        {
            // Segment length 1.0 at 0.1 spacing -> 10 samples + 1 terminal = 11.
            List<LimbMetaball> balls = LimbMetaballSampler.Sample(StraightChain());

            Assert.AreEqual(11, balls.Count);
            Assert.AreEqual(Vector3.zero, balls[0].Position);
            Assert.AreEqual(new Vector3(0f, -1f, 0f), balls[balls.Count - 1].Position);
            Assert.AreEqual(0.30f, balls[0].Radius, 1e-4f, "Root radius from the default profile at t = 0.");
            Assert.AreEqual(0.12f, balls[balls.Count - 1].Radius, 1e-4f, "Tip radius from the default profile at t = 1.");
        }

        [Test]
        public void Sample_IsDeterministic()
        {
            List<LimbMetaball> first = LimbMetaballSampler.Sample(StraightChain());
            List<LimbMetaball> second = LimbMetaballSampler.Sample(StraightChain());

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].Position, second[i].Position);
                Assert.AreEqual(first[i].Radius, second[i].Radius);
            }
        }

        [Test]
        public void Sample_DoesNotMutateChain()
        {
            LimbChain chain = StraightChain();
            List<LimbMetaball> ignored = LimbMetaballSampler.Sample(chain);

            Assert.AreEqual(2, chain.Joints.Count);
            Assert.AreEqual(Vector3.zero, chain.Joints[0].Position);
            Assert.AreEqual(new Vector3(0f, -1f, 0f), chain.Joints[1].Position);
            Assert.AreEqual(2, chain.Thickness.Keys.Count, "Sampling must never write back to the chain.");
        }

        [Test]
        public void Sample_BentChain_UsesNormalizedArcLength()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });
            chain.Joints.Add(new LimbJoint { Id = 3, Position = new Vector3(1f, -1f, 0f) });
            // total length 2.0 (two 1.0 segments); each segment samples 10 + the terminal.

            List<LimbMetaball> balls = LimbMetaballSampler.Sample(chain);

            Assert.AreEqual(21, balls.Count, "10 per segment + 1 terminal.");
            Assert.AreEqual(new Vector3(0f, -1f, 0f), balls[10].Position,
                "The first ball of segment 1 must sit exactly on the bend joint (t = 0.5).");
            // Segment 1's k=0 is at cumulative arc 1.0 / 2.0 = 0.5 -> default profile midpoint.
            Assert.AreEqual(0.30f + (0.12f - 0.30f) * 0.5f, balls[10].Radius, 1e-3f);
        }

        [Test]
        public void Sample_VariableThickness_MatchesProfileEvaluate()
        {
            var chain = StraightChain();
            var profile = new ThicknessProfile();
            profile.Keys.Add(new ThicknessKey { T = 0f, Value = 0.4f });
            profile.Keys.Add(new ThicknessKey { T = 0.5f, Value = 0.2f });
            profile.Keys.Add(new ThicknessKey { T = 1f, Value = 0.1f });
            chain.Thickness = profile;

            List<LimbMetaball> balls = LimbMetaballSampler.Sample(chain);

            // k=5 of 10 on the single segment is at frac 0.5 -> t = 0.5.
            Assert.AreEqual(0.2f, balls[5].Radius, 1e-4f);
            Assert.AreEqual(0.4f, balls[0].Radius, 1e-4f);
            Assert.AreEqual(0.1f, balls[balls.Count - 1].Radius, 1e-4f);
        }

        [Test]
        public void Sample_LongerChain_ProducesMoreMetaballs()
        {
            var shortChain = new LimbChain();
            shortChain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            shortChain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -0.5f, 0f) });

            var longChain = new LimbChain();
            longChain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            longChain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -2f, 0f) });

            Assert.Greater(LimbMetaballSampler.Sample(longChain).Count,
                LimbMetaballSampler.Sample(shortChain).Count,
                "Longer limbs must sample more metaballs without any DNA change.");
        }

        [Test]
        public void Sample_NullChain_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => LimbMetaballSampler.Sample(null));
        }

        [Test]
        public void Sample_EmptyChain_ThrowsDomainException()
        {
            var chain = new LimbChain();
            Assert.Throws<DomainException>(() => LimbMetaballSampler.Sample(chain));
        }
    }
}
