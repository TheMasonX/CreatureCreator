using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// On-demand <c>CreatureMeshGenerator.GenerateData</c> benchmark harness for the
    /// TSK-0008 preview-generation performance gate (TSK-0135).
    ///
    /// The harness is INTENTIONALLY opt-in: it contains NO [Test] / [UnityTest]
    /// methods, so it is never auto-discovered and never runs in the default Runtime
    /// PlayMode suite. Timing is environment-sensitive and must not flake the always-run
    /// suite, so this benchmark is invoked explicitly (e.g. Unity MCP execute_code /
    /// a manual editor call) and prints a report to the console. Results are recorded
    /// by the caller (task comments / the note in Assets/Scripts/README.md).
    ///
    /// It reuses the existing instrumentation path: each rep calls
    /// <c>CreatureMeshGenerator.GenerateData(definition, diagnostics)</c> with a fresh
    /// <c>GenerationDiagnostics</c>, then reads the per-stage timings
    /// (FieldSampling, SdfCompile, MeshExtraction, the extraction sub-stages
    /// MeshActiveCellConstruction / MeshContourResolution, and TotalTime). No
    /// production generation code is modified.
    /// </summary>
    public static class GenerateDataBenchmark
    {
        /// <summary>Default number of timed reps per VPU (a warmup rep is added and discarded).</summary>
        public const int DefaultReps = 7;

        /// <summary>The VPUs the TSK-0008 gate tracks.</summary>
        public static readonly int[] GateVpus = { 10, 16 };

        // ---- representative fixture -------------------------------------------

        /// <summary>
        /// A deterministic, self-contained multi-part representative creature: a torso
        /// body spline plus fused spherical head/tail/leg/eye parts. Built fresh for
        /// the requested VoxelsPerUnit so reps never mutate a shared definition. The
        /// body-spline + spherical-decorations authoring pattern is the one already
        /// proven by the committed CC-091 parity / Definition fixtures (no limb-chain
        /// machinery is needed, keeping validation simple and the output watertight).
        /// </summary>
        public static CreatureDefinition BuildFixture(float voxelsPerUnit)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Generation = new GenerationSettings { VoxelsPerUnit = voxelsPerUnit };

            // Torso: overlapping body spheres along Z (the dorsal line).
            float[] zs = { -1.0f, -0.5f, 0.0f, 0.5f, 1.0f };
            float[] radii = { 0.70f, 0.85f, 0.90f, 0.85f, 0.70f };
            for (int i = 0; i < zs.Length; i++)
            {
                definition.Body.Samples.Add(new BodySample
                {
                    Id = (uint)(i + 1),
                    Position = new Vector3(0f, 0f, zs[i]),
                    Radius = radii[i],
                });
            }

            // Head, tail and four legs + two eyes as fused spherical decorations.
            AddSphere(definition, "head", new Vector3(0f, 0.35f, 1.55f), 0.55f);
            AddSphere(definition, "tail", new Vector3(0f, 0.05f, -1.75f), 0.30f);
            AddSphere(definition, "leg_fl", new Vector3(0.55f, -0.95f, 0.55f), 0.30f);
            AddSphere(definition, "leg_fr", new Vector3(-0.55f, -0.95f, 0.55f), 0.30f);
            AddSphere(definition, "leg_bl", new Vector3(0.55f, -0.95f, -0.55f), 0.30f);
            AddSphere(definition, "leg_br", new Vector3(-0.55f, -0.95f, -0.55f), 0.30f);
            AddSphere(definition, "eye_l", new Vector3(0.42f, 0.55f, 1.35f), 0.13f);
            AddSphere(definition, "eye_r", new Vector3(-0.42f, 0.55f, 1.35f), 0.13f);
            return definition;
        }

        private static void AddSphere(CreatureDefinition definition, string id, Vector3 position, float radius)
        {
            definition.AddPart(new CreaturePart
            {
                Id = id,
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Part,
                Transform = new TransformData { Position = position, Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = new ShapeDefinition
                {
                    Type = ShapeType.Sphere,
                    Radius = radius,
                    PrimarySize = radius,
                    CapsuleAxis = ShapeAxis.Y,
                    CapsuleHeight = radius * 2f,
                    EllipsoidRadii = new Vector3(radius, radius, radius),
                    BoxHalfExtents = new Vector3(radius, radius, radius),
                    SmoothBlendRadius = 0.05f,
                },
                Appearance = AppearanceDefinition.Default,
            });
        }

        // ---- running -----------------------------------------------------------

        /// <summary>
        /// Runs the benchmark for <see cref="GateVpus"/> at <see cref="DefaultReps"/> reps
        /// each and Debug.Log's a formatted report. Returns the same report string.
        /// </summary>
        public static string Run(int reps = DefaultReps)
        {
            return Run(reps, GateVpus);
        }

        /// <summary>
        /// Runs the benchmark for the given VPUs and rep count. Returns a formatted report
        /// (also logged via UnityEngine.Debug.Log). A warmup rep is always run first and
        /// discarded (JIT / first-call cost); the timed reps report median and min.
        /// </summary>
        public static string Run(int reps, IReadOnlyList<int> vpus)
        {
            if (vpus == null || vpus.Count == 0) throw new ArgumentException("vpus must not be empty.");
            if (reps < 1) throw new ArgumentException("reps must be >= 1.");

            var report = new StringBuilder();
            report.AppendLine("=== GenerateData benchmark (TSK-0135 / TSK-0008 perf gate) ===");
            report.AppendLine($"Unity version: {Application.unityVersion}");
            report.AppendLine($"Reps per VPU (timed): {reps}  (+1 warmup, discarded)");
            report.AppendLine("Methodology: median over timed reps; 'min' is the GC-clean proxy (least GC-stall");
            report.AppendLine("  influence). Timings are environment-sensitive; treat as relative, not absolute.");
            report.AppendLine("Allocation methodology: per-rep managed growth sampled via");
            report.AppendLine("  UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong deltas. Burst/native");
            report.AppendLine("  buffer and Unity object allocations are OUTSIDE the managed heap and are not");
            report.AppendLine("  captured here (requires the profiler); reported values are managed-heap only.");
            report.AppendLine();

            foreach (int vpu in vpus)
            {
                AppendVpuBlock(report, vpu, reps);
            }

            report.AppendLine("=== end benchmark ===");
            string text = report.ToString();
            Debug.Log(text);
            return text;
        }

        private static void AppendVpuBlock(StringBuilder report, int vpu, int reps)
        {
            CreatureDefinition fixture = BuildFixture(vpu);

            // Sanity + warmup: run once, discard timing, assert generation succeeded and
            // produced a non-trivial mesh so the reported numbers are meaningful.
            GenerationDiagnostics warm = new GenerationDiagnostics();
            GeneratedCreatureData warmData = CreatureMeshGenerator.GenerateData(fixture, warm);
            AssertHealthy(warm, warmData, vpu);

            // Settle managed GC once before the timed reps so cross-rep GC spikes are
            // rarer; the median still guards against the occasional remaining stall.
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();

            var totals = new List<double>(reps);
            var fieldSampling = new List<double>(reps);
            var sdfCompile = new List<double>(reps);
            var meshExtraction = new List<double>(reps);
            var activeCell = new List<double>(reps);
            var contour = new List<double>(reps);
            var managedBytesPerRep = new List<long>(reps);

            for (int i = 0; i < reps; i++)
            {
                long before = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                var diagnostics = new GenerationDiagnostics();
                GeneratedCreatureData data = CreatureMeshGenerator.GenerateData(fixture, diagnostics);
                long after = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

                totals.Add(diagnostics.TotalTime.TotalMilliseconds);
                fieldSampling.Add(StageMs(diagnostics, GenerationStage.FieldSampling));
                sdfCompile.Add(StageMs(diagnostics, GenerationStage.SdfCompile));
                meshExtraction.Add(StageMs(diagnostics, GenerationStage.MeshExtraction));
                activeCell.Add(StageMs(diagnostics, GenerationStage.MeshActiveCellConstruction));
                contour.Add(StageMs(diagnostics, GenerationStage.MeshContourResolution));
                managedBytesPerRep.Add(Math.Max(0L, after - before));
            }

            report.AppendLine($"--- VPU {vpu} (voxelsPerUnit = {vpu}) ---");
            report.AppendLine($"  grid cells: {warm.GridCellsX} x {warm.GridCellsY} x {warm.GridCellsZ}; samples: {warm.GridSampleCount}");
            report.AppendLine($"  output: {warm.TriangleCount} triangles, {warm.VertexCount} vertices; watertight={warmData.TopologyReport?.IsWatertight.ToString() ?? "n/a"}");
            report.AppendLine($"  total        median {Median(totals),8:F1} ms   min {totals.Min(),8:F1} ms");
            report.AppendLine($"  SdfCompile   median {Median(sdfCompile),8:F1} ms   min {sdfCompile.Min(),8:F1} ms");
            report.AppendLine($"  FieldSampling median {Median(fieldSampling),8:F1} ms   min {fieldSampling.Min(),8:F1} ms");
            report.AppendLine($"  MeshExtraction(all) median {Median(meshExtraction),8:F1} ms   min {meshExtraction.Min(),8:F1} ms");
            report.AppendLine($"    - ActiveCellConstruction median {Median(activeCell),8:F1} ms");
            report.AppendLine($"    - ContourResolution     median {Median(contour),8:F1} ms");
            report.AppendLine($"  managed alloc/rep (median): {MedianLong(managedBytesPerRep) / 1024.0:F1} KiB");
            report.AppendLine();
        }

        private static void AssertHealthy(GenerationDiagnostics diagnostics, GeneratedCreatureData data, int vpu)
        {
            if (!diagnostics.Succeeded)
            {
                throw new InvalidOperationException(
                    $"GenerateData benchmark fixture failed at VPU {vpu} (stage {diagnostics.FailedStage}).");
            }
            if (data == null || diagnostics.TriangleCount == 0)
            {
                throw new InvalidOperationException(
                    $"GenerateData benchmark fixture produced no mesh at VPU {vpu}; cannot benchmark.");
            }
        }

        private static double StageMs(GenerationDiagnostics diagnostics, GenerationStage stage)
        {
            for (int i = 0; i < diagnostics.Timings.Count; i++)
            {
                StageTiming t = diagnostics.Timings[i];
                if (t.Stage == stage) return t.Elapsed.TotalMilliseconds;
            }
            return 0.0;
        }

        private static double Median(List<double> values)
        {
            double[] sorted = values.ToArray();
            Array.Sort(sorted);
            return sorted[sorted.Length / 2];
        }

        private static long MedianLong(List<long> values)
        {
            long[] sorted = values.ToArray();
            Array.Sort(sorted);
            return sorted[sorted.Length / 2];
        }
    }
}
