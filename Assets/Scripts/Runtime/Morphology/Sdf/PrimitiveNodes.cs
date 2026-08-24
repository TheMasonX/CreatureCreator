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
    /// Capsule with an authored axis, radius, and total segment length.
    /// </summary>
    public sealed class CapsuleSdfNode : ISdfNode
    {
        private readonly float _radius;
        private readonly float _height;
        private readonly Definition.ShapeAxis _axis;

        public CapsuleSdfNode(float radius, float height = 1f, Definition.ShapeAxis axis = Definition.ShapeAxis.Y)
        {
            if (radius <= 0f || height <= 0f || float.IsNaN(radius) || float.IsInfinity(radius)
                || float.IsNaN(height) || float.IsInfinity(height))
            {
                throw new DomainException($"Capsule radius and height must be finite and positive; got {radius}, {height}.");
            }
            _radius = radius;
            _height = height;
            _axis = axis;
        }

        public float Evaluate(Vector3 point)
        {
            Vector3 axisPoint = _axis == Definition.ShapeAxis.X
                ? new Vector3(point.y, point.x, point.z)
                : _axis == Definition.ShapeAxis.Z
                    ? new Vector3(point.x, point.z, point.y)
                    : point;
            Vector3 endpoint = new Vector3(0f, _height * 0.5f, 0f);
            Vector3 pa = axisPoint - new Vector3(0f, -_height * 0.5f, 0f);
            Vector3 ba = endpoint - new Vector3(0f, -_height * 0.5f, 0f);
            float t = Mathf.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));
            return (pa - ba * t).magnitude - _radius;
        }
    }

    /// <summary>
    /// An ellipsoid distance approximation using the standard scaled-sphere form.
    /// It supports independently authored radii while remaining inexpensive enough
    /// for repeated SDF sampling.
    /// </summary>
    public sealed class EllipsoidSdfNode : ISdfNode
    {
        private readonly Vector3 _radii;

        public EllipsoidSdfNode(Vector3 radii)
        {
            if (radii.x <= 0f || radii.y <= 0f || radii.z <= 0f
                || float.IsNaN(radii.x) || float.IsNaN(radii.y) || float.IsNaN(radii.z)
                || float.IsInfinity(radii.x) || float.IsInfinity(radii.y) || float.IsInfinity(radii.z))
            {
                throw new DomainException($"Ellipsoid radii must be finite and positive; got {radii}.");
            }
            _radii = radii;
        }

        public float Evaluate(Vector3 point)
        {
            Vector3 normalized = new Vector3(point.x / _radii.x, point.y / _radii.y, point.z / _radii.z);
            Vector3 gradient = new Vector3(point.x / (_radii.x * _radii.x), point.y / (_radii.y * _radii.y), point.z / (_radii.z * _radii.z));
            float denominator = gradient.magnitude;
            if (denominator <= Mathf.Epsilon) return -Mathf.Min(_radii.x, Mathf.Min(_radii.y, _radii.z));
            return (normalized.magnitude - 1f) / denominator;
        }
    }
}
