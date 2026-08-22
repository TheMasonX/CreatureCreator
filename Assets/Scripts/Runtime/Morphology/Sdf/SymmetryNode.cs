using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Morphology.Sdf
{
    /// <summary>
    /// Wraps a single authored part's compiled node and unions it with its own
    /// mirror image across the creature-space X = 0 plane. This is the SDF-layer
    /// half of the symmetry decision recorded in SymmetryMode.cs (delta-audit item
    /// #2): the DNA holds exactly one CreaturePart with
    /// MirrorAcrossSymmetryPlane = true, and SymmetryNode is where the second
    /// (mirrored) copy actually comes into existence — as derived geometry, never
    /// written back to DNA.
    ///
    /// Uses a hard min (not a smooth blend) between the original and its mirror:
    /// two symmetric limbs meeting at the centerline should read as "the same limb,
    /// reflected," not "smoothly melted into each other," which a smooth-min would
    /// produce for any part whose bounds cross or approach the mirror plane.
    /// Smooth-blending mirrored pairs at the centerline (e.g. for a creature with
    /// no gap between symmetric halves) is a plausible future refinement, not an
    /// MVP requirement.
    /// </summary>
    public sealed class SymmetryNode : ISdfNode
    {
        private readonly ISdfNode _child;

        public SymmetryNode(ISdfNode child)
        {
            _child = child ?? throw new DomainException("SymmetryNode child must not be null.");
        }

        public float Evaluate(Vector3 point)
        {
            float original = _child.Evaluate(point);
            Vector3 mirrored = new Vector3(-point.x, point.y, point.z);
            float mirror = _child.Evaluate(mirrored);
            return Mathf.Min(original, mirror);
        }
    }
}
