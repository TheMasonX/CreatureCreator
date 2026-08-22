using System.Collections.Generic;
using UnityEngine;

namespace ProceduralCreature.Morphology.Extraction
{
    public sealed class MeshTopologyReport
    {
        public bool IsWatertight => NonManifoldEdgeCount == 0 && BoundaryEdgeCount == 0;
        public int NonManifoldEdgeCount { get; internal set; }
        public int BoundaryEdgeCount { get; internal set; }
        public int TotalEdgeCount { get; internal set; }
    }

    /// <summary>
    /// Checks the closed-surface invariant every generated creature mesh must
    /// satisfy: every edge is shared by exactly 2 triangles (a "boundary edge",
    /// shared by only 1, means a hole; a "non-manifold edge", shared by 3+, means
    /// self-intersecting or duplicated geometry).
    ///
    /// This is the actual safety net for CubeContourResolver's hand-derived logic
    /// (see delta-audit item #1 and the design notes there): rather than trusting
    /// that logic is correct because its derivation reads convincingly, this
    /// validator checks the property we actually care about — no holes — directly
    /// on the output. Run this against golden-fixture creatures as the first real
    /// test before trusting this extractor in production (README).
    /// </summary>
    public static class MeshTopologyValidator
    {
        public static MeshTopologyReport Validate(MeshExtractionResult mesh)
        {
            var edgeUseCounts = new Dictionary<(int, int), int>();

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

            foreach (int count in edgeUseCounts.Values)
            {
                if (count == 1) report.BoundaryEdgeCount++;
                else if (count > 2) report.NonManifoldEdgeCount++;
            }

            return report;
        }

        private static void CountEdge(Dictionary<(int, int), int> counts, int a, int b)
        {
            (int, int) key = a < b ? (a, b) : (b, a);
            counts[key] = counts.TryGetValue(key, out int existing) ? existing + 1 : 1;
        }
    }
}
