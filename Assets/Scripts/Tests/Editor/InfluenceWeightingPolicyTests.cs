using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Common;
using UnityEngine;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// TSK-0147: the tunable influence weighting policy. The chain-aware gate is what
    /// stops a downstream bone (a forearm) from reaching upstream into its parent
    /// (the upper arm) while still blending across the joint itself.
    /// </summary>
    [TestFixture]
    public class InfluenceWeightingPolicyTests
    {
        private const float Radius = 0.2f;
        private const string ArmDomain = "arm";

        [Test]
        public void DefaultPolicy_EqualsTSK0131RadialValues_WithLocalityOn()
        {
            InfluenceWeightingPolicy policy = InfluenceWeightingPolicy.Default;
            Assert.AreEqual(InfluenceWeightingPolicy.DefaultRadiusScale, policy.RadiusScale);
            Assert.AreEqual(InfluenceWeightingPolicy.DefaultFalloffPower, policy.FalloffPower);
            Assert.AreEqual(InfluenceWeightingPolicy.DefaultBoneRadius, policy.FallbackBoneRadius);
            Assert.IsTrue(policy.ChainAwareLocality);
            Assert.AreEqual(InfluenceWeightingPolicy.DefaultLongitudinalBlendMarginRadii, policy.LongitudinalBlendMarginRadii);
        }

        [Test]
        public void Policy_OutOfRangeValues_AreRejected()
        {
            Assert.Throws<DomainException>(() => new InfluenceWeightingPolicy(0f, 2f, 0.5f, true, 1f));
            Assert.Throws<DomainException>(() => new InfluenceWeightingPolicy(3f, 0f, 0.5f, true, 1f));
            Assert.Throws<DomainException>(() => new InfluenceWeightingPolicy(3f, 2f, 0f, true, 1f));
            Assert.Throws<DomainException>(() => new InfluenceWeightingPolicy(3f, 2f, 0.5f, true, -1f));
            Assert.Throws<DomainException>(() => new InfluenceWeightingPolicy(float.NaN, 2f, 0.5f, true, 1f));
        }

        [Test]
        public void DefaultPolicy_VertexUpTheUpperArm_HasNoForearmWeight()
        {
            List<BoneSegmentInfluence> segments = BuildArm();
            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(
                segments, new[] { new Vector3(0f, -0.5f, 0f) }, null, InfluenceWeightingPolicy.Default);

            Assert.AreEqual(0f, WeightForBone(weights[0], 1), 1e-6f,
                "the forearm bone must not influence geometry halfway up the upper arm");
        }

        [Test]
        public void LegacyPolicy_VertexUpTheUpperArm_StillBlendsForearm()
        {
            // Discriminating control: without the chain-aware gate the radial tube does
            // reach up the upper arm, which is the defect TSK-0147 records.
            List<BoneSegmentInfluence> segments = BuildArm();
            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(
                segments, new[] { new Vector3(0f, -0.5f, 0f) }, null, InfluenceWeightingPolicy.Legacy);

            Assert.Greater(WeightForBone(weights[0], 1), 0f,
                "the legacy radial model is expected to leak into the upper arm");
        }

        [Test]
        public void DefaultPolicy_VertexAtTheJoint_StillBlendsBothBones()
        {
            List<BoneSegmentInfluence> segments = BuildArm();
            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(
                segments, new[] { new Vector3(0f, -1f, 0f) }, null, InfluenceWeightingPolicy.Default);

            Assert.Greater(WeightForBone(weights[0], 0), 0.1f, "the upper arm must still hold the joint");
            Assert.Greater(WeightForBone(weights[0], 1), 0.1f, "the forearm must still hold the joint");
        }

        [Test]
        public void DefaultPolicy_WeightsSumToOne()
        {
            List<BoneSegmentInfluence> segments = BuildArm();
            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(
                segments,
                new[] { new Vector3(0f, -0.1f, 0f), new Vector3(0f, -0.9f, 0f), new Vector3(0f, -1.5f, 0f) },
                null,
                InfluenceWeightingPolicy.Default);

            for (int v = 0; v < weights.Length; v++)
            {
                float total = 0f;
                for (int i = 0; i < weights[v].Length; i++) total += weights[v][i].Weight;
                Assert.AreEqual(1f, total, 1e-4f, $"vertex {v} weights must be normalized");
            }
        }

        [Test]
        public void WeightFor_RespectsChainAwareGate()
        {
            var forearm = new BoneSegmentInfluence(1, false, new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f), Radius, ArmDomain);
            var upperArmPoint = new Vector3(0f, -0.5f, 0f);

            Assert.AreEqual(0f, ImplicitSurfaceWeightAuthoring.WeightFor(upperArmPoint, forearm, InfluenceWeightingPolicy.Default), 1e-6f);
            Assert.Greater(ImplicitSurfaceWeightAuthoring.WeightFor(upperArmPoint, forearm, InfluenceWeightingPolicy.Legacy), 0f);
        }

        [Test]
        public void BlendMargin_WidensTheJointBand()
        {
            var forearm = new BoneSegmentInfluence(1, false, new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f), Radius, ArmDomain);
            // 1.5 radii upstream of the joint (the forearm runs toward -Y, so upstream
            // is +Y from its start): outside the default one-radius band.
            var justUpstream = new Vector3(0f, -1f + (Radius * 1.5f), 0f);

            Assert.AreEqual(0f,
                ImplicitSurfaceWeightAuthoring.WeightFor(justUpstream, forearm, InfluenceWeightingPolicy.Default), 1e-6f);

            var wideBand = new InfluenceWeightingPolicy(
                InfluenceWeightingPolicy.DefaultRadiusScale,
                InfluenceWeightingPolicy.DefaultFalloffPower,
                InfluenceWeightingPolicy.DefaultBoneRadius,
                chainAwareLocality: true,
                longitudinalBlendMarginRadii: 2f);
            Assert.Greater(ImplicitSurfaceWeightAuthoring.WeightFor(justUpstream, forearm, wideBand), 0f,
                "a wider blend band must re-admit geometry just upstream of the joint");
        }

        [Test]
        public void DefaultPolicy_VertexOutsideOwnSpanButInsideTube_StillGetsInfluence()
        {
            // Totality: the gate may not leave a vertex unweighted. A single bone whose
            // only nearby geometry sits behind its own start must still bind, using the
            // ungated radial fallback for that vertex.
            var onlySegment = new List<BoneSegmentInfluence>
            {
                new BoneSegmentInfluence(0, false, new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f), Radius, ArmDomain),
            };
            var vertex = new Vector3(0.2f, -0.6f, 0f);

            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(
                onlySegment, new[] { vertex }, null, InfluenceWeightingPolicy.Default);

            Assert.AreEqual(1, weights[0].Length);
            Assert.AreEqual(0, weights[0][0].BoneIndex);
            Assert.AreEqual(1f, weights[0][0].Weight, 1e-4f);
        }

        private static List<BoneSegmentInfluence> BuildArm()
        {
            return new List<BoneSegmentInfluence>
            {
                new BoneSegmentInfluence(0, false, new Vector3(0f, 0f, 0f), new Vector3(0f, -1f, 0f), Radius, ArmDomain),
                new BoneSegmentInfluence(1, false, new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f), Radius, ArmDomain),
            };
        }

        private static float WeightForBone(VertexInfluence[] influences, int boneIndex)
        {
            for (int i = 0; i < influences.Length; i++)
            {
                if (influences[i].BoneIndex == boneIndex) return influences[i].Weight;
            }
            return 0f;
        }
    }
}
