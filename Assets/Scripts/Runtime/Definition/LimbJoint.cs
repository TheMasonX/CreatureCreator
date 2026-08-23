using System;
using UnityEngine;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// One authored joint in a <see cref="LimbChain"/>. The chain is an
    /// articulated semantic structure: joints are authored, intermediate
    /// geometry is derived (ADR-001). List order is the semantic chain order;
    /// joint IDs are stable and unique (mirrors the Body spline's ID rule).
    /// Positions are in the owning part's local morphology frame, so
    /// <c>Joints[0] ≈ Vector3.zero</c> is the limb root (ADR-001 §3).
    /// </summary>
    [Serializable]
    public sealed class LimbJoint
    {
        /// <summary>Stable identifier. Never derived from list position.</summary>
        public uint Id;

        /// <summary>Local position inside the part's morphology frame.</summary>
        public Vector3 Position;

        public LimbJoint Clone()
        {
            return new LimbJoint
            {
                Id = Id,
                Position = Position,
            };
        }
    }
}
