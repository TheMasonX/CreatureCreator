using System;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Semantic placement of a geometry item relative to its owning part (CC-031).
    /// The mesh is not authoritative for placement: changing topology or resolution
    /// must not lose attachment intent, so intent lives here in DNA.
    ///
    /// Pass 1 carries only Offset / Orientation / Scale in the part's local frame.
    /// The body-surface anchor is deliberately deferred — pass 1 places mesh
    /// geometry at the part's local-space position (ADR-002 §2).
    /// </summary>
    [Serializable]
    public sealed class GeometryAttachment
    {
        public UnityEngine.Vector3 Offset = UnityEngine.Vector3.zero;
        public UnityEngine.Quaternion Orientation = UnityEngine.Quaternion.identity;
        public UnityEngine.Vector3 Scale = UnityEngine.Vector3.one;

        public bool IsFinite()
        {
            return IsFiniteVector(Offset)
                && IsFiniteVector(Scale)
                && IsFinite(Orientation.x) && IsFinite(Orientation.y)
                && IsFinite(Orientation.z) && IsFinite(Orientation.w);
        }

        private static bool IsFiniteVector(UnityEngine.Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        private static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        public GeometryAttachment Clone()
        {
            return new GeometryAttachment
            {
                Offset = Offset,
                Orientation = Orientation,
                Scale = Scale,
            };
        }
    }
}
