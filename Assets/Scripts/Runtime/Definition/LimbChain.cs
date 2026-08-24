using System;
using System.Collections.Generic;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// The authored, authoritative description of a limb part (CC-018, ADR-001).
    /// A dedicated semantic model, NOT a reuse of <see cref="BodySpline"/>:
    /// joints are authored, intermediate geometry is derived, and the chain
    /// maps naturally to generated bones. Only thickness is authored; positions
    /// come from the joint chain, not free-form spline editing.
    ///
    /// Coordinate frame: joints live in the owning part's local morphology frame.
    /// <c>Joints[0] ≈ Vector3.zero</c> is the limb root (the part's placement
    /// frame is <see cref="CreaturePart.Transform"/>). The terminal joint
    /// (<c>Joints[N-1]</c>) is a stable semantic point that children (Foot,
    /// Hand, Claw, Decoration) attach to.
    ///
    /// The joint list order IS the semantic chain order. Derived metaballs and
    /// bones are never serialized as authoritative DNA — this class is the only
    /// limb state that survives serialization.
    /// </summary>
    [Serializable]
    public sealed class LimbChain
    {
        /// <summary>
        /// Default part-to-field union blend radius for a limb. Matches
        /// <see cref="ShapeDefinition.DefaultSphere"/>'s SmoothBlendRadius so
        /// existing and default limbs generate identically after the CC-049
        /// migration to an explicit limb blend.
        /// </summary>
        public const float DefaultBlendRadius = 0.1f;

        public List<LimbJoint> Joints = new List<LimbJoint>();

        /// <summary>1D thickness over normalized chain arc length (0 = root, 1 = tip).</summary>
        public ThicknessProfile Thickness = ThicknessProfile.CreateDefault();

        /// <summary>
        /// The blend radius used when uniting this limb's implicit surface into
        /// the creature field (CC-049). Shape.SmoothBlendRadius is inert for
        /// limb parts (ADR-001), so the limb carries its own explicit blend. A
        /// value of 0 is a hard union.
        /// </summary>
        public float BlendRadius = DefaultBlendRadius;

        /// <summary>
        /// A default straight limb: a root joint at the local origin and a single
        /// terminal joint extending along local -Y (down, matching how legs are
        /// typically authored in the editor's default pose). The default
        /// tapering thickness profile is used.
        /// </summary>
        public static LimbChain CreateDefault()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = UnityEngine.Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new UnityEngine.Vector3(0f, -1f, 0f) });
            return chain;
        }

        public LimbChain Clone()
        {
            var clone = new LimbChain
            {
                Thickness = Thickness == null ? null : Thickness.Clone(),
                BlendRadius = BlendRadius,
            };
            if (Joints != null)
            {
                foreach (LimbJoint joint in Joints)
                {
                    clone.Joints.Add(joint == null ? null : joint.Clone());
                }
            }
            return clone;
        }
    }
}
