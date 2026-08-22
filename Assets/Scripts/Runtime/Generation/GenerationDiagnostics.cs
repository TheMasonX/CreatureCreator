using System;
using System.Collections.Generic;
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
    /// Centralized diagnostics collected across one generation run: which stage
    /// failed, how long each stage took, and any validation issues surfaced along
    /// the way. This is what "Generation failures must identify the stage and, when
    /// possible, the part/parameter responsible" (§14) is built on top of.
    ///
    /// Deliberately append-only and stage-scoped rather than a single flat log, so
    /// timing hooks (Sprint 0.2: "performance timing hooks for SDF sampling, mesh
    /// extraction, skeleton inference, and appearance baking") don't require
    /// per-voxel logging noise — one StageTiming entry per stage per run.
    /// </summary>
    public sealed class GenerationDiagnostics
    {
        private readonly List<StageTiming> _timings = new List<StageTiming>();
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();

        public GenerationStage? FailedStage { get; private set; }

        public IReadOnlyList<StageTiming> Timings => _timings;
        public IReadOnlyList<ValidationIssue> Issues => _issues;

        public bool Succeeded => FailedStage == null;

        public void RecordTiming(GenerationStage stage, TimeSpan elapsed)
        {
            _timings.Add(new StageTiming(stage, elapsed));
        }

        public void RecordIssue(ValidationIssue issue)
        {
            _issues.Add(issue);
        }

        public void RecordIssues(IEnumerable<ValidationIssue> issues)
        {
            _issues.AddRange(issues);
        }

        /// <summary>
        /// Marks the run as failed at the given stage. Idempotent-ish: the first
        /// failure recorded wins, matching "a failed generation must not partially
        /// replace the current valid runtime creature" — once a stage has failed we
        /// don't want a later stage's failure to overwrite which stage was actually
        /// responsible.
        /// </summary>
        public void MarkFailed(GenerationStage stage)
        {
            FailedStage ??= stage;
        }

        /// <summary>
        /// Convenience helper for timing a stage: runs <paramref name="action"/>,
        /// records elapsed time, and marks the stage failed if it throws a
        /// DomainException (programmer error) — user-data failures should already
        /// have been surfaced as ValidationIssues by the stage itself, not thrown.
        /// </summary>
        public void TimeStage(GenerationStage stage, Action action)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                action();
            }
            catch (Common.DomainException)
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
