using System;
using System.Collections.Generic;
using ProceduralCreature.Common;
using UnityEngine;

namespace ProceduralCreature.Definition
{
    [Serializable]
    public sealed class BodySample
    {
        public uint Id;
        public Vector3 Position;
        public float Radius;

        public BodySample Clone()
        {
            return new BodySample
            {
                Id = Id,
                Position = Position,
                Radius = Radius,
            };
        }
    }

    [Serializable]
    public sealed class BodySpline
    {
        public List<BodySample> Samples = new List<BodySample>();

        /// <summary>
        /// The Body's vertical-gradient color model (CC-025). Geometry (the
        /// spline samples) and appearance are both owned by the Body, but kept as
        /// separate fields so the pure geometry consumers (BodyFrameResolver,
        /// the SDF compiler) never touch color data. Defaults to a flat gray
        /// model, preserving the pre-gradient Body color behavior.
        /// </summary>
        public BodyVerticalGradientAppearance Appearance = BodyVerticalGradientAppearance.CreateDefault();

        public BodySpline Clone()
        {
            return new BodySpline
            {
                Appearance = Appearance == null ? null : Appearance.Clone(),
                Samples = CollectionCloneUtility.DeepClone(Samples, sample => sample.Clone()),
            };
        }
    }

    [Serializable]
    public sealed class BodySurfaceAnchor
    {
        public uint SegmentStartSampleId;
        public float SegmentT;
        public float RadialAngle;
        public float SurfaceOffset;
        public float Roll;

        public BodySurfaceAnchor Clone()
        {
            return new BodySurfaceAnchor
            {
                SegmentStartSampleId = SegmentStartSampleId,
                SegmentT = SegmentT,
                RadialAngle = RadialAngle,
                SurfaceOffset = SurfaceOffset,
                Roll = Roll,
            };
        }
    }
}