namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Primitive SDF shape vocabulary. This is the DNA-level shape identifier; the
    /// portable SDF operations that interpret it belong to Phase 2 and must
    /// not be referenced from here (Definition code has no knowledge of SDF/mesh
    /// generation types — implementation guide §1.2 dependency rules).
    /// </summary>
    public enum ShapeType
    {
        Sphere = 0,
        Capsule = 1,
        Box = 2,
        Ellipsoid = 3,
    }
}
