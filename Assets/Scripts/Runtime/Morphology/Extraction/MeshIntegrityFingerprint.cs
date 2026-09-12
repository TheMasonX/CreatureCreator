using System;

namespace ProceduralCreature.Morphology.Extraction
{
    /// <summary>
    /// Stable debug/test fingerprint for generated extraction data. This is deliberately
    /// independent of object identity, hash-code randomization, and Unity Mesh state.
    /// It hashes exact IEEE-754 float bit patterns plus triangle indices, so repeated
    /// generation can distinguish true data drift from timing noise.
    /// </summary>
    public static class MeshIntegrityFingerprint
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static string ForDensityGrid(DensityGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            ulong hash = OffsetBasis;
            Add(ref hash, grid.CellsX);
            Add(ref hash, grid.CellsY);
            Add(ref hash, grid.CellsZ);
            Add(ref hash, grid.Origin.x);
            Add(ref hash, grid.Origin.y);
            Add(ref hash, grid.Origin.z);
            Add(ref hash, grid.CellSize);

            for (int z = 0; z <= grid.CellsZ; z++)
            for (int y = 0; y <= grid.CellsY; y++)
            for (int x = 0; x <= grid.CellsX; x++)
                Add(ref hash, grid.GetSample(x, y, z));

            return hash.ToString("X16");
        }

        public static string ForMesh(MeshExtractionResult mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));

            ulong hash = OffsetBasis;
            Add(ref hash, mesh.Positions.Count);
            Add(ref hash, mesh.Triangles.Count);
            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                Add(ref hash, mesh.Positions[i].x);
                Add(ref hash, mesh.Positions[i].y);
                Add(ref hash, mesh.Positions[i].z);
            }
            for (int i = 0; i < mesh.Triangles.Count; i++) Add(ref hash, mesh.Triangles[i]);
            return hash.ToString("X16");
        }

        private static void Add(ref ulong hash, int value)
        {
            unchecked
            {
                uint bits = (uint)value;
                hash ^= (byte)bits; hash *= Prime;
                hash ^= (byte)(bits >> 8); hash *= Prime;
                hash ^= (byte)(bits >> 16); hash *= Prime;
                hash ^= (byte)(bits >> 24); hash *= Prime;
            }
        }

        private static void Add(ref ulong hash, float value)
        {
            Add(ref hash, BitConverter.SingleToInt32Bits(value));
        }
    }
}
