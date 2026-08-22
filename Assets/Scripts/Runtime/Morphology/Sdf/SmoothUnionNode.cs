using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Morphology.Sdf
{
    /// <summary>
    /// Combines two child nodes with a smooth minimum. N-ary blending (more than
    /// two parts meeting at a junction) is built by chaining these binary nodes
    /// (see SdfProgramBuilder) rather than implementing a separate N-ary node —
    /// simpler, and the chain order is fixed deterministically by part Id, so the
    /// result is reproducible even though the polynomial smooth-min is not
    /// perfectly associative (chaining (a smin b) smin c can differ very slightly
    /// from a smin (b smin c) at three-way junctions). This is an accepted MVP
    /// approximation — see SdfProgramBuilder's ordering-rule documentation.
    /// </summary>
    public sealed class SmoothUnionNode : ISdfNode
    {
        private readonly ISdfNode _a;
        private readonly ISdfNode _b;
        private readonly float _blendRadius;

        public SmoothUnionNode(ISdfNode a, ISdfNode b, float blendRadius)
        {
            _a = a ?? throw new DomainException("SmoothUnionNode child 'a' must not be null.");
            _b = b ?? throw new DomainException("SmoothUnionNode child 'b' must not be null.");

            if (float.IsNaN(blendRadius) || float.IsInfinity(blendRadius))
            {
                throw new DomainException($"SmoothUnionNode blendRadius must be finite; got {blendRadius}.");
            }

            _blendRadius = blendRadius;
        }

        public float Evaluate(Vector3 point)
        {
            float da = _a.Evaluate(point);
            float db = _b.Evaluate(point);
            return SmoothMinMath.SmoothMin(da, db, _blendRadius);
        }
    }
}
