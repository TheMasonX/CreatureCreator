using System;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Skeleton;
using SkeletonModel = ProceduralCreature.Skeleton.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class MorphologyInfluenceRadiusBridgeTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void BuildRadii_ValidBodyAndLimbMorphology_UsesCompactBodyAndLimbValues()
        {
            CreatureDefinition definition = CreateDefinition(
                bodyRadius: 0.85f,
                limb: CreateLimb(new[] { 0f, 1f }, new[] { 0.12f, 0.08f }));
            SkeletonModel skeleton = SkeletonInferrer.Infer(definition);
            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(skeleton);

            float[] first = MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(
                snapshot, ResolvedCreatureSnapshot.Resolve(definition));
            float[] second = MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(
                snapshot, ResolvedCreatureSnapshot.Resolve(definition));

            AssertFiniteAndDeterministic(first, second);
            Assert.That(first[snapshot.GetIndex(AnatomicalBodyRigLayout.BodyRootBoneId)],
                Is.EqualTo(0.85f).Within(Tolerance));
            Assert.That(first[snapshot.GetIndex("limb_j0")], Is.EqualTo(0.10f).Within(Tolerance));
        }

        [Test]
        public void BuildRadii_UnavailableMorphology_UsesDeterministicFiniteFallback()
        {
            CreatureDefinition definition = CreateDefinition(bodyRadius: null, limb: null);
            definition.Body.Samples.Clear();
            definition.AddPart(new CreaturePart
            {
                Id = "attachment",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Part,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.2f },
                Appearance = AppearanceDefinition.Default,
            });

            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(SkeletonInferrer.Infer(definition));
            float[] radii = MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(
                snapshot, ResolvedCreatureSnapshot.Resolve(definition));

            Assert.That(radii.Length, Is.EqualTo(1));
            Assert.That(radii[0], Is.EqualTo(ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius)
                .Within(Tolerance));
            AssertFinite(radii);
        }

        [Test]
        public void BuildRadii_NonPositiveBodyRadius_IsRejected()
        {
            CreatureDefinition definition = CreateDefinition(bodyRadius: 0f, limb: null);

            // Strict rejection: invalid DNA is reported by DefinitionValidator and
            // never resolved, so the resolver must not repair the radius.
            Assert.Throws<DomainException>(() => ResolvedCreatureSnapshot.Resolve(definition));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void BuildRadii_NonFiniteBodyRadius_IsRejected(float radius)
        {
            CreatureDefinition definition = CreateDefinition(bodyRadius: radius, limb: null);

            Assert.Throws<DomainException>(() => ResolvedCreatureSnapshot.Resolve(definition));
        }

        [Test]
        public void BuildRadii_DegenerateFirstSegment_ClampsMidpointAtLowerBound()
        {
            CreatureDefinition definition = CreateDefinition(
                bodyRadius: 0.7f,
                limb: CreateLimb(new[] { 0f, 0f, 1f }, new[] { 0.2f, 0.4f, 0.8f }));
            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(SkeletonInferrer.Infer(definition));
            float[] radii = MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(
                snapshot, ResolvedCreatureSnapshot.Resolve(definition));

            Assert.That(radii[snapshot.GetIndex("limb_j0")], Is.EqualTo(0.2f).Within(Tolerance));
            Assert.That(radii[snapshot.GetIndex("limb_j1")], Is.EqualTo(0.6f).Within(Tolerance));
            AssertFinite(radii);
        }

        [Test]
        public void BuildRadii_DefinitionOverloadMatchesSnapshotOverload()
        {
            CreatureDefinition definition = CreateDefinition(
                bodyRadius: 0.75f,
                limb: CreateLimb(new[] { 0f, 0.4f, 1f }, new[] { 0.16f, 0.12f, 0.09f }));
            ResolvedCreatureSnapshot resolved = ResolvedCreatureSnapshot.Resolve(definition);
            SkeletonSnapshot skeleton = SkeletonSnapshot.Capture(SkeletonInferrer.Infer(resolved));

            float[] fromDefinition = MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(definition);
            float[] fromSnapshot = MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(skeleton, resolved);

            AssertFiniteAndDeterministic(fromDefinition, fromSnapshot);
        }

        private static CreatureDefinition CreateDefinition(float? bodyRadius, LimbChain limb)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Clear();
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = new Vector3(0f, 0f, -1f),
                Radius = bodyRadius ?? 0.8f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 2,
                Position = Vector3.zero,
                Radius = bodyRadius ?? 0.8f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 3,
                Position = new Vector3(0f, 0f, 1f),
                Radius = bodyRadius ?? 0.8f,
            });

            if (limb != null)
            {
                definition.AddPart(new CreaturePart
                {
                    Id = "limb",
                    ParentId = CreatureDefinition.BodyId,
                    PartType = PartType.Limb,
                    Limb = limb,
                    Shape = new ShapeDefinition
                    {
                        Type = ShapeType.Capsule,
                        PrimarySize = 0.1f,
                        Radius = 0.1f,
                    },
                    Appearance = AppearanceDefinition.Default,
                });
            }

            return definition;
        }

        private static LimbChain CreateLimb(float[] normalizedPositions, float[] thicknessValues)
        {
            var limb = new LimbChain { Thickness = new ThicknessProfile() };
            for (int i = 0; i < normalizedPositions.Length; i++)
            {
                limb.Joints.Add(new LimbJoint
                {
                    Id = (uint)(i + 1),
                    Position = new Vector3(normalizedPositions[i], 0f, 2f),
                });
            }
            for (int i = 0; i < thicknessValues.Length; i++)
            {
                limb.Thickness.Keys.Add(new ThicknessKey
                {
                    T = (float)i / (thicknessValues.Length - 1),
                    Value = thicknessValues[i],
                });
            }
            return limb;
        }

        private static void AssertFiniteAndDeterministic(float[] first, float[] second)
        {
            Assert.AreEqual(first.Length, second.Length);
            AssertFinite(first);
            AssertFinite(second);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(first[i], Is.EqualTo(second[i]).Within(Tolerance));
            }
        }

        private static void AssertFinite(float[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                Assert.IsTrue(NumericValidity.IsFinite(values[i]), $"radius[{i}] must be finite");
            }
        }
    }
}