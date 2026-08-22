using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Morphology.Extraction
{
    /// <summary>
    /// Resolves the boundary contour of a single cube given its 8 corner
    /// densities, as a set of closed loops of interpolated edge-crossing points.
    /// This is the core Asymptotic Decider implementation, built from three small,
    /// individually-verifiable steps rather than a single opaque triangulation
    /// table:
    ///
    /// 1. PER-FACE SEGMENTS: for each of the cube's 6 faces, determine how many of
    ///    its 4 cyclic edges are crossed (sign change between endpoints). This can
    ///    only be 0, 2, or 4 — never 1 or 3 (a face's corner signs always change an
    ///    even number of times going around a cycle). 0 crossed edges means the
    ///    face contributes nothing. 2 crossed edges has exactly one valid pairing
    ///    (unambiguous — there's only one way to connect two points). 4 crossed
    ///    edges is the checkerboard/ambiguous case, resolved via AsymptoticDecider.
    ///
    /// 2. LOOP TRACING: every crossed cube edge borders exactly 2 faces, and (by
    ///    construction in step 1) is used by exactly one segment from each of
    ///    those faces — so every edge-crossing point has degree exactly 2 in the
    ///    combined segment graph. A degree-2 graph decomposes into disjoint closed
    ///    loops with no special-casing required; this is a topological guarantee,
    ///    not an assumption specific to any particular corner configuration.
    ///
    /// 3. TRIANGULATION happens outside this class (see MarchingCubesExtractor) —
    ///    this class only produces the loops.
    ///
    /// WHY THIS ELIMINATES HOLES: two cubes sharing a face compute step 1's
    /// ambiguous-face decision from the exact same four corner density values
    /// (the shared face's corners belong to both cubes identically), so they
    /// always agree on that face's connectivity. Plain Marching Cubes' table-based
    /// approach can pick different interpretations for the "same" ambiguous
    /// pattern depending on which cube's table row is consulted; this
    /// construction cannot, because there is no separate per-cube table row to
    /// disagree — the decision is a direct function of the shared data.
    /// </summary>
    public static class CubeContourResolver
    {
        /// <summary>One straight segment crossing a face, as a pair of edge indices (0-11).</summary>
        private readonly struct FaceSegment
        {
            public readonly int EdgeA;
            public readonly int EdgeB;

            public FaceSegment(int edgeA, int edgeB)
            {
                EdgeA = edgeA;
                EdgeB = edgeB;
            }
        }

        /// <summary>One point on a resolved loop: its local edge index (0-11) and interpolated position.</summary>
        public readonly struct LoopVertex
        {
            public readonly int EdgeIndex;
            public readonly Vector3 Position;

            public LoopVertex(int edgeIndex, Vector3 position)
            {
                EdgeIndex = edgeIndex;
                Position = position;
            }
        }

        /// <summary>
        /// Computes closed loops for a cube given its 8 corner densities (indexed
        /// per CubeTopology's corner numbering) and the corresponding 8 corner
        /// world positions. Returns an empty list if the cube has no surface
        /// crossing (all 8 corners share a sign). Each loop vertex carries its
        /// local edge index so callers (MarchingCubesExtractor) can weld it
        /// against the matching vertex from a neighboring cube sharing that edge.
        /// </summary>
        public static List<List<LoopVertex>> ResolveLoops(float[] cornerDensities, Vector3[] cornerPositions)
        {
            if (cornerDensities == null || cornerDensities.Length != 8)
            {
                throw new DomainException("cornerDensities must have exactly 8 entries.");
            }
            if (cornerPositions == null || cornerPositions.Length != 8)
            {
                throw new DomainException("cornerPositions must have exactly 8 entries.");
            }

            var segments = new List<FaceSegment>();
            foreach (CubeTopology.Face face in CubeTopology.AllFaces)
            {
                CollectFaceSegments(face, cornerDensities, segments);
            }

            if (segments.Count == 0)
            {
                return new List<List<LoopVertex>>();
            }

            List<List<int>> edgeLoops = TraceLoops(segments);

            var result = new List<List<LoopVertex>>(edgeLoops.Count);
            foreach (List<int> edgeLoop in edgeLoops)
            {
                var vertices = new List<LoopVertex>(edgeLoop.Count);
                foreach (int edgeIndex in edgeLoop)
                {
                    Vector3 position = InterpolateEdge(edgeIndex, cornerDensities, cornerPositions);
                    vertices.Add(new LoopVertex(edgeIndex, position));
                }
                result.Add(vertices);
            }
            return result;
        }

        private static void CollectFaceSegments(
            CubeTopology.Face face, float[] cornerDensities, List<FaceSegment> segments)
        {
            int faceIndex = (int)face;
            int[] faceCorners = CubeTopology.FaceCorners[faceIndex];
            int[] faceEdges = CubeTopology.FaceEdges[faceIndex];

            float v00 = cornerDensities[faceCorners[0]];
            float v10 = cornerDensities[faceCorners[1]];
            float v11 = cornerDensities[faceCorners[2]];
            float v01 = cornerDensities[faceCorners[3]];

            bool[] cyclicSign = { v00 >= 0f, v10 >= 0f, v11 >= 0f, v01 >= 0f };

            var crossedEdges = new List<int>(4);
            for (int k = 0; k < 4; k++)
            {
                if (cyclicSign[k] != cyclicSign[(k + 1) % 4])
                {
                    crossedEdges.Add(faceEdges[k]);
                }
            }

            switch (crossedEdges.Count)
            {
                case 0:
                    return;

                case 2:
                    segments.Add(new FaceSegment(crossedEdges[0], crossedEdges[1]));
                    return;

                case 4:
                    // Ambiguous face: crossedEdges are in cyclic order [e0,e1,e2,e3]
                    // matching faceEdges exactly (all 4 are crossed), where e0
                    // connects corner0-corner1, e1 connects corner1-corner2, etc.
                    // "Pairing A" (diagonal corners c0,c2 separated) groups
                    // (e3,e0) and (e1,e2). "Pairing B" (connected through the
                    // middle) groups (e0,e1) and (e2,e3). See AsymptoticDecider's
                    // derivation comment for why this is the correct mapping.
                    bool connectThroughMiddle = AsymptoticDecider.DiagonalConnectsThroughMiddle(v00, v10, v11, v01);
                    if (connectThroughMiddle)
                    {
                        segments.Add(new FaceSegment(faceEdges[0], faceEdges[1]));
                        segments.Add(new FaceSegment(faceEdges[2], faceEdges[3]));
                    }
                    else
                    {
                        segments.Add(new FaceSegment(faceEdges[3], faceEdges[0]));
                        segments.Add(new FaceSegment(faceEdges[1], faceEdges[2]));
                    }
                    return;

                default:
                    // Cannot happen for a valid cyclic 4-corner sign sequence —
                    // the number of sign changes going around a cycle is always
                    // even. A genuine programmer error (corrupted input) if reached.
                    throw new DomainException(
                        $"Face produced {crossedEdges.Count} crossed edges; expected 0, 2, or 4. " +
                        "This indicates corrupted corner density input.");
            }
        }

        private static List<List<int>> TraceLoops(List<FaceSegment> segments)
        {
            var neighbors = new Dictionary<int, List<int>>();
            void AddNeighbor(int from, int to)
            {
                if (!neighbors.TryGetValue(from, out List<int> list))
                {
                    list = new List<int>(2);
                    neighbors[from] = list;
                }
                list.Add(to);
            }

            foreach (FaceSegment segment in segments)
            {
                AddNeighbor(segment.EdgeA, segment.EdgeB);
                AddNeighbor(segment.EdgeB, segment.EdgeA);
            }

            foreach (KeyValuePair<int, List<int>> entry in neighbors)
            {
                if (entry.Value.Count != 2)
                {
                    throw new DomainException(
                        $"Edge {entry.Key} has degree {entry.Value.Count}, expected exactly 2. " +
                        "This is a bug in CollectFaceSegments, not a property of the input data.");
                }
            }

            var visited = new HashSet<int>();
            var loops = new List<List<int>>();

            foreach (int startEdge in neighbors.Keys)
            {
                if (visited.Contains(startEdge)) continue;

                var loop = new List<int> { startEdge };
                visited.Add(startEdge);

                int previous = -1;
                int current = startEdge;

                while (true)
                {
                    List<int> candidates = neighbors[current];
                    int next = candidates[0] == previous ? candidates[1] : candidates[0];

                    if (next == startEdge) break;

                    loop.Add(next);
                    visited.Add(next);
                    previous = current;
                    current = next;
                }

                loops.Add(loop);
            }

            return loops;
        }

        private static Vector3 InterpolateEdge(int edgeIndex, float[] cornerDensities, Vector3[] cornerPositions)
        {
            (int a, int b) = CubeTopology.EdgeCorners[edgeIndex];
            float da = cornerDensities[a];
            float db = cornerDensities[b];

            // Both endpoints must differ in sign for this edge to have been
            // selected as crossed; guard against the degenerate da == db case
            // (would only occur if both are exactly zero) by clamping t to the
            // midpoint rather than dividing by zero.
            float denominator = da - db;
            float t = Mathf.Approximately(denominator, 0f) ? 0.5f : da / denominator;
            t = Mathf.Clamp01(t);

            return Vector3.Lerp(cornerPositions[a], cornerPositions[b], t);
        }
    }
}
