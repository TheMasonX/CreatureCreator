using UnityEngine;

namespace ProceduralCreature.Morphology.Sdf
{
    /// <summary>
    /// Represents "no geometry" — every point is outside, at effectively infinite
    /// distance. Returned by SdfProgramBuilder for a zero-part CreatureDefinition
    /// (implementation guide §10.3: "Exercise empty/degenerate creatures") instead
    /// of throwing, since an empty creature is a valid (if useless) definition, not
    /// a data error. Marching Cubes sampling this field will correctly find no
    /// sign changes anywhere and produce zero triangles.
    /// </summary>
    public sealed class EmptySdfNode : ISdfNode
    {
        public float Evaluate(Vector3 point) => float.PositiveInfinity;
    }
}
