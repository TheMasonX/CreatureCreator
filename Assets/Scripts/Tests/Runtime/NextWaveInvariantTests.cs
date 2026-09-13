using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Morphology;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class NextWaveInvariantTests
    {
        [Test]
        public void ResolvedBody_NormalizedArcLength_IsMonotonicAndEndsAtOne()
        {
            ResolvedBody body = ResolvedBody.Resolve(new[]
            {
                new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f },
                new BodySample { Id = 2, Position = new Vector3(0f, 0f, 2f), Radius = 1f },
                new BodySample { Id = 3, Position = new Vector3(0f, 0f, 5f), Radius = 1f },
            });

            Assert.AreEqual(body.SamplePositions.Count, body.NormalizedArcLengthAtSample.Count);
            Assert.AreEqual(0f, body.NormalizedArcLengthAtSample[0], 1e-6f);
            Assert.AreEqual(1f, body.NormalizedArcLengthAtSample[body.NormalizedArcLengthAtSample.Count - 1], 1e-6f);
            for (int i = 1; i < body.NormalizedArcLengthAtSample.Count; i++)
            {
                Assert.GreaterOrEqual(
                    body.NormalizedArcLengthAtSample[i],
                    body.NormalizedArcLengthAtSample[i - 1]);
            }
        }

        [Test]
        public void MeshTopologyValidator_DetectsBoundaryEdge()
        {
            var mesh = new MeshExtractionResult();
            mesh.Positions.Add(Vector3.zero);
            mesh.Positions.Add(Vector3.right);
            mesh.Positions.Add(Vector3.up);
            mesh.Triangles.AddRange(new[] { 0, 1, 2 });

            MeshTopologyReport report = MeshTopologyValidator.Validate(mesh);

            Assert.AreEqual(3, report.BoundaryEdgeCount);
            Assert.AreEqual(0, report.NonManifoldEdgeCount);
            Assert.IsFalse(report.IsValidClosedSurface);
            Assert.IsNotEmpty(report.BoundaryEdgeExamples);
        }

        [Test]
        public void GenerationDiagnostics_TotalTime_ExcludesMeshSubtimings()
        {
            var diagnostics = new GenerationDiagnostics();
            diagnostics.RecordTiming(GenerationStage.FieldSampling, System.TimeSpan.FromMilliseconds(10));
            diagnostics.RecordTiming(GenerationStage.MeshExtraction, System.TimeSpan.FromMilliseconds(20));
            diagnostics.RecordTiming(GenerationStage.MeshContourResolution, System.TimeSpan.FromMilliseconds(5));
            diagnostics.RecordTiming(GenerationStage.AppearanceBake, System.TimeSpan.FromMilliseconds(7));

            Assert.AreEqual(System.TimeSpan.FromMilliseconds(37), diagnostics.TotalTime);
        }

        [Test]
        public void GenerationDiagnostics_FirstFailureStage_IsStable()
        {
            var diagnostics = new GenerationDiagnostics();
            diagnostics.MarkFailed(GenerationStage.Validation);
            diagnostics.MarkFailed(GenerationStage.SdfCompile);

            Assert.AreEqual(GenerationStage.Validation, diagnostics.FailedStage.Value);
            Assert.IsFalse(diagnostics.Succeeded);
        }

        [Test]
        public void SkeletonSnapshot_RejectsDuplicateBoneIds()
        {
            var skeleton = new ProceduralCreature.Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone { Id = "root", ParentBoneId = null, Position = Vector3.zero, Rotation = Quaternion.identity });
            skeleton.Bones.Add(new Bone { Id = "root", ParentBoneId = null, Position = Vector3.one, Rotation = Quaternion.identity });

            Assert.Throws<DomainException>(() => SkeletonSnapshot.Capture(skeleton));
        }

        [Test]
        public void MeshExtractionResult_RejectsDuplicateTriangleVertexIndices()
        {
            var mesh = new MeshExtractionResult();
            mesh.Positions.Add(Vector3.zero);
            mesh.Positions.Add(Vector3.right);
            mesh.Triangles.AddRange(new[] { 0, 1, 1 });

            Assert.Throws<DomainException>(() => mesh.ComputeAngleWeightedNormals());
        }
    }
}
