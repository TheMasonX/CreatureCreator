using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class ImplicitSurfaceInfluenceDomainResolverTests
    {
        private static CreatureDefinition BuildHierarchy(bool mirrored = false)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 8f };
            definition.SymmetryMode = mirrored ? SymmetryMode.MirrorAcrossXAxis : SymmetryMode.None;
            definition.Body.Samples.Clear();
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = new Vector3(0f, 0f, -3f),
                Radius = 0.25f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 2,
                Position = new Vector3(0f, 0f, 3f),
                Radius = 0.25f,
            });

            definition.AddPart(new CreaturePart
            {
                Id = "leg_left",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Leg,
                Transform = new TransformData
                {
                    Position = new Vector3(2f, 1f, 0f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, Radius = 0.65f },
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = mirrored,
            });

            definition.AddPart(new CreaturePart
            {
                Id = "foot_left",
                ParentId = "leg_left",
                PartType = PartType.Foot,
                Transform = new TransformData
                {
                    Position = new Vector3(0f, -1f, 0f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, Radius = 0.55f },
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = mirrored,
            });

            definition.AddPart(new CreaturePart
            {
                Id = "neighbor_leg",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Leg,
                Transform = new TransformData
                {
                    Position = new Vector3(0f, -1f, 0f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, Radius = 0.35f },
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = false,
            });

            return definition;
        }

        [Test]
        public void Resolve_FootDomain_IncludesOwnedParentButNotSibling()
        {
            CreatureDefinition definition = BuildHierarchy();
            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);

            InfluenceDomain[] domains = ImplicitSurfaceInfluenceDomainResolver.Resolve(
                definition,
                snapshot,
                new[] { new Vector3(2f, 0f, 0f) });

            Assert.AreEqual(1, domains.Length);
            Assert.AreEqual("foot_left", domains[0].DomainId);
            Assert.IsTrue(domains[0].Allows("foot_left"), "geometry owns its own domain");
            Assert.IsTrue(domains[0].Allows("leg_left"), "geometry may blend with its intended parent chain");
            Assert.IsFalse(domains[0].Allows("neighbor_leg"), "geometry must not inherit a sibling limb domain");
        }

        [Test]
        public void Resolve_MirroredFootDomain_IncludesMirroredParentButNotOriginalSide()
        {
            CreatureDefinition definition = BuildHierarchy(mirrored: true);
            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);

            InfluenceDomain[] domains = ImplicitSurfaceInfluenceDomainResolver.Resolve(
                definition,
                snapshot,
                new[] { new Vector3(-2f, 0f, 0f) });

            Assert.AreEqual(1, domains.Length);
            Assert.AreEqual("foot_left_mirror", domains[0].DomainId);
            Assert.IsTrue(domains[0].Allows("foot_left_mirror"));
            Assert.IsTrue(domains[0].Allows("leg_left_mirror"), "mirrored geometry may blend with its mirrored parent chain");
            Assert.IsFalse(domains[0].Allows("leg_left"), "mirrored geometry must not inherit the original-side limb domain");
        }

        [Test]
        public void Resolve_DeepOwnedHierarchy_IncludesEachNonBodyAncestor()
        {
            CreatureDefinition definition = BuildHierarchy();
            definition.AddPart(new CreaturePart
            {
                Id = "toe_left",
                ParentId = "foot_left",
                PartType = PartType.Part,
                Transform = new TransformData
                {
                    Position = new Vector3(0f, -0.55f, 0f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, Radius = 0.35f },
                Appearance = AppearanceDefinition.Default,
            });

            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);
            InfluenceDomain[] domains = ImplicitSurfaceInfluenceDomainResolver.Resolve(
                definition,
                snapshot,
                new[] { new Vector3(2f, -0.95f, 0f) });

            Assert.AreEqual(1, domains.Length);
            Assert.AreEqual("toe_left", domains[0].DomainId);
            Assert.IsTrue(domains[0].Allows("toe_left"));
            Assert.IsTrue(domains[0].Allows("foot_left"));
            Assert.IsTrue(domains[0].Allows("leg_left"));
            Assert.IsFalse(domains[0].Allows("neighbor_leg"));
        }
    }
}