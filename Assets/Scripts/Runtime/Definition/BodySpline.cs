using System;
using System.Collections.Generic;
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

        public BodySpline Clone()
        {
            var clone = new BodySpline();
            foreach (BodySample sample in Samples)
            {
                clone.Samples.Add(sample == null ? null : sample.Clone());
            }
            return clone;
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