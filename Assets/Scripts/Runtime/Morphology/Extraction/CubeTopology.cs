using UnityEngine;

namespace ProceduralCreature.Morphology.Extraction
{
    /// <summary>
    /// Fixed cube topology used by the Marching-Cubes-with-Asymptotic-Decider
    /// extractor. Every value here is derived from a single self-consistent
    /// convention (bit-interleaved corner numbering) rather than transcribed from
    /// an external reference table — see the class-level derivation notes below.
    /// This is deliberate: a large externally-sourced lookup table can't be
    /// compile-verified in this environment, so the extraction algorithm (see
    /// CubeContourResolver) is built to need only this small, individually
    /// reasoned-through data instead of a 256-row triangulation table.
    ///
    /// CORNER NUMBERING: corner i (0-7) sits at local position
    /// (i&amp;1, (i>>1)&amp;1, (i>>2)&amp;1) — i.e. bit 0 = x, bit 1 = y, bit 2 = z.
    ///
    /// EDGES: the 12 cube edges, each connecting two corners that differ in
    /// exactly one bit (four edges per axis direction).
    ///
    /// FACES: the 6 cube faces, each listing its 4 corners in CYCLIC order
    /// around the face quad (corner i, i+1, i+2, i+3 form a closed loop — this
    /// ordering is what makes "diagonal" vs "adjacent" corner pairs on a face
    /// well-defined for the ambiguity test in AsymptoticDecider) together with
    /// the 4 face edges in the matching cyclic order (FaceEdges[f][k] connects
    /// FaceCorners[f][k] to FaceCorners[f][(k+1)%4]).
    /// </summary>
    public static class CubeTopology
    {
        public static readonly Vector3[] CornerOffsets =
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0),
            new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1),
        };

        /// <summary>Grid-space integer offsets matching CornerOffsets, for indexing DensityGrid.</summary>
        public static readonly Vector3Int[] CornerGridOffsets =
        {
            new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1), new Vector3Int(0, 1, 1), new Vector3Int(1, 1, 1),
        };

        /// <summary>Each entry is (cornerA, cornerB) for edge index 0-11.</summary>
        public static readonly (int A, int B)[] EdgeCorners =
        {
            (0, 1), (2, 3), (4, 5), (6, 7), // along X
            (0, 2), (1, 3), (4, 6), (5, 7), // along Y
            (0, 4), (1, 5), (2, 6), (3, 7), // along Z
        };

        /// <summary>Corners of each of the 6 faces, in cyclic order. Index = face enum value.</summary>
        public static readonly int[][] FaceCorners =
        {
            new[] { 0, 2, 6, 4 }, // NegX (x=0)
            new[] { 1, 3, 7, 5 }, // PosX (x=1)
            new[] { 0, 1, 5, 4 }, // NegY (y=0)
            new[] { 2, 3, 7, 6 }, // PosY (y=1)
            new[] { 0, 1, 3, 2 }, // NegZ (z=0)
            new[] { 4, 5, 7, 6 }, // PosZ (z=1)
        };

        /// <summary>
        /// Edge indices for each face in cyclic order: FaceEdges[f][k] is the edge
        /// connecting FaceCorners[f][k] and FaceCorners[f][(k+1)%4]. Derived by hand
        /// from FaceCorners + EdgeCorners; see CubeTopologyTests for the
        /// self-consistency checks (every edge appears in exactly 2 faces; every
        /// face edge matches a real EdgeCorners entry) that catch a transcription
        /// mistake here.
        /// </summary>
        public static readonly int[][] FaceEdges =
        {
            new[] { 4, 10, 6, 8 },  // NegX: (0,2)=4 (2,6)=10 (6,4)=6 (4,0)=8
            new[] { 5, 11, 7, 9 },  // PosX: (1,3)=5 (3,7)=11 (7,5)=7 (5,1)=9
            new[] { 0, 9, 2, 8 },   // NegY: (0,1)=0 (1,5)=9 (5,4)=2 (4,0)=8
            new[] { 1, 11, 3, 10 }, // PosY: (2,3)=1 (3,7)=11 (7,6)=3 (6,2)=10
            new[] { 0, 5, 1, 4 },   // NegZ: (0,1)=0 (1,3)=5 (3,2)=1 (2,0)=4
            new[] { 2, 7, 3, 6 },   // PosZ: (4,5)=2 (5,7)=7 (7,6)=3 (6,4)=6
        };

        public enum Face
        {
            NegX = 0,
            PosX = 1,
            NegY = 2,
            PosY = 3,
            NegZ = 4,
            PosZ = 5,
        }

        public static readonly Face[] AllFaces =
        {
            Face.NegX, Face.PosX, Face.NegY, Face.PosY, Face.NegZ, Face.PosZ,
        };
    }
}
