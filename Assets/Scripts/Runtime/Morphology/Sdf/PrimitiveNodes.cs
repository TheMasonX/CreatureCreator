using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Morphology.Sdf
{
    /// <summary>
    /// Sphere of the given radius, centered at the local origin. Exact SDF.
    /// </summary>
    public sealed class SphereSdfNode : ISdfNode
    {
        private readonly float _radius;

        public SphereSdfNode(float radius)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
            {
                throw new DomainException($"Sphere radius must be finite and positive; got {radius}.");
            }
            _radius = radius;
        }

        public float Evaluate(Vector3 point) => point.magnitude - _radius;
    }

    /// <summary>
    /// Axis-aligned box centered at the local origin, given full half-extents on
    /// each axis. Exact SDF (Inigo Quilez's box distance function).
    /// </summary>
    public sealed class BoxSdfNode : ISdfNode
    {
        private readonly Vector3 _halfExtents;

        public BoxSdfNode(Vector3 halfExtents)
        {
            if (halfExtents.x <= 0f || halfExtents.y <= 0f || halfExtents.z <= 0f)
            {
                throw new DomainException($"Box half-extents must all be positive; got {halfExtents}.");
            }
            _halfExtents = halfExtents;
        }

        public float Evaluate(Vector3 point)
        {
            Vector3 q = new Vector3(
                Mathf.Abs(point.x) - _halfExtents.x,
                Mathf.Abs(point.y) - _halfExtents.y,
                Mathf.Abs(point.z) - _halfExtents.z);

            Vector3 qMax = new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f));
            float outside = qMax.magnitude;
            float inside = Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
            return outside + inside;
        }
    }

    /// <summary>
    /// Capsule aligned along the local Y axis, spanning from (0, -0.5, 0) to
    /// (0, 0.5, 0) before any TransformNode scale is applied — a caller wanting a
    /// longer capsule elongates it via the part's Transform.Scale.y rather than a
    /// second shape parameter (ShapeDefinition intentionally carries only
    /// PrimarySize; see ShapeDefinition.cs). Exact SDF for the unit-length capsule;
    /// TransformNode's non-uniform-scale approximation applies if elongated.
    /// </summary>
    public sealed class CapsuleSdfNode : ISdfNode
    {
        private static readonly Vector3 EndpointA = new Vector3(0f, -0.5f, 0f);
        private static readonly Vector3 EndpointB = new Vector3(0f, 0.5f, 0f);

        private readonly float _radius;

        public CapsuleSdfNode(float radius)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
            {
                throw new DomainException($"Capsule radius must be finite and positive; got {radius}.");
            }
            _radius = radius;
        }

        public float Evaluate(Vector3 point)
        {
            Vector3 pa = point - EndpointA;
            Vector3 ba = EndpointB - EndpointA;
            float t = Mathf.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));
            return (pa - ba * t).magnitude - _radius;
        }
    }

    /// <summary>
    /// MVP simplification, documented deliberately rather than silently: an exact
    /// per-axis ellipsoid distance field requires per-axis radii, which
    /// ShapeDefinition does not carry (it has one PrimarySize scalar, matching every
    /// other primitive — see ShapeDefinition.cs). This node is therefore a sphere of
    /// radius PrimarySize; per-axis elongation comes from the part's
    /// TransformNode.Scale exactly like Capsule/Box do, subject to the same
    /// non-uniform-scale approximation. If true per-axis-exact ellipsoids become a
    /// requirement, extend ShapeDefinition with a second/third radius parameter and
    /// implement Inigo Quilez's approximate (not exact — no closed-form exact
    /// solution exists) ellipsoid distance function here instead of delegating to
    /// SphereSdfNode.
    /// </summary>
    public sealed class EllipsoidSdfNode : ISdfNode
    {
        private readonly SphereSdfNode _inner;

        public EllipsoidSdfNode(float radius)
        {
            _inner = new SphereSdfNode(radius);
        }

        public float Evaluate(Vector3 point) => _inner.Evaluate(point);
    }
}
