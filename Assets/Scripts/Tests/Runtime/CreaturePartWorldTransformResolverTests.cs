using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

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
        public void ResolvedCreatureSnapshot_ProvidesDeterministicPartLookup()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart
            {
                Id = "part_b",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_a",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });

            ResolvedCreatureSnapshot first = ResolvedCreatureSnapshot.Resolve(definition);
            ResolvedCreatureSnapshot second = ResolvedCreatureSnapshot.Resolve(definition);

            Assert.AreEqual(2, first.PartsById.Count);
            Assert.IsTrue(first.TryGetPart("part_a", out ResolvedPartSnapshot firstPart));
            Assert.IsTrue(second.TryGetPart("part_a", out ResolvedPartSnapshot secondPart));
            Assert.AreEqual(firstPart.Id, secondPart.Id);
            Assert.AreEqual(firstPart.PartFrameToCreatureSpace, secondPart.PartFrameToCreatureSpace);
            Assert.IsFalse(first.TryGetPart("missing", out _));
        }

        [Test]
        public void ResolvedCreatureSnapshot_RevisionId_IsCanonicalAndStable()
        {
            var firstDefinition = CreatureDefinition.CreateEmpty();
            firstDefinition.AddPart(new CreaturePart
            {
                Id = "part_b",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });
            firstDefinition.AddPart(new CreaturePart
            {
                Id = "part_a",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });

            CreatureDefinition reorderedDefinition = firstDefinition.Clone();
            CreaturePart firstPart = reorderedDefinition.Parts[0];
            reorderedDefinition.Parts[0] = reorderedDefinition.Parts[1];
            reorderedDefinition.Parts[1] = firstPart;

            ResolvedCreatureSnapshot first = ResolvedCreatureSnapshot.Resolve(firstDefinition);
            ResolvedCreatureSnapshot reordered = ResolvedCreatureSnapshot.Resolve(reorderedDefinition);
            Assert.AreEqual(first.RevisionId, reordered.RevisionId,
                "canonical ordering must not change the snapshot revision");
            Assert.IsNotEmpty(first.RevisionId);

            reorderedDefinition.FindPart("part_a").Transform.Position = new Vector3(1f, 0f, 0f);
            ResolvedCreatureSnapshot changed = ResolvedCreatureSnapshot.Resolve(reorderedDefinition);
            Assert.AreNotEqual(first.RevisionId, changed.RevisionId,
                "a DNA change must create a different snapshot revision");
        }

        [Test]
        public void ResolvedCreatureSnapshot_CapturesMeshAttachmentCorrespondence()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var part = new CreaturePart
            {
                Id = "mesh_part",
                ParentId = CreatureDefinition.BodyId,
                Transform = new TransformData
                {
                    Position = new Vector3(1f, 2f, 3f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MeshGeometry = new MeshGeometry
                {
                    MeshAssetKey = "mesh_asset",
                    Attachment = new GeometryAttachment
                    {
                        Offset = new Vector3(0f, 0.25f, 0f),
                        Orientation = Quaternion.Euler(0f, 90f, 0f),
                        Scale = new Vector3(2f, 1f, 1f),
                    },
                },
            };
            definition.AddPart(part);

            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);
            Assert.IsTrue(snapshot.TryGetPart(part.Id, out ResolvedPartSnapshot resolved));
            Assert.IsTrue(resolved.HasMeshGeometry);
            Assert.AreEqual("mesh_asset", resolved.MeshAssetKey);
            Assert.AreEqual(part.MeshGeometry.Attachment.Offset, resolved.GeometryOffset);
            Assert.AreEqual(part.MeshGeometry.Attachment.Orientation.normalized, resolved.GeometryOrientation);
            Assert.AreEqual(part.MeshGeometry.Attachment.Scale, resolved.GeometryScale);
            Assert.AreEqual(new Vector3(1f, 2.25f, 3f),
                resolved.GeometryPlacementToCreatureSpace.MultiplyPoint3x4(Vector3.zero));
        }

        [Test]
        public void ResolvedCreatureSnapshot_UsesResolvedLimbTerminalForChildFrame()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var arm = new CreaturePart
            {
                Id = "part_arm",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -1f, 0f)),
            };
            definition.AddPart(arm);

            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);

            Assert.IsTrue(snapshot.TryGetPart("part_arm", out ResolvedPartSnapshot resolvedArm));
            Assert.IsTrue(resolvedArm.HasLimb);
            Assert.AreEqual(new Vector3(0f, -1f, 0f), resolvedArm.Limb.TerminalSocket);
            Assert.AreEqual(new Vector3(0f, -1f, 0f),
                (Vector3)resolvedArm.ChildFrameToCreatureSpace.GetColumn(3));
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

        // ---- CC-051 single canonical placement path ---------------------------------
        // ResolvePartFrameToCreatureSpace is THE part-frame resolver (ADR-002 §7);
        // ResolveLocalToCreatureSpace is an alias. No consumer may re-derive
        // placement, so the two must agree everywhere.

        [Test]
        public void ResolvePartFrame_IsTheSingleCanonicalPath_ForBodyChild()
        {
            var definition = CreatureDefinition.CreateEmpty();
            var child = new CreaturePart
            {
                Id = "part_head",
                ParentId = CreatureDefinition.BodyId,
                Transform = new TransformData
                {
                    Position = new Vector3(0f, 0.5f, 1.2f),
                    Rotation = Quaternion.Euler(0f, 30f, 0f),
                    Scale = new Vector3(1f, 1.2f, 1f),
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            };
            definition.AddPart(child);

            Matrix4x4 canonical = CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace(definition, child);
            Matrix4x4 alias = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, child);
            Vector3 canonicalPosition = canonical.GetColumn(3);

            Assert.AreEqual(alias, canonical,
                "ResolveLocalToCreatureSpace must be an exact alias of the canonical part-frame resolver.");
            Assert.AreEqual(new Vector3(0f, 0.5f, 1.2f), canonicalPosition,
                "A Body child's frame is its authored local transform (the Body owns the creature frame).");
        }

        [Test]
        public void ResolvePartFrame_IsTheSingleCanonicalPath_ForLimbTipChild()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart
            {
                Id = "part_arm",
                Transform = new TransformData { Position = new Vector3(1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f)),
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_hand",
                ParentId = "part_arm",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });

            Matrix4x4 canonical = CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_hand"));
            Matrix4x4 alias = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, definition.FindPart("part_hand"));
            Vector3 canonicalPosition = canonical.GetColumn(3);

            Assert.AreEqual(alias, canonical, "The canonical part-frame resolver and its alias must agree at a limb tip.");
            Assert.AreEqual(new Vector3(1f, -2f, 0f), canonicalPosition,
                "A child of a limb sits at the limb's terminal joint through the canonical path.");
        }

        // ---- CC-056B body-surface anchor projection --------------------------------
        // A direct Body child with a ParentAttachment is placed by projecting the
        // anchor onto the resolved Body surface (ADR-002 §7 precedence table:
        // "Body child | BodySurfaceAnchor"). The projected SurfaceFrame is the
        // placement root; the part's local transform is a fine adjustment in that
        // frame's local space.

        private static BodySpline ProjectionBody()
        {
            var body = new BodySpline();
            body.Samples.Add(new BodySample { Id = 10, Position = new Vector3(0f, 0f, 0f), Radius = 1f });
            body.Samples.Add(new BodySample { Id = 20, Position = new Vector3(0f, 0f, 2f), Radius = 2f });
            return body;
        }

        private static CreaturePart BodyChildWithAnchor(uint segmentId, float segmentT,
            float radialAngle = 0f, float surfaceOffset = 0f, float roll = 0f,
            Vector3? localPosition = null, Quaternion? localRotation = null)
        {
            return new CreaturePart
            {
                Id = "part_leg",
                ParentId = CreatureDefinition.BodyId,
                Transform = new TransformData
                {
                    Position = localPosition ?? Vector3.zero,
                    Rotation = localRotation ?? Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                ParentAttachment = new BodySurfaceAnchor
                {
                    SegmentStartSampleId = segmentId,
                    SegmentT = segmentT,
                    RadialAngle = radialAngle,
                    SurfaceOffset = surfaceOffset,
                    Roll = roll,
                },
            };
        }

        [Test]
        public void ResolvePartFrame_BodyChildWithParentAttachment_ProjectsPositionToSurface()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = ProjectionBody();
            definition.AddPart(BodyChildWithAnchor(10, 0.5f));

            Vector3 position = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_leg")).GetColumn(3);

            // Segment 10->20 at T=0.5: centerline (0,0,1), interpolated radius 1.5,
            // RadialAngle 0 -> Normal (up). Surface point = (0, 1.5, 1).
            Assert.That(position, Is.EqualTo(new Vector3(0f, 1.5f, 1f)).Within(1e-5f),
                "The anchor is the position authority for a Body child.");
        }

        [Test]
        public void ResolvePartFrame_BodyChildWithParentAttachment_AppliesLocalOffsetInSurfaceFrame()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = ProjectionBody();
            definition.AddPart(BodyChildWithAnchor(10, 0.5f, localPosition: new Vector3(0f, -0.3f, 0f)));

            Vector3 position = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_leg")).GetColumn(3);

            Assert.That(position, Is.EqualTo(new Vector3(0f, 1.2f, 1f)).Within(1e-5f),
                "The part's local position is a fine adjustment in the surface frame's local space.");
        }

        [Test]
        public void ResolvePartFrame_BodyChildWithParentAttachment_RadialAngleDrivesPosition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = ProjectionBody();
            // RadialAngle pi/2 points toward Binormal (left for a z-forward spline).
            definition.AddPart(BodyChildWithAnchor(10, 0f, radialAngle: Mathf.PI * 0.5f));

            Vector3 position = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_leg")).GetColumn(3);

            Assert.That(Vector3.Distance(position, new Vector3(-1f, 0f, 0f)), Is.LessThan(1e-5f),
                $"Positive RadialAngle turns the projected point from Normal toward Binormal (got {position.ToString("R")}).");
        }

        [Test]
        public void ResolvePartFrame_BodyChildWithParentAttachment_RollRotatesFrameAroundTangent()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = ProjectionBody();
            definition.AddPart(BodyChildWithAnchor(10, 0f, roll: Mathf.PI * 0.5f));

            Matrix4x4 frame = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_leg"));
            Quaternion rotation = frame.rotation;

            // Roll pi/2 around the tangent (z) maps the frame's up (Normal) -> left.
            Assert.That(Vector3.Distance(rotation * Vector3.up, Vector3.left), Is.LessThan(1e-5f),
                $"Roll rotates the surface frame around the body tangent (up -> {(rotation * Vector3.up).ToString("R")}).");
            Assert.That(Vector3.Distance(rotation * Vector3.forward, Vector3.forward), Is.LessThan(1e-5f),
                "The along-spline tangent is unchanged by roll.");
        }

        [Test]
        public void ResolvePartFrame_ChildOfAnchoredBodyChild_ChainsThroughProjectedFrame()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = ProjectionBody();
            definition.AddPart(BodyChildWithAnchor(10, 0.5f)); // anchored leg at (0,1.5,1)
            definition.FindPart("part_leg").Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -1f, 0f));
            definition.AddPart(new CreaturePart
            {
                Id = "part_foot",
                ParentId = "part_leg",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });

            Vector3 footPosition = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_foot")).GetColumn(3);

            // Leg surface frame (0,1.5,1) -> leg terminal joint (0,-1,0) -> foot identity.
            Assert.That(footPosition, Is.EqualTo(new Vector3(0f, 0.5f, 1f)).Within(1e-5f),
                "A child of an anchored Body child rides the projected frame and the limb tip.");
        }

        [Test]
        public void ResolvePartFrame_NonBodyChildWithParentAttachment_AnchorStaysInert()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = ProjectionBody();
            definition.AddPart(new CreaturePart
            {
                Id = "part_leg",
                ParentId = CreatureDefinition.BodyId,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_foot",
                ParentId = "part_leg",
                Transform = new TransformData { Position = new Vector3(0f, -0.5f, 0.4f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                ParentAttachment = new BodySurfaceAnchor
                {
                    SegmentStartSampleId = 10u,
                    SegmentT = 0.5f,
                    RadialAngle = 0f,
                    SurfaceOffset = 0f,
                    Roll = 0f,
                },
            });

            Vector3 footPosition = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_foot")).GetColumn(3);

            Assert.That(footPosition, Is.EqualTo(new Vector3(0f, -0.5f, 0.4f)).Within(1e-5f),
                "ParentAttachment only has placement authority for direct Body children.");
        }

        // ---- CC-056B parity: five attachment kinds, one resolver seam --------------
        // ADR-002 §7: every attachment kind resolves through
        // ResolvePartFrameToCreatureSpace with its documented authority. These
        // tests pin the precedence contract so no kind can drift onto a second
        // placement path.

        [Test]
        public void ResolvePartFrame_Parity_BodySurfaceAnchorIsPositionAuthority()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = ProjectionBody();
            definition.AddPart(BodyChildWithAnchor(10, 0.5f));

            Vector3 position = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_leg")).GetColumn(3);

            Assert.That(position, Is.EqualTo(new Vector3(0f, 1.5f, 1f)).Within(1e-5f),
                "BodySurface: the anchor is the position authority for a Body child.");
        }

        [Test]
        public void ResolvePartFrame_Parity_PartFrameIsAuthoredLocalTransform()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = ProjectionBody();
            definition.AddPart(new CreaturePart
            {
                Id = "part_head",
                ParentId = CreatureDefinition.BodyId,
                Transform = new TransformData { Position = new Vector3(0f, 0.5f, 1.2f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });

            Vector3 position = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_head")).GetColumn(3);

            Assert.That(position, Is.EqualTo(new Vector3(0f, 0.5f, 1.2f)).Within(1e-5f),
                "PartFrame: an unanchored Body child's local transform IS creature-space.");
        }

        [Test]
        public void ResolvePartFrame_Parity_LimbRootIsPlacementFrame()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = ProjectionBody();
            definition.AddPart(new CreaturePart
            {
                Id = "part_arm",
                ParentId = CreatureDefinition.BodyId,
                Transform = new TransformData { Position = new Vector3(1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f)),
            });

            Vector3 position = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_arm")).GetColumn(3);

            Assert.That(position, Is.EqualTo(new Vector3(1f, 0f, 0f)).Within(1e-5f),
                "LimbRoot: a limb's own frame is its placement frame, not its terminal joint.");
        }

        [Test]
        public void ResolvePartFrame_Parity_LimbTerminalIsChildSocket()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = ProjectionBody();
            definition.AddPart(new CreaturePart
            {
                Id = "part_arm",
                ParentId = CreatureDefinition.BodyId,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -1f, 0f)),
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_hand",
                ParentId = "part_arm",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });

            Vector3 position = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_hand")).GetColumn(3);

            Assert.That(position, Is.EqualTo(new Vector3(0f, -1f, 0f)).Within(1e-5f),
                "LimbTerminal: a limb child sits at the limb's terminal joint.");
        }

        [Test]
        public void ResolvePartFrame_Parity_GeometryAttachmentComposesOnPartFrame()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = ProjectionBody();
            definition.AddPart(new CreaturePart
            {
                Id = "part_head",
                ParentId = CreatureDefinition.BodyId,
                Transform = new TransformData { Position = new Vector3(0f, 0.5f, 1.2f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MeshGeometry = new MeshGeometry
                {
                    MeshAssetKey = "eye",
                    Attachment = new GeometryAttachment
                    {
                        Offset = new Vector3(0f, 0.1f, 0f),
                        Orientation = Quaternion.identity,
                        Scale = Vector3.one,
                    },
                },
            });

            // Mirrors CreatureMeshGenerator: placement = resolver part frame *
            // the local attachment (offset/orientation/scale in the part's frame).
            Matrix4x4 partFrame = CreaturePartWorldTransformResolver
                .ResolvePartFrameToCreatureSpace(definition, definition.FindPart("part_head"));
            GeometryAttachment attachment = definition.FindPart("part_head").MeshGeometry.Attachment;
            Vector3 position = (partFrame * Matrix4x4.TRS(
                attachment.Offset, attachment.Orientation.normalized, attachment.Scale)).GetColumn(3);

            Assert.That(position, Is.EqualTo(new Vector3(0f, 0.6f, 1.2f)).Within(1e-5f),
                "GeometryAttachment: mesh placement is the part frame plus the local offset.");
        }
    }
}
