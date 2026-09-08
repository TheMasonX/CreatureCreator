using System;
using System.Collections.Generic;
using ProceduralCreature.Common;
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
        public int ContourResolutionCallCount { get; internal set; }
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
        /// triangle face normals. The topology and vertex-domain contract is checked
        /// before indexing so manually-constructed malformed results fail with a
        /// domain error instead of surfacing as arbitrary IndexOutOfRange behavior.
        /// Idempotent — safe to call more than once; recomputes from scratch each time
        /// rather than accumulating on stale data. Arithmetic overflow is rejected at
        /// the geometry boundary too: finite coordinates alone do not guarantee that
        /// edge vectors or cross products remain representable as finite floats.
        /// </summary>
        public void ComputeAngleWeightedNormals()
        {
            ValidateTopology();
            var accumulated = new Vector3[Positions.Count];

            for (int i = 0; i < Triangles.Count; i += 3)
            {
                int i0 = Triangles[i];
                int i1 = Triangles[i + 1];
                int i2 = Triangles[i + 2];

                Vector3 p0 = Positions[i0];
                Vector3 p1 = Positions[i1];
                Vector3 p2 = Positions[i2];
                Vector3 edgeA = p1 - p0;
                Vector3 edgeB = p2 - p0;
                if (!NumericValidity.IsFinite(edgeA) || !NumericValidity.IsFinite(edgeB))
                {
                    throw new DomainException($"Triangle starting at index {i} has a non-finite edge vector.");
                }

                Vector3 faceNormal = Vector3.Cross(edgeA, edgeB);
                if (!NumericValidity.IsFinite(faceNormal))
                {
                    throw new DomainException($"Triangle starting at index {i} produced a non-finite face normal.");
                }
                if (faceNormal.sqrMagnitude < 1e-12f) continue;
                faceNormal.Normalize();
                if (!NumericValidity.IsFinite(faceNormal))
                {
                    throw new DomainException($"Triangle starting at index {i} produced an invalid normalized face normal.");
                }

                accumulated[i0] += faceNormal * AngleAt(p0, p1, p2);
                accumulated[i1] += faceNormal * AngleAt(p1, p2, p0);
                accumulated[i2] += faceNormal * AngleAt(p2, p0, p1);
            }

            var normals = new List<Vector3>(Positions.Count);
            foreach (Vector3 n in accumulated)
            {
                Vector3 normal = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
                if (!NumericValidity.IsFinite(normal))
                {
                    throw new DomainException("Accumulated mesh normal became non-finite.");
                }
                normals.Add(normal);
            }
            Normals = normals;
        }

        private void ValidateTopology()
        {
            if (Positions == null) throw new DomainException("Positions must not be null.");
            if (Triangles == null) throw new DomainException("Triangles must not be null.");
            if (Triangles.Count % 3 != 0)
            {
                throw new DomainException(
                    $"Triangles must contain complete triangles; got {Triangles.Count} indices.");
            }

            for (int i = 0; i < Positions.Count; i++)
            {
                if (!NumericValidity.IsFinite(Positions[i]))
                {
                    throw new DomainException($"Positions[{i}] must be finite.");
                }
            }

            for (int i = 0; i < Triangles.Count; i += 3)
            {
                int i0 = Triangles[i];
                int i1 = Triangles[i + 1];
                int i2 = Triangles[i + 2];
                if (i0 < 0 || i0 >= Positions.Count)
                {
                    throw new DomainException(
                        $"Triangles[{i}] references vertex {i0}, outside Positions (count {Positions.Count}).");
                }
                if (i1 < 0 || i1 >= Positions.Count)
                {
                    throw new DomainException(
                        $"Triangles[{i + 1}] references vertex {i1}, outside Positions (count {Positions.Count}).");
                }
                if (i2 < 0 || i2 >= Positions.Count)
                {
                    throw new DomainException(
                        $"Triangles[{i + 2}] references vertex {i2}, outside Positions (count {Positions.Count}).");
                }
                if (i0 == i1 || i1 == i2 || i0 == i2)
                {
                    throw new DomainException(
                        $"Triangle starting at index {i} contains duplicate vertex indices ({i0}, {i1}, {i2}).");
                }
            }
        }

        private static float AngleAt(Vector3 vertex, Vector3 a, Vector3 b)
        {
            Vector3 toA = (a - vertex).normalized;
            Vector3 toB = (b - vertex).normalized;
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(toA, toB), -1f, 1f));
        }

        public Mesh ToUnityMesh()
        {
            ValidateTopology();
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
