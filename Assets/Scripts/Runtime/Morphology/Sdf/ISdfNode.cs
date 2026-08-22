using UnityEngine;

namespace ProceduralCreature.Morphology.Sdf
{
    /// <summary>
    /// A node in the SDF composition graph. Every implementation must be a pure
    /// function of its inputs — no mutable state, no reference back to
    /// CreatureDefinition/CreaturePart, no Unity scene ownership (implementation
    /// guide §1.2: mesh/generation types must not reference back to Definition
    /// types; this graph is a one-way derived structure).
    ///
    /// SIGN CONVENTION (fixed for the whole project, per design doc §16 "Define SDF
    /// sign convention explicitly"): <b>negative = inside the surface, zero = on the
    /// surface, positive = outside the surface.</b> Every node — primitives,
    /// TransformNode, SmoothUnionNode, SymmetryNode — must preserve this convention.
    /// Marching Cubes (Phase 3) depends on this being consistent everywhere; a node
    /// that inverts the sign silently breaks extraction for every creature that
    /// touches it, not just itself.
    ///
    /// COMPOSITION RULE: nodes compose by wrapping, not by mutating a shared field —
    /// SmoothUnionNode holds references to its two children and computes from them
    /// on every Evaluate call. This keeps the graph trivially rebuildable and
    /// side-effect-free, which is what makes "recompile the whole tree on every
    /// definition change" (design doc §8, conservative regeneration) cheap to reason
    /// about even though it is not cheap to execute.
    /// </summary>
    public interface ISdfNode
    {
        /// <summary>
        /// Evaluates the signed distance from <paramref name="point"/> (in the same
        /// space this node was constructed to operate in — typically creature-root
        /// space for the root of a compiled tree) to the node's surface.
        /// </summary>
        float Evaluate(Vector3 point);
    }
}
