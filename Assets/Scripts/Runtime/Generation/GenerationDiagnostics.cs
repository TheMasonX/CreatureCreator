using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Generation
{
    /// <summary>Which pipeline stage a diagnostic or timing entry belongs to (§11 CreatureGenerationPipeline stages).</summary>
    public enum GenerationStage
    {
        Validation,
        SdfCompile,
        FieldSampling,
        MeshExtraction,
        MeshActiveCellConstruction,
        MeshContourResolution,
        MeshVertexWelding,
        MeshTriangleEmission,
        MeshValidation,
        SkeletonInference,
        CenterOfMass,
        AppearanceBake,
    }

    public readonly struct StageTiming
    {
        public readonly GenerationStage Stage;
        public readonly TimeSpan Elapsed;

        public StageTiming(GenerationStage stage, TimeSpan elapsed)
        {
            Stage = stage;
            Elapsed = elapsed;
        }
    }

    /// <summary>
    /// Mutable collector for one generation run. Consumers receive fixed read-only
    /// views, so the completed diagnostic surface cannot be mutated through an
    /// IReadOnlyList downcast. All exceptions escaping a timed stage mark that stage
    /// failed before being rethrown, keeping Succeeded consistent with the scheduler's
    /// all-exceptions failure boundary.
    /// </summary>
    public sealed class GenerationDiagnostics
    {
        private readonly List<StageTiming> _timings = new List<StageTiming>();
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();
        private readonly ReadOnlyCollection<StageTiming> _timingsView;
        private readonly ReadOnlyCollection<ValidationIssue> _issuesView;

        public GenerationDiagnostics(bool collectTimings = true)
        {
            CollectTimings = collectTimings;
            _timingsView = _timings.AsReadOnly();
            _issuesView = _issues.AsReadOnly();
        }

        public GenerationStage? FailedStage { get; private set; }
        public bool CollectTimings { get; }
        public TimeSpan TotalTime { get; private set; }
        public int GridCellsX { get; private set; }
        public int GridCellsY { get; private set; }
        public int GridCellsZ { get; private set; }
        public int GridSampleCount { get; private set; }
        public int MixedCellCount { get; private set; }
        public int GradientEvaluationCount { get; private set; }
        public int VertexCount { get; private set; }
        public int TriangleCount { get; private set; }

        public IReadOnlyList<StageTiming> Timings => _timingsView;
        public IReadOnlyList<ValidationIssue> Issues => _issuesView;

        public bool Succeeded => FailedStage == null;

        public void RecordGridDimensions(int cellsX, int cellsY, int cellsZ, int sampleCount)
        {
            GridCellsX = cellsX;
            GridCellsY = cellsY;
            GridCellsZ = cellsZ;
            GridSampleCount = sampleCount;
        }

        public void RecordExtractionStatistics(int mixedCellCount, int gradientEvaluationCount)
        {
            MixedCellCount = mixedCellCount;
            GradientEvaluationCount = gradientEvaluationCount;
        }

        public void RecordMeshStatistics(int vertexCount, int triangleCount)
        {
            VertexCount = vertexCount;
            TriangleCount = triangleCount;
        }

        public void RecordExtractionTiming(
            TimeSpan activeCellConstruction,
            TimeSpan contourResolution,
            TimeSpan vertexWelding,
            TimeSpan triangleEmission)
        {
            RecordTiming(GenerationStage.MeshActiveCellConstruction, activeCellConstruction);
            RecordTiming(GenerationStage.MeshContourResolution, contourResolution);
            RecordTiming(GenerationStage.MeshVertexWelding, vertexWelding);
            RecordTiming(GenerationStage.MeshTriangleEmission, triangleEmission);
        }

        public void RecordTiming(GenerationStage stage, TimeSpan elapsed)
        {
            if (!CollectTimings) return;
            _timings.Add(new StageTiming(stage, elapsed));
            if (!IsMeshSubtiming(stage))
            {
                TotalTime += elapsed;
            }
        }

        private static bool IsMeshSubtiming(GenerationStage stage)
        {
            return stage == GenerationStage.MeshActiveCellConstruction
                   || stage == GenerationStage.MeshContourResolution
                   || stage == GenerationStage.MeshVertexWelding
                   || stage == GenerationStage.MeshTriangleEmission;
        }

        public void RecordIssue(ValidationIssue issue)
        {
            if (issue == null) throw new ArgumentNullException(nameof(issue));
            _issues.Add(issue);
        }

        public void RecordIssues(IEnumerable<ValidationIssue> issues)
        {
            if (issues == null) throw new ArgumentNullException(nameof(issues));
            foreach (ValidationIssue issue in issues)
            {
                RecordIssue(issue);
            }
        }

        /// <summary>Marks the first failing stage; later failures do not replace it.</summary>
        public void MarkFailed(GenerationStage stage)
        {
            FailedStage ??= stage;
        }

        /// <summary>
        /// Times one stage and marks it failed for every exception type before
        /// propagating that exception. This matches the generation scheduler boundary,
        /// which treats all escaping exceptions as failed generation rather than only
        /// domain/user-data exceptions.
        /// </summary>
        public void TimeStage(GenerationStage stage, Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (!CollectTimings)
            {
                try
                {
                    action();
                }
                catch (Exception)
                {
                    MarkFailed(stage);
                    throw;
                }
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                action();
            }
            catch (Exception)
            {
                MarkFailed(stage);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                RecordTiming(stage, stopwatch.Elapsed);
            }
        }
    }
}
