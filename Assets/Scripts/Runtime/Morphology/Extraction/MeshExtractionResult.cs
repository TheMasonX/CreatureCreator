using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralCreature.Morphology.Extraction
{
    /// <summary>
    /// Plain-data extraction output: positions and triangle indices only. Normals
    /// and UVs belong to Phase 4 (appearance baking), which is a separate stage by
    /// design (design doc §8) — this keeps mesh extraction testable and usable
    /// without pulling in appearance concerns.
    /// </summary>
    public sealed class MeshExtractionResult
    {
        public List<Vector3> Positions { get; } = new List<Vector3>();
        public List<int> Triangles { get; } = new List<int>();
        public int MixedCellCount { get; internal set; }
        public int GradientEvaluationCount { get; internal set; }

        /// <summary>
        /// Number of times the extractor called <see cref="CubeContourResolver"/>.
        /// One call per active (mixed-sign) cell, so equality with
        /// <see cref="MixedCellCount"/> proves homogeneous cells never reach the
        /// contour resolver.
        /// </summary>
        public int ContourResolutionCallCount { get; internal set; }

        /// <summary>
        /// Time spent building the active-cell metadata: the single dense scan
        /// that classifies every cell and retains only mixed-sign cells. This
        /// replaced the extractor's old full-volume classification pass.
        /// </summary>
        public TimeSpan ActiveCellConstructionTime { get; internal set; }
        public TimeSpan ContourResolutionTime { get; internal set; }
        public TimeSpan VertexWeldingTime { get; internal set; }
        public TimeSpan TriangleEmissionTime { get; internal set; }

        /// <summary>
        /// Populated by ComputeAngleWeightedNormals(); empty until then. Kept as
        /// plain data (not delegated to Unity's Mesh.RecalculateNormals) so Phase 4
        /// appearance baking can consume normals without first constructing a
        /// Unity Mesh object — mesh extraction and appearance stay independently
        /// testable, matching the design doc's stage separation (§8).
        /// </summary>
        public List<Vector3> Normals { get; private set; } = new List<Vector3>();

        public int TriangleCount => Triangles.Count / 3;

        /// <summary>
        /// Computes per-vertex normals via angle-weighted accumulation of adjacent
        /// triangle face normals (each triangle's contribution to a vertex is
        /// weighted by the angle it subtends at that vertex, which handles
        /// irregular triangle sizes/shapes better than a plain area- or
        /// count-weighted average). Idempotent — safe to call more than once;
        /// recomputes from scratch each time rather than accumulating on stale data.
        /// </summary>
        public void ComputeAngleWeightedNormals()
        {
            var accumulated = new Vector3[Positions.Count];

            for (int i = 0; i < Triangles.Count; i += 3)
            {
                int i0 = Triangles[i];
                int i1 = Triangles[i + 1];
                int i2 = Triangles[i + 2];

                Vector3 p0 = Positions[i0];
                Vector3 p1 = Positions[i1];
                Vector3 p2 = Positions[i2];

                Vector3 faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
                if (faceNormal.sqrMagnitude < 1e-12f) continue; // degenerate triangle, no contribution
                faceNormal.Normalize();

                accumulated[i0] += faceNormal * AngleAt(p0, p1, p2);
                accumulated[i1] += faceNormal * AngleAt(p1, p2, p0);
                accumulated[i2] += faceNormal * AngleAt(p2, p0, p1);
            }

            var normals = new List<Vector3>(Positions.Count);
            foreach (Vector3 n in accumulated)
            {
                normals.Add(n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up);
            }
            Normals = normals;
        }

        private static float AngleAt(Vector3 vertex, Vector3 a, Vector3 b)
        {
            Vector3 toA = (a - vertex).normalized;
            Vector3 toB = (b - vertex).normalized;
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(toA, toB), -1f, 1f));
        }

        public Mesh ToUnityMesh()
        {
            var mesh = new Mesh();
            if (Positions.Count > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(Positions);
            mesh.SetTriangles(Triangles, 0);

            if (Normals.Count == Positions.Count && Positions.Count > 0)
            {
                mesh.SetNormals(Normals);
            }
            else
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
