namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Classifies how a part's geometry is produced (CC-031). Kept semantic: a part
    /// declares one geometry source, while <see cref="PartType"/> stays a separate
    /// semantic classification (Eye, Limb, ...) and is never turned into a geometry
    /// taxonomy (EyeMesh/EyeSdf/...).
    /// </summary>
    public enum GeometryType
    {
        /// <summary>Surface derived from the implicit SDF field (Body + Shape/Limb parts).</summary>
        Implicit = 0,

        /// <summary>Pre-authored mesh asset referenced by stable key.</summary>
        MeshAsset = 1,

        /// <summary>Mesh produced by a procedural MeshGenerator (not yet implemented).</summary>
        Procedural = 2,
    }
}
