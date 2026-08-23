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
            Vector3 worldPosition = world.GetColumn(3);

            Assert.AreEqual(new Vector3(1f, 2f, 3f), worldPosition, "Position should be preserved unchanged for a root part.");
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
        public void BodyChild_ResolvesAgainstTheBodyRootFrame()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var child = new CreaturePart
            {
                Id = "part_leg",
                ParentId = CreatureDefinition.BodyId,
                Transform = new TransformData { Position = new Vector3(1f, -1f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            };
            definition.AddPart(child);

            Matrix4x4 world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, child);
            Vector3 worldPosition = world.GetColumn(3);

            Assert.AreEqual(new Vector3(1f, -1f, 0f), worldPosition,
                "A Body child's local transform IS creature-space: the Body spline owns the creature frame.");
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

        // ---- CC-018 child-at-tip frame ----------------------------------------------
        // A limb's TERMINAL joint is the origin of any child's local space, so a
        // child authored at local (0,0,0) sits at the limb's tip, not its root.

        private static LimbChain LimbChainWith(params Vector3[] positions)
        {
            var chain = new LimbChain();
            for (int i = 0; i < positions.Length; i++)
            {
                chain.Joints.Add(new LimbJoint { Id = (uint)(i + 1), Position = positions[i] });
            }
            return chain;
        }

        [Test]
        public void ChildOfLimb_IdentityLocalResolvesToTerminalJoint()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart
            {
                Id = "part_arm",
                Transform = new TransformData { Position = new Vector3(1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
                Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f)),
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_hand",
                ParentId = "part_arm",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            });

            Matrix4x4 world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, definition.FindPart("part_hand"));
            Vector3 worldPosition = world.GetColumn(3);

            Assert.AreEqual(new Vector3(1f, -2f, 0f), worldPosition,
                "A child at identity under a limb sits at the limb's terminal joint, not the root.");
        }

        [Test]
        public void LimbItself_ResolvesWithoutTerminalJointOffset()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var arm = new CreaturePart
            {
                Id = "part_arm",
                Transform = new TransformData { Position = new Vector3(1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
                Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f)),
            };
            definition.AddPart(arm);

            Matrix4x4 world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, arm);
            Vector3 worldPosition = world.GetColumn(3);

            Assert.AreEqual(new Vector3(1f, 0f, 0f), worldPosition,
                "The limb's own frame is its placement frame; the terminal joint offset applies only to children.");
        }

        [Test]
        public void ResolveChildFrameToCreatureSpace_LimbParent_IncludesTerminalJoint()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var arm = new CreaturePart
            {
                Id = "part_arm",
                Transform = new TransformData { Position = new Vector3(1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
                Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f)),
            };
            definition.AddPart(arm);

            Matrix4x4 childFrame = CreaturePartWorldTransformResolver.ResolveChildFrameToCreatureSpace(definition, arm);
            Vector3 childFrameOrigin = childFrame.GetColumn(3);

            Assert.AreEqual(new Vector3(1f, -2f, 0f), childFrameOrigin,
                "A limb's child frame origin is its terminal joint.");
        }

        [Test]
        public void ResolveChildFrameToCreatureSpace_NonLimbParent_EqualsPartFrame()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var parent = new CreaturePart
            {
                Id = "part_p",
                Transform = new TransformData { Position = new Vector3(2f, 3f, 4f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            };
            definition.AddPart(parent);

            Matrix4x4 childFrame = CreaturePartWorldTransformResolver.ResolveChildFrameToCreatureSpace(definition, parent);
            Matrix4x4 partFrame = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, parent);

            Assert.AreEqual(partFrame.GetColumn(3), childFrame.GetColumn(3),
                "Non-limb parents have no child-frame offset.");
        }

        [Test]
        public void GrandchildOfLimb_ChainsThroughAncestorTips()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart
            {
                Id = "part_arm",
                Transform = new TransformData { Position = new Vector3(1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
                Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -1f, 0f)),
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_hand",
                ParentId = "part_arm",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
                Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -0.5f, 0f)),
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_claw",
                ParentId = "part_hand",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            });

            Matrix4x4 world = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, definition.FindPart("part_claw"));
            Vector3 worldPosition = world.GetColumn(3);

            // arm at (1,0,0) -> arm tip (0,-1,0) -> hand frame -> hand tip (0,-0.5,0) -> claw at identity.
            Assert.AreEqual(new Vector3(1f, -1.5f, 0f), worldPosition,
                "A grandchild of a limb chains through the arm tip and the hand tip.");
        }
    }
}
