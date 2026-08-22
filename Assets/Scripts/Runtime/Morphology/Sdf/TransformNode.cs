using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Morphology.Sdf
{
    /// <summary>
    /// Wraps a child node with an affine transform, evaluating the child in its own
    /// local space by inverse-transforming the query point (design doc §16:
    /// "Implement transform handling in SDF space without mutating source
    /// definition data" — the child primitive never knows it has been placed
    /// anywhere).
    ///
    /// EXACTNESS: rotation and translation are isometries, so distance is exact
    /// under those alone. Uniform scale is also exact (local distance × scale
    /// factor = world distance). <b>Non-uniform scale is an approximation</b>: true
    /// non-uniform scaling of a distance field does not itself produce a valid
    /// distance field (the gradient magnitude stops being 1 everywhere), so this
    /// node scales the local-space distance by the minimum absolute scale
    /// component — a conservative choice that never overestimates distance (safe
    /// for Marching Cubes' surface-crossing test) at the cost of some accuracy in
    /// the interior far from the surface. This is a known, deliberate MVP
    /// simplification (delta-audit-adjacent: flagged here rather than discovered
    /// mid-debugging a warped mesh).
    /// </summary>
    public sealed class TransformNode : ISdfNode
    {
        private readonly ISdfNode _child;
        private readonly Matrix4x4 _worldToLocal;
        private readonly float _distanceScale;

        public TransformNode(ISdfNode child, Matrix4x4 localToWorld)
        {
            _child = child ?? throw new DomainException("TransformNode child must not be null.");

            _worldToLocal = localToWorld.inverse;

            Vector3 scale = localToWorld.lossyScale;
            float minAbsScale = Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            if (minAbsScale <= 0f || float.IsNaN(minAbsScale) || float.IsInfinity(minAbsScale))
            {
                throw new DomainException(
                    $"TransformNode received a degenerate scale {scale}; validate the definition " +
                    "before compiling (see DefinitionValidator's InvalidScale check).");
            }

            _distanceScale = minAbsScale;
        }

        public float Evaluate(Vector3 point)
        {
            Vector3 local = _worldToLocal.MultiplyPoint3x4(point);
            float localDistance = _child.Evaluate(local);
            return localDistance * _distanceScale;
        }
    }
}
