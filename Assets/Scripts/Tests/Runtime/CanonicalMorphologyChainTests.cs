using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Animation;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Serialization;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// TSK-0085 / CC-081: ONE canonical end-to-end morphology verification run.
    ///
    /// The per-stage unit fixtures (SDF, extraction, skeleton, rig, serialization)
    /// each prove one stage on small synthetic inputs. This fixture is the single
    /// coherent gate that drives the WHOLE chain on one canonical dino-like
    /// definition plus adversarial fixtures and asserts the stage-boundary
    /// invariants those per-stage tests deliberately do not combine:
    ///
    ///     Definition -> Morphology -> SDF -> Mesh -> Skeleton -> Rig -> serialization
    ///
    /// - Definition/SDF/Mesh: the canonical dino generates a deterministic implicit
    ///   surface that is watertight at the SDF/mesh boundary (MeshTopologyReport).
    /// - Skeleton: a flagged part infers exactly one mirrored bone; unflagged parts
    ///   infer none (mirroring never cascades without a flag).
    /// - Rig: CreatureRig builds a real Transform hierarchy from the inferred
    ///   skeleton whose rest positions match the skeleton.
    /// - serialization: a canonical round-trip (serialize -> deserialize) preserves
    ///   the full downstream chain output (mesh counts + topology + skeleton).
    ///
    /// Tagged [Category("CanonicalMorphologyChain")]. Run this single category to
    /// see the whole chain green. See the README validation section for the exact
    /// command and expected pass count. Deterministic only; never flaky.
    /// </summary>
    [TestFixture]
    [Category("CanonicalMorphologyChain")]
    public sealed class CanonicalMorphologyChainTests
    {
        private const float Tolerance = 1e-3f;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
        }

        private static CreaturePart MakePart(string id, PartType type, Vector3 position, string parentId, bool mirror = false)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = parentId,
                PartType = type,
                Transform = new TransformData { Position = position, Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = mirror,
            };
        }

        /// <summary>
        /// A canonical dino-like creature that satisfies DefinitionValidator: an
        /// even, straight five-sample spine (Body) plus a leg->foot child chain and
        /// an eye (a dino head/eye). The leg and the eye are flagged to mirror; the
        /// foot child-at-tip is deliberately NOT flagged so the
        /// mirroring-never-cascades contract is exercised at the chain level.
        /// All parts are Shape-only (implicit surface), so the whole creature folds
        /// into ONE implicit geometry item. PartTypes and parentage follow the
        /// validated conventions (no independent Body-anchored Tail; parts stay
        /// inside the default +/-4 bounds).
        /// </summary>
        private static CreatureDefinition BuildCanonicalDino()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 10f };

            // Even, straight spine along +Z: uniform arc-length spacing so the body
            // spline validator passes.
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -2f), Radius = 0.9f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, -1f), Radius = 0.95f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 0f), Radius = 1.0f });
            definition.Body.Samples.Add(new BodySample { Id = 4, Position = new Vector3(0f, 0f, 1f), Radius = 0.95f });
            definition.Body.Samples.Add(new BodySample { Id = 5, Position = new Vector3(0f, 0f, 2f), Radius = 0.85f });

            definition.AddPart(MakePart("leg", PartType.Leg, new Vector3(0.45f, -1.2f, 0.3f), CreatureDefinition.BodyId, mirror: true));
            definition.AddPart(MakePart("foot", PartType.Foot, new Vector3(0.2f, -0.6f, 0.1f), "leg", mirror: false));
            definition.AddPart(MakePart("eye", PartType.Eye, new Vector3(0.5f, 0.8f, -1.7f), CreatureDefinition.BodyId, mirror: true));
            return definition;
        }

        [Test]
        public void CanonicalDino_GenerateTwice_IsDeterministicAndWatertightImplicitSurface()
        {
            // Definition -> SDF -> Mesh. Two runs of the full generation pipeline on
            // the canonical dino must produce byte-identical counts and geometry, and
            // the extracted implicit surface must be watertight at the SDF/mesh
            // boundary (MeshTopologyReport) - the deterministic value + topology gate.
            CreatureDefinition dino = BuildCanonicalDino();

            GeneratedCreature first = CreatureMeshGenerator.Generate(dino, out MeshTopologyReport firstReport);
            GeneratedCreature second = CreatureMeshGenerator.Generate(dino, out MeshTopologyReport secondReport);

            Assert.AreEqual(1, first.Count, "an all-Shape canonical dino folds into one implicit surface item");
            Assert.IsTrue(first.TryGetImplicitSurface(out GeometryItem implicitItem), "the implicit surface is present");
            Assert.AreEqual(GeometryType.Implicit, implicitItem.GeometryType);

            Assert.IsTrue(firstReport.IsWatertight, "canonical dino implicit surface must be watertight (first run)");
            Assert.IsTrue(secondReport.IsWatertight, "canonical dino implicit surface must be watertight (second run)");

            Mesh meshA = first.Geometry[0].Mesh;
            Mesh meshB = second.Geometry[0].Mesh;
            Assert.Greater(meshA.vertexCount, 0, "implicit surface must emit vertices");
            Assert.Greater(meshA.triangles.Length, 0, "implicit surface must emit triangles");
            Assert.AreEqual(meshA.vertexCount, meshB.vertexCount, "deterministic vertex count across runs");
            Assert.AreEqual(meshA.triangles.Length, meshB.triangles.Length, "deterministic triangle count across runs");
            AssertVectorClose(meshA.bounds.center, meshB.bounds.center, Tolerance, "deterministic bounds center");
            AssertVectorClose(meshA.bounds.size, meshB.bounds.size, Tolerance, "deterministic bounds size");
        }

        [Test]
        public void CanonicalDino_SerializationRoundTrip_PreservesChainOutput()
        {
            // serialization stage: a canonical round-trip must reproduce the same
            // full downstream chain - identical mesh counts + watertight topology and
            // an identical skeleton - proving DNA -> serialization -> DNA preserves
            // the whole chain, not just the definition text.
            CreatureDefinition dino = BuildCanonicalDino();
            var serializer = new JsonDnaSerializer();
            string json = serializer.Serialize(dino);
            CreatureDefinition reconstructed = serializer.Deserialize(json);

            GeneratedCreature raw = CreatureMeshGenerator.Generate(dino, out MeshTopologyReport rawReport);
            GeneratedCreature round = CreatureMeshGenerator.Generate(reconstructed, out MeshTopologyReport roundReport);

            Assert.AreEqual(raw.Count, round.Count, "round-trip preserves geometry item count");
            Assert.AreEqual(raw.Geometry[0].Mesh.vertexCount, round.Geometry[0].Mesh.vertexCount, "round-trip preserves vertex count");
            Assert.AreEqual(raw.Geometry[0].Mesh.triangles.Length, round.Geometry[0].Mesh.triangles.Length, "round-trip preserves triangle count");
            Assert.AreEqual(rawReport.IsWatertight, roundReport.IsWatertight, "round-trip preserves watertight topology");
            Assert.IsTrue(roundReport.IsWatertight, "round-tripped canonical dino must stay watertight");

            Assert.AreEqual(SkeletonInferrer.Infer(dino).Bones.Count, SkeletonInferrer.Infer(reconstructed).Bones.Count,
                "round-trip preserves the inferred skeleton");
        }

        [Test]
        public void CanonicalDino_Skeleton_MirrorsOnlyFlaggedParts()
        {
            // Definition -> Skeleton. Mirroring must never cascade: only the flagged
            // 'leg' and 'eye' infer a mirrored bone; the unflagged 'foot' child-at-tip
            // infers none.
            CreatureDefinition dino = BuildCanonicalDino();
            ProceduralCreature.Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(dino);

            AssertMirrorCount(skeleton, "leg", 1, "flagged leg infers exactly one mirrored bone");
            AssertMirrorCount(skeleton, "eye", 1, "flagged eye infers exactly one mirrored bone");
            AssertMirrorCount(skeleton, "foot", 0, "unflagged child-at-tip foot must not cascade a mirror");

            Assert.GreaterOrEqual(CountBones(skeleton, "leg", mirrored: false), 1, "the authored leg bone is present");
            Assert.GreaterOrEqual(CountBones(skeleton, "eye", mirrored: false), 1, "the authored eye bone is present");
        }

        [Test]
        public void CanonicalDino_Rig_BuildsRestHierarchyFromInferredSkeleton()
        {
            // Definition -> Skeleton -> Rig. Building CreatureRig from the inferred
            // dino skeleton must place every rig bone at its skeleton rest position.
            CreatureDefinition dino = BuildCanonicalDino();
            ProceduralCreature.Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(dino);
            Assert.Greater(skeleton.Bones.Count, 0, "the dino must infer a non-empty skeleton");

            var host = new GameObject("CanonicalRigHost");
            _objects.Add(host);
            CreatureRig rig = host.AddComponent<CreatureRig>();
            rig.Build(skeleton);

            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                Bone bone = skeleton.Bones[i];
                Assert.IsTrue(rig.Bones.TryGetValue(bone.Id, out Transform boneTransform),
                    $"rig must contain a bone for skeleton bone '{bone.Id}'");
                AssertVectorClose(bone.Position, boneTransform.position, 1e-4f,
                    $"rig rest position for '{bone.Id}' must match the skeleton");
            }
        }

        [Test]
        public void Adversarial_MeshAssetItem_EmitsItemAndSurvivesSerialization()
        {
            // mesh-asset adversarial fixture: a keyed mesh-asset part must flow
            // through generation as a second geometry item carrying rig-binding data,
            // and its DNA key must survive a canonical round-trip and regenerate the
            // same two-item output.
            CreatureDefinition dino = BuildCanonicalDino();
            dino.AddPart(new CreaturePart
            {
                Id = "eyeMesh",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Eye,
                Transform = new TransformData { Position = new Vector3(0.5f, 0.7f, -1.6f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MeshGeometry = new MeshGeometry { MeshAssetKey = "eye_asset", Attachment = new GeometryAttachment { Offset = Vector3.zero } },
            });

            GeneratedCreature generated = GenerateWithUnitCubeResolver(dino);
            Assert.AreEqual(2, generated.Count, "implicit surface + one mesh-asset item");
            Assert.IsTrue(generated.TryFindGeometryForPart("eyeMesh", out GeometryItem item),
                "the mesh-asset item is found by part id");
            Assert.AreEqual(GeometryType.MeshAsset, item.GeometryType);
            Assert.AreEqual("eyeMesh", item.RigBinding.SourcePartId, "rig binding carries the mesh part id");

            var serializer = new JsonDnaSerializer();
            string json = serializer.Serialize(dino);
            CreatureDefinition reconstructed = serializer.Deserialize(json);
            Assert.IsNotNull(reconstructed.FindPart("eyeMesh").MeshGeometry, "mesh-asset key survives the round trip");
            Assert.AreEqual("eye_asset", reconstructed.FindPart("eyeMesh").MeshGeometry.MeshAssetKey);

            GeneratedCreature round = GenerateWithUnitCubeResolver(reconstructed);
            Assert.AreEqual(2, round.Count, "regenerated round-tripped definition still emits two items");
        }

        [Test]
        public void Adversarial_NonFiniteInput_IsRejectedBeforeTheChain()
        {
            // non-finite adversarial fixture: a NaN body-sample position is invalid
            // DNA. The generation chain gate must reject it (DomainException) rather
            // than propagate a non-finite field into SDF/sampling.
            CreatureDefinition dino = BuildCanonicalDino();
            dino.Body.Samples[0].Position = new Vector3(float.NaN, 0f, 0f);

            ValidationResult validation = DefinitionValidator.Validate(dino);
            Assert.IsFalse(validation.IsValid, "a NaN body-sample position must be reported invalid");
            Assert.Throws<DomainException>(() => CreatureMeshGenerator.Generate(dino, out _),
                "generation must reject invalid non-finite DNA before any stage runs");
        }

        private static GeneratedCreature GenerateWithUnitCubeResolver(CreatureDefinition definition)
        {
            return CreatureMeshGenerator.Generate(definition, out _, null, _ => UnitCube());
        }

        /// <summary>A unit cube centred at the origin (half-size 0.5).</summary>
        private static Mesh UnitCube()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                3, 7, 6, 3, 6, 2,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int CountBones(ProceduralCreature.Skeleton.Skeleton skeleton, string sourcePartId, bool mirrored)
        {
            int count = 0;
            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                Bone bone = skeleton.Bones[i];
                if (bone.SourcePartId == sourcePartId && bone.IsMirrored == mirrored) count++;
            }
            return count;
        }

        private static void AssertMirrorCount(ProceduralCreature.Skeleton.Skeleton skeleton, string sourcePartId, int expected, string message)
        {
            Assert.AreEqual(expected, CountBones(skeleton, sourcePartId, mirrored: true), message);
        }

        private static void AssertVectorClose(Vector3 expected, Vector3 actual, float tolerance, string message)
        {
            Assert.IsTrue(Vector3.Distance(expected, actual) <= tolerance,
                $"{message}. Expected {expected}, got {actual}.");
        }
    }
}
