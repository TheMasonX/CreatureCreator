using System.Collections.Generic;
using UnityEngine;

namespace ProceduralCreature.Morphology.Extraction
{
    public sealed class MeshTopologyReport
    {
        public bool IsWatertight => NonManifoldEdgeCount == 0 && BoundaryEdgeCount == 0;
        public bool IsConsistentlyWound => InconsistentWindingEdgeCount == 0;
        public bool IsValidClosedSurface => IsWatertight && IsConsistentlyWound;
        public int NonManifoldEdgeCount { get; internal set; }
        public int BoundaryEdgeCount { get; internal set; }
        public int InconsistentWindingEdgeCount { get; internal set; }
        public int TotalEdgeCount { get; internal set; }
        public int MaxEdgeUseCount { get; internal set; }
        public IReadOnlyList<string> BoundaryEdgeExamples => _boundaryEdgeExamples;
        public IReadOnlyList<string> NonManifoldEdgeExamples => _nonManifoldEdgeExamples;
        public IReadOnlyList<string> InconsistentWindingEdgeExamples => _inconsistentWindingEdgeExamples;

        private readonly List<string> _boundaryEdgeExamples = new List<string>();
        private readonly List<string> _nonManifoldEdgeExamples = new List<string>();
        private readonly List<string> _inconsistentWindingEdgeExamples = new List<string>();
    }

    /// <summary>
    /// Checks the closed-surface invariant every generated creature mesh must
    /// satisfy: every edge is shared by exactly 2 triangles. It also verifies the
    /// stronger orientation invariant that the two incident triangles traverse a
    /// manifold edge in opposite directions. The latter is necessary because a
    /// mesh can be topologically closed while still containing a local winding
    /// inversion that produces an inside-out patch during rendering.
    ///
    /// Only a small bounded set of example edges is retained so diagnostics remain
    /// useful without turning validation into a large logging/allocation surface.
    /// </summary>
    public static class MeshTopologyValidator
    {
        private const int MaxDiagnosticExamples = 8;

        private readonly struct EdgeUse
        {
            public readonly int Count;
            public readonly int ForwardUses;
            public readonly int ReverseUses;

            public EdgeUse(int count, int forwardUses, int reverseUses)
            {
                Count = count;
                ForwardUses = forwardUses;
                ReverseUses = reverseUses;
            }
        }

        public static MeshTopologyReport Validate(MeshExtractionResult mesh)
        {
            if (mesh == null) throw new DomainException("mesh must not be null.");
            if (mesh.Triangles == null) throw new DomainException("mesh triangles must not be null.");
            if (mesh.Triangles.Count % 3 != 0)
            {
                throw new DomainException("Mesh triangle index count must be divisible by 3.");
            }

            var edgeUseCounts = new Dictionary<(int, int), EdgeUse>();
            for (int i = 0; i < mesh.Triangles.Count; i += 3)
            {
                int a = mesh.Triangles[i];
                int b = mesh.Triangles[i + 1];
                int c = mesh.Triangles[i + 2];
                CountEdge(edgeUseCounts, a, b);
                CountEdge(edgeUseCounts, b, c);
                CountEdge(edgeUseCounts, c, a);
            }

            var report = new MeshTopologyReport { TotalEdgeCount = edgeUseCounts.Count };
            foreach (KeyValuePair<(int, int), EdgeUse> entry in edgeUseCounts)
            {
                EdgeUse use = entry.Value;
                if (use.Count > report.MaxEdgeUseCount) report.MaxEdgeUseCount = use.Count;

                if (use.Count == 1)
                {
                    report.BoundaryEdgeCount++;
                    AddExample(report.BoundaryEdgeExamples, FormatEdge(entry.Key, use));
                }
                else if (use.Count > 2)
                {
                    report.NonManifoldEdgeCount++;
                    AddExample(report.NonManifoldEdgeExamples, FormatEdge(entry.Key, use));
                }
                else if (use.Count == 2 && use.ForwardUses != 1)
                {
                    report.InconsistentWindingEdgeCount++;
                    AddExample(report.InconsistentWindingEdgeExamples, FormatEdge(entry.Key, use));
                }
            }

            return report;
        }

        private static void CountEdge(Dictionary<(int, int), EdgeUse> counts, int a, int b)
        {
            (int, int) key = a < b ? (a, b) : (b, a);
            bool forward = a < b;

            if (counts.TryGetValue(key, out EdgeUse existing))
            {
                counts[key] = new EdgeUse(
                    existing.Count + 1,
                    existing.ForwardUses + (forward ? 1 : 0),
                    existing.ReverseUses + (forward ? 0 : 1));
                return;
            }

            counts.Add(key, new EdgeUse(1, forward ? 1 : 0, forward ? 0 : 1));
        }

        private static string FormatEdge((int, int) key, EdgeUse use)
        {
            return $"edge ({key.Item1},{key.Item2}) uses={use.Count} forward={use.ForwardUses} reverse={use.ReverseUses}";
        }

        private static void AddExample(IReadOnlyList<string> destination, string value)
        {
            // The concrete collection is owned by the report; this helper is called
            // only with one of its internal lists through the IReadOnlyList interface.
            if (destination is List<string> list && list.Count < MaxDiagnosticExamples)
            {
                list.Add(value);
            }
        }
    }
}
