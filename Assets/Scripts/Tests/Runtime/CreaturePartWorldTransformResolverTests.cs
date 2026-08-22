using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class CreaturePartWorldTransformResolverTests
    {
        [Test]
        public void RootPart_ResolvesToItsOwnLocalTransform()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var root = new CreaturePart
            {
                Id = "part_root",
                Transform = new TransformData
                {
                    Position = new Vector3(1f, 2f, 3f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            };
            definition.AddPart(root);

            Matrix4x4 world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, root);

            Assert.AreEqual(new Vector3(1f, 2f, 3f), world.GetColumn(3), "Position should be preserved unchanged for a root part.");
        }

        [Test]
        public void ChildPart_ComposesWithParentTranslation()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var root = new CreaturePart
            {
                Id = "part_root",
                Transform = new TransformData { Position = new Vector3(10f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            };
            var child = new CreaturePart
            {
                Id = "part_child",
                ParentId = "part_root",
                Transform = new TransformData { Position = new Vector3(0f, 5f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            };
            definition.AddPart(root);
            definition.AddPart(child);

            Matrix4x4 world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, child);
            Vector3 worldPosition = world.GetColumn(3);

            Assert.AreEqual(new Vector3(10f, 5f, 0f), worldPosition,
                "Child position should be parent position + child's local offset.");
        }

        [Test]
        public void ThreeLevelChain_ComposesCorrectly()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart
            {
                Id = "part_a",
                Transform = new TransformData { Position = new Vector3(1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_b", ParentId = "part_a",
                Transform = new TransformData { Position = new Vector3(1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            });
            var grandchild = new CreaturePart
            {
                Id = "part_c", ParentId = "part_b",
                Transform = new TransformData { Position = new Vector3(1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            };
            definition.AddPart(grandchild);

            Matrix4x4 world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, grandchild);
            Vector3 worldPosition = world.GetColumn(3);

            Assert.AreEqual(new Vector3(3f, 0f, 0f), worldPosition, "Three unit offsets along X should sum to (3,0,0).");
        }

        [Test]
        public void MissingParent_ThrowsDomainException()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var orphan = new CreaturePart
            {
                Id = "part_a", ParentId = "part_missing",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            };
            definition.AddPart(orphan);

            Assert.Throws<DomainException>(() =>
                CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, orphan));
        }

        [Test]
        public void ParentCycle_ThrowsDomainExceptionInsteadOfLoopingForever()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var a = new CreaturePart
            {
                Id = "part_a", ParentId = "part_b",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            };
            var b = new CreaturePart
            {
                Id = "part_b", ParentId = "part_a",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            };
            definition.AddPart(a);
            definition.AddPart(b);

            Assert.Throws<DomainException>(() =>
                CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, a));
        }
    }
}
