using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ProceduralCreature.Animation;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Animation.Skinned;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Serialization;
using ProceduralCreature.Skeleton;
using SkeletonModel = ProceduralCreature.Skeleton.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// PLAY MODE acceptance for TSK-0132 — the runtime SkinnedMeshRenderer adapter
    /// on a REAL generated creature. These tests exercise Unity's actual skinning
    /// (Mesh.boneWeights / bindposes / bones / rootBone via SkinnedMeshRenderer), so
    /// they can only run in a running instance (PlayMode). The oracle is
    /// <see cref="LinearBlendSkinning.Deform"/> on the SAME authored weights and the
    /// SAME bone frames Unity saw, which proves the Unity wiring rather than just the
    /// math.
    ///
    /// HARNESS: a real GeneratedCreature is produced synchronously
    /// (<see cref="CreatureMeshGenerator.Generate"/>); the implicit welded-surface item
    /// is bound to a <see cref="CreatureRig"/> built from
    /// <see cref="SkeletonInferrer.Infer"/>. Deformed vertices are read back with
    /// <see cref="SkinnedMeshRenderer.BakeMesh"/>. All generated GameObjects/meshes are
    /// destroyed in teardown.
    /// </summary>
    [TestFixture]
    public sealed class CreatureSkinnedMeshRendererTests
    {
        // Loose read-back tolerance: Unity skinning and BakeMesh go through the real
        // engine pipeline, so we allow a small epsilon over exact LBS math.
        private const float SkinningTolerance = 2e-2f;

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<Mesh> _meshes = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
            for (int i = _meshes.Count - 1; i >= 0; i--)
            {
                if (_meshes[i] != null) Object.DestroyImmediate(_meshes[i]);
            }
            _meshes.Clear();
        }

        // ---- fixture -----------------------------------------------------------

        private static CreatureDefinition BodyOnlyDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 8f };
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.8f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 0.9f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 0.8f });
            return definition;
        }

        /// <summary>Host + rig + adapter for a real generated creature's implicit surface.</summary>
        private sealed class BoundRig
        {
            public GameObject Host;
            public CreatureRig Rig;
            public CreatureSkinnedMeshRenderer Adapter;
            public SkeletonSnapshot Snapshot;
            public SkeletonModel SkeletonInput;
            public Mesh SourceMesh;
            public Vector3[] RestVertices;
            public VertexInfluence[][] Weights;
            public Mesh Baked;
        }

        /// <summary>
        /// Generates a creature, infers its skeleton, builds a rig and binds the implicit
        /// welded-surface mesh. Weights are re-authored identically (Author is
        /// deterministic) so the test can feed the exact same weights/bones to the LBS
        /// oracle the adapter bound into Unity.
        /// </summary>
        private BoundRig BuildBoundRig(CreatureDefinition definition)
        {
            var bound = new BoundRig();
            GeneratedCreature generated = CreatureMeshGenerator.Generate(definition, out _);
            if (!generated.TryGetImplicitSurface(out GeometryItem implicitItem))
            {
                Assert.Fail("Expected an implicit welded-surface item.");
            }

            SkeletonModel skeleton = SkeletonInferrer.Infer(definition);
            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(skeleton);
            ResolvedCreatureSnapshot resolved = ResolvedCreatureSnapshot.Resolve(definition);
            Assert.Greater(snapshot.Count, 0, "a generated creature must have an inferred skeleton");

            Mesh source = implicitItem.Mesh;
            Assert.Greater(source.vertexCount, 0, "implicit welded surface must have vertices");

            bound.SourceMesh = source;
            bound.RestVertices = source.vertices;
            bound.Snapshot = snapshot;
            bound.SkeletonInput = skeleton;

            // Re-author the identical welded-surface weights the adapter will build,
            // using the morphology-derived radius bridge rather than the 0.5 default.
            float[] radiiByBoneIndex = MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(snapshot, resolved);
            List<BoneSegmentInfluence> segments =
                ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(snapshot, radiiByBoneIndex);
            bound.Weights = ImplicitSurfaceWeightAuthoring.Author(segments, bound.RestVertices);

            var host = new GameObject("SkinnedHost");
            host.transform.position = Vector3.zero;
            host.transform.rotation = Quaternion.identity;
            host.transform.localScale = Vector3.one;
            CreatureRig rig = host.AddComponent<CreatureRig>();
            rig.Build(skeleton);
            CreatureSkinnedMeshRenderer adapter = host.AddComponent<CreatureSkinnedMeshRenderer>();
            adapter.Bind(rig, skeleton, source, null);

            bound.Host = host;
            bound.Rig = rig;
            bound.Adapter = adapter;
            bound.Baked = new Mesh();
            _objects.Add(host);
            _meshes.Add(bound.Baked);
            return bound;
        }

        private static BonePose[] RestFrames(SkeletonSnapshot snapshot)
        {
            var frames = new BonePose[snapshot.Count];
            for (int i = 0; i < snapshot.Count; i++)
            {
                frames[i] = new BonePose(snapshot[i].Position, snapshot[i].Rotation);
            }
            return frames;
        }

        private static CreatureDefinition BodyAndThinLimbDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 10f };
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.9f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 0.85f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 0.8f });

            var limb = new CreaturePart
            {
                Id = "thin_leg",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = new TransformData
                {
                    Position = new Vector3(0.65f, 0f, 0f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = new ShapeDefinition { Type = ShapeType.Capsule, PrimarySize = 0.12f, Radius = 0.12f },
                Appearance = AppearanceDefinition.Default,
                Limb = new LimbChain
                {
                    Joints =
                    {
                        new LimbJoint { Id = 1, Position = Vector3.zero },
                        new LimbJoint { Id = 2, Position = new Vector3(0.55f, 0f, 0f) },
                    },
                    Thickness = new ThicknessProfile
                    {
                        Keys =
                        {
                            new ThicknessKey { T = 0f, Value = 0.12f },
                            new ThicknessKey { T = 1f, Value = 0.08f },
                        }
                    },
                    BlendRadius = 0.08f,
                },
            };
            definition.AddPart(limb);
            return definition;
        }

        [Test]
        public void MorphologyInfluenceRadii_BodyAndThinLimb_UseResolvedMorphology_NotDefaultHalf()
        {
            CreatureDefinition definition = BodyAndThinLimbDefinition();
            GeneratedCreature generated = CreatureMeshGenerator.Generate(definition, out _);
            Assert.IsTrue(generated.TryGetImplicitSurface(out GeometryItem _), "body + limb should generate an implicit welded surface");

            SkeletonModel skeleton = SkeletonInferrer.Infer(definition);
            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(skeleton);
            float[] radii = MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(snapshot, ResolvedCreatureSnapshot.Resolve(definition));

            int bodyBoneIndex = snapshot.GetIndex(SemanticBoneResolver.ResolveBodySocketBoneId(2u));
            int limbBoneIndex = snapshot.GetIndex(SemanticBoneResolver.ResolveLimbSegmentBoneId(definition.FindPart("thin_leg"), 0, false));

            Assert.That(radii[bodyBoneIndex], Is.EqualTo(0.85f).Within(1e-4f),
                "body sample radius must drive the body bone influence radius");
            Assert.That(radii[limbBoneIndex], Is.EqualTo(0.10f).Within(1e-4f),
                "limb thickness at segment midpoint must drive the bone influence radius");
            Assert.That(radii[bodyBoneIndex], Is.Not.EqualTo(ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius).Within(1e-4f));
            Assert.That(radii[limbBoneIndex], Is.Not.EqualTo(ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius).Within(1e-4f));

            List<BoneSegmentInfluence> segments = ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences(snapshot, radii);
            Assert.IsTrue(generated.TryGetImplicitSurface(out GeometryItem implicitItem),
                "expected an implicit welded-surface item");
            VertexInfluence[][] weights = ImplicitSurfaceWeightAuthoring.Author(segments, implicitItem.Mesh.vertices);
            Vector3[] rest = LinearBlendSkinning.Deform(RestFrames(snapshot), RestFrames(snapshot), implicitItem.Mesh.vertices, weights);
            for (int i = 0; i < implicitItem.Mesh.vertexCount; i++)
            {
                Assert.That(Vector3.Distance(rest[i], implicitItem.Mesh.vertices[i]), Is.LessThan(1e-4f),
                    "weighting with derived morphology must reproduce the rest mesh");
            }
        }

        /// <summary>Reads the rig's actual bone frames (what Unity saw) as LBS frames.</summary>
        private static BonePose[] ReadRigFrames(BoundRig bound)
        {
            var frames = new BonePose[bound.Rig.IndexedBones.Count];
            for (int i = 0; i < frames.Length; i++)
            {
                Transform t = bound.Rig.IndexedBones[i];
                frames[i] = new BonePose(t.position, t.rotation);
            }
            return frames;
        }

        private Vector3[] BakeVertices(BoundRig bound)
        {
            SkinnedMeshRenderer renderer = bound.Adapter.Renderer;
            Assert.IsNotNull(renderer, "adapter must own a renderer after bind");
            renderer.BakeMesh(bound.Baked);
            return bound.Baked.vertices;
        }

        private static void AssertVerticesClose(Vector3[] actual, Vector3[] expected, string message)
        {
            Assert.AreEqual(expected.Length, actual.Length, message + ": vertex count");
            for (int v = 0; v < actual.Length; v++)
            {
                Assert.That(Vector3.Distance(actual[v], expected[v]), Is.LessThan(SkinningTolerance),
                    message + $" vertex {v}: got {actual[v]} expected {expected[v]}");
            }
        }

        private static void AssertMirroredVerticesClose(
            Vector3[] mirrored,
            Vector3[] unmirrored,
            Vector3[] unmirroredRest,
            Vector3[] mirroredRest,
            string message)
        {
            Assert.AreEqual(unmirrored.Length, mirrored.Length, message + ": vertex count");
            var usedMirroredIndices = new HashSet<int>();

            for (int v = 0; v < unmirrored.Length; v++)
            {
                Vector3 reflectedRest = MirrorUtility.ReflectPointAcrossX(unmirroredRest[v]);
                int mirroredIndex = -1;
                float closestSqrDistance = float.PositiveInfinity;
                for (int candidate = 0; candidate < mirroredRest.Length; candidate++)
                {
                    if (usedMirroredIndices.Contains(candidate)) continue;
                    float sqrDistance = (mirroredRest[candidate] - reflectedRest).sqrMagnitude;
                    if (sqrDistance < closestSqrDistance)
                    {
                        closestSqrDistance = sqrDistance;
                        mirroredIndex = candidate;
                    }
                }

                Assert.That(mirroredIndex, Is.GreaterThanOrEqualTo(0),
                    message + $" missing reflected rest vertex {v}: {reflectedRest}");
                Assert.That(closestSqrDistance, Is.LessThan(2.5e-3f),
                    message + $" reflected rest vertex {v} matched too far: " +
                    $"got {mirroredRest[mirroredIndex]} expected {reflectedRest}");
                usedMirroredIndices.Add(mirroredIndex);
                Vector3 expected = MirrorUtility.ReflectPointAcrossX(unmirrored[v]);
                Assert.That(Vector3.Distance(mirrored[mirroredIndex], expected), Is.LessThan(SkinningTolerance),
                    message + $" vertex {v} -> {mirroredIndex}: rest {unmirroredRest[v]} -> {mirroredRest[mirroredIndex]} " +
                    $"got {mirrored[mirroredIndex]} expected {expected}");
            }
        }

        private static Vector3[] DeformOracle(BoundRig bound, BonePose[] posed)
        {
            return LinearBlendSkinning.Deform(
                RestFrames(bound.Snapshot), posed, bound.RestVertices, bound.Weights);
        }

        private static PosedSkeleton PoseOf(BoundRig bound, params (string boneId, Vector3 position)[] moves)
        {
            var updates = new Dictionary<string, Vector3>();
            foreach ((string boneId, Vector3 position) in moves)
            {
                updates[boneId] = position;
            }
            return PosedSkeleton.FromRestPose(bound.SkeletonInput).WithUpdatedPositions(updates);
        }

        // ---- acceptance --------------------------------------------------------

        [Test]
        public void Rest_SmrEqualsGeneratedRestGeometry()
        {
            BoundRig bound = BuildBoundRig(BodyOnlyDefinition());
            Vector3[] baked = BakeVertices(bound);
            AssertVerticesClose(baked, bound.RestVertices, "rest SMR must equal generated rest geometry");
        }

        [Test]
        public void Posed_SmrEqualsLbsOracle_AndRestoreReturnsToRest()
        {
            BoundRig bound = BuildBoundRig(BodyOnlyDefinition());

            // Pose the distal spine bone forward by a clear amount; several vertices move.
            int tip = bound.Snapshot.Count - 1;
            bound.Rig.ApplyPose(PoseOf(bound,
                (bound.Snapshot[tip].Id, bound.Snapshot[tip].Position + Vector3.forward * 0.4f)));

            BonePose[] posedFrames = ReadRigFrames(bound);
            Vector3[] oracle = DeformOracle(bound, posedFrames);
            Vector3[] baked = BakeVertices(bound);
            AssertVerticesClose(baked, oracle, "posed SMR must equal the LBS oracle on real geometry");

            // Posed -> rest restores within tolerance.
            bound.Rig.ApplyPose(PoseOf(bound));
            Vector3[] restored = BakeVertices(bound);
            AssertVerticesClose(restored, bound.RestVertices, "posed->rest must restore the generated rest");
        }

        [Test]
        public void TwoBonePose_SmrEqualsLbsOracle_OnRealGeometry()
        {
            BoundRig bound = BuildBoundRig(BodyOnlyDefinition());

            // A relative two-bone pose so the whole chain blends across the joints.
            int mid = bound.Snapshot.Count > 2 ? bound.Snapshot.Count / 2 : 0;
            int tip = bound.Snapshot.Count - 1;
            bound.Rig.ApplyPose(PoseOf(bound,
                (bound.Snapshot[mid].Id, bound.Snapshot[mid].Position + Vector3.up * 0.3f),
                (bound.Snapshot[tip].Id, bound.Snapshot[tip].Position + Vector3.forward * 0.5f)));

            BonePose[] posedFrames = ReadRigFrames(bound);
            Vector3[] oracle = DeformOracle(bound, posedFrames);
            Vector3[] baked = BakeVertices(bound);
            AssertVerticesClose(baked, oracle, "posed SMR must match the LinearBlendSkinning oracle");
        }

        [Test]
        public void PerFrame_ApplyPose_AllocatesNoManagedMemory_AndDoesNotRebuildMesh()
        {
            BoundRig bound = BuildBoundRig(BodyOnlyDefinition());
            PosedSkeleton pose = PoseOf(bound);

            SkinnedMeshRenderer renderer = bound.Adapter.Renderer;
            Mesh sharedBefore = renderer.sharedMesh;
            BoneWeight[] boneWeightsBefore = sharedBefore.boneWeights;
            Matrix4x4[] bindposesBefore = sharedBefore.bindposes;

            for (int i = 0; i < 8; i++) bound.Rig.ApplyPose(pose);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) bound.Rig.ApplyPose(pose);
            stopwatch.Stop();
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log($"Skinned per-frame ApplyPose repeated=1000 allocatedBytes={allocated} " +
                      $"elapsedMilliseconds={stopwatch.Elapsed.TotalMilliseconds:F3}");

            Assert.AreEqual(0L, allocated, "per-frame pose application must not allocate managed memory");
            Assert.AreSame(sharedBefore, renderer.sharedMesh, "mesh must not be rebuilt per frame");
            Assert.AreEqual(boneWeightsBefore.Length, sharedBefore.boneWeights.Length,
                "bone weights must not be rebuilt per frame");
            Assert.AreEqual(bindposesBefore.Length, sharedBefore.bindposes.Length,
                "bindposes must not be rebuilt per frame");
        }

        [UnityTest]
        public System.Collections.IEnumerator RuntimePreview_ImplicitSurface_IsBoundToSkinnedRenderer_AndAppliedIdlePose()
        {
            var host = new GameObject("RuntimePreviewHost");
            _objects.Add(host);
            var preview = host.AddComponent<CreatureRuntimePreview>();
            var definitionJson = new TextAsset(new JsonDnaSerializer().Serialize(BodyOnlyDefinition()));
            FieldInfo field = typeof(CreatureRuntimePreview).GetField("definitionJson",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "preview should load a definition asset");
            field.SetValue(preview, definitionJson);

            preview.Generate();
            CreatureRig rig = null;
            for (int i = 0; i < 120 && rig == null; i++)
            {
                yield return null;
                rig = host.GetComponentInChildren<CreatureRig>();
            }

            SkinnedMeshRenderer renderer = host.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.NotNull(rig, "runtime preview should install a rig on the generated implicit surface");
            Assert.NotNull(renderer, "runtime preview should install a SkinnedMeshRenderer for the implicit surface");
            Assert.IsTrue(renderer.sharedMesh != null, "preview skinning mesh must be created");
            Assert.IsTrue(renderer.rootBone != null, "preview skinned mesh must have a root bone");
            Assert.Greater(renderer.bones.Length, 0, "preview skinned mesh must bind the rig's bone array");
            Assert.IsTrue(renderer.enabled, "idle pose should be applied and the skin render enabled");

            preview.enabled = false;
        }

        [UnityTest]
        public System.Collections.IEnumerator Lifecycle_ValidRebind_ReplacesRendererWithoutLeaking()
        {
            BoundRig bound = BuildBoundRig(BodyOnlyDefinition());
            int childCount = bound.Host.transform.childCount;
            SkinnedMeshRenderer first = bound.Adapter.Renderer;
            Mesh firstMesh = first.sharedMesh;

            bound.Adapter.Bind(bound.Rig, bound.SkeletonInput, bound.SourceMesh, null);

            Assert.IsNotNull(bound.Adapter.Renderer, "rebind must produce a renderer");
            Assert.AreNotSame(first, bound.Adapter.Renderer, "rebind must install a fresh renderer");

            // Object.Destroy is deferred to the end of the current frame in PlayMode, so
            // yield a few frames for the old bind's GameObject/mesh to be reclaimed.
            for (int i = 0; i < 3 && bound.Host.transform.childCount != childCount; i++)
            {
                yield return null;
            }

            Assert.AreEqual(childCount, bound.Host.transform.childCount,
                "rebind must replace (not stack) the generated renderer object");
            Assert.IsTrue(firstMesh == null, "old bind mesh must be destroyed");
            Assert.IsTrue(first == null, "old renderer must be destroyed");
        }

        [Test]
        public void FailedBind_LeavesNoRenderer_AndSourceMeshNotModified()
        {
            BoundRig bound = BuildBoundRig(BodyOnlyDefinition());
            Mesh source = bound.SourceMesh;
            Vector3[] sourceVertices = (Vector3[])source.vertices.Clone();
            int sourceSubMeshes = source.subMeshCount;

            // A rig that was never built -> bind must throw and leave no renderer.
            var emptyHost = new GameObject("EmptyHost");
            _objects.Add(emptyHost);
            CreatureSkinnedMeshRenderer emptyAdapter = emptyHost.AddComponent<CreatureSkinnedMeshRenderer>();
            var emptyRig = emptyHost.AddComponent<CreatureRig>();

            Assert.Throws<DomainException>(() =>
                emptyAdapter.Bind(emptyRig, bound.SkeletonInput, source, null));

            Assert.IsNull(emptyAdapter.Renderer, "failed bind must leave no renderer");

            // Source mesh must be untouched.
            Assert.AreEqual(sourceVertices.Length, source.vertices.Length, "source vertex count unchanged");
            Assert.AreEqual(sourceSubMeshes, source.subMeshCount, "source submesh count unchanged");
            for (int v = 0; v < sourceVertices.Length; v++)
            {
                Assert.That(Vector3.Distance(source.vertices[v], sourceVertices[v]), Is.LessThan(1e-6f),
                    $"source vertex {v} must not be modified");
            }
        }

        // A Body + a real mirrored Limb so the inferred skeleton contains BOTH the authored
        // limb chain and its reflected copy (real mirror bones with segment geometry). The
        // welded implicit surface covers both sides, so binding exercises mirrored bones
        // through the real Unity skinning path.
        private static CreatureDefinition MirroredLimbDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 10f };
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.9f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = Vector3.zero, Radius = 0.85f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 0.8f });

            definition.AddPart(new CreaturePart
            {
                Id = "part_arm",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = new TransformData
                {
                    Position = new Vector3(0.4f, 0.0f, 0.0f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = new ShapeDefinition { Type = ShapeType.Capsule, PrimarySize = 0.14f, Radius = 0.14f },
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
                Limb = new LimbChain
                {
                    Joints =
                    {
                        new LimbJoint { Id = 1, Position = Vector3.zero },
                        new LimbJoint { Id = 2, Position = new Vector3(0.35f, 0f, 0f) },
                        new LimbJoint { Id = 3, Position = new Vector3(0.7f, 0f, 0f) },
                    },
                    Thickness = new ThicknessProfile
                    {
                        Keys =
                        {
                            new ThicknessKey { T = 0f, Value = 0.14f },
                            new ThicknessKey { T = 1f, Value = 0.10f },
                        }
                    },
                    BlendRadius = 0.08f,
                },
            });
            return definition;
        }

        [Test]
        public void MirroredLimb_PosedSmrEqualsLbsOracle_OnRealGeometry()
        {
            // Bind the welded surface of a real creature whose skeleton carries mirrored
            // segment bones. Posing the mirrored limb is reflected (via MirrorUtility) from
            // the authored limb, so deformation of the mirrored side flows through the same
            // SkeletonSnapshot index-parallel bind. This exercises mirror bones through the
            // real SkinnedMeshRenderer wiring (the adapter has no mirror logic of its own).
            CreatureDefinition definition = MirroredLimbDefinition();
            BoundRig bound = BuildBoundRig(definition);
            CreaturePart arm = definition.FindPart("part_arm");

            string authoredSeg = SemanticBoneResolver.ResolveLimbSegmentBoneId(arm, 1, false);
            string mirroredSeg = SemanticBoneResolver.ResolveLimbSegmentBoneId(arm, 1, true);
            int authoredIndex = bound.Snapshot.GetIndex(authoredSeg);
            int mirroredIndex = bound.Snapshot.GetIndex(mirroredSeg);

            Vector3 authoredDelta = new Vector3(0.2f, 0.25f, 0f);
            PosedSkeleton pose = PosedSkeleton.FromRestPose(bound.SkeletonInput).WithUpdatedPositions(
                new Dictionary<string, Vector3>
                {
                    [authoredSeg] = bound.Snapshot[authoredIndex].Position + authoredDelta,
                    [mirroredSeg] = bound.Snapshot[mirroredIndex].Position
                                    + MirrorUtility.ReflectPointAcrossX(authoredDelta),
                });
            bound.Rig.ApplyPose(pose);

            BonePose[] posedFrames = ReadRigFrames(bound);
            Vector3[] oracle = DeformOracle(bound, posedFrames);
            Vector3[] baked = BakeVertices(bound);
            AssertVerticesClose(baked, oracle, "mirrored-limb posed SMR must equal the LBS oracle");
        }

        [Test]
        public void MirroredLimb_PosedSmrEqualsReflectedUnmirroredSmr_OnRealGeometry()
        {
            CreatureDefinition definition = MirroredLimbDefinition();
            BoundRig unmirrored = BuildBoundRig(definition);
            BoundRig mirrored = BuildBoundRig(definition);
            CreaturePart arm = definition.FindPart("part_arm");

            string authoredSeg = SemanticBoneResolver.ResolveLimbSegmentBoneId(arm, 1, false);
            string mirroredSeg = SemanticBoneResolver.ResolveLimbSegmentBoneId(arm, 1, true);
            int authoredIndex = unmirrored.Snapshot.GetIndex(authoredSeg);
            int mirroredIndex = mirrored.Snapshot.GetIndex(mirroredSeg);
            Vector3 authoredDelta = new Vector3(0.2f, 0.25f, 0f);

            unmirrored.Rig.ApplyPose(PoseOf(unmirrored,
                (authoredSeg, unmirrored.Snapshot[authoredIndex].Position + authoredDelta)));
            mirrored.Rig.ApplyPose(PoseOf(mirrored,
                (mirroredSeg, mirrored.Snapshot[mirroredIndex].Position
                    + MirrorUtility.ReflectPointAcrossX(authoredDelta))));

            Vector3[] unmirroredBaked = BakeVertices(unmirrored);
            Vector3[] mirroredBaked = BakeVertices(mirrored);
            AssertMirroredVerticesClose(
                mirroredBaked,
                unmirroredBaked,
                unmirrored.RestVertices,
                mirrored.RestVertices,
                "mirrored SMR must equal reflection of the unmirrored SMR");
        }
    }
}
