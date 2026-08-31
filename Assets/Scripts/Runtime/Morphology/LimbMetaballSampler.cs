using System;
using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Morphology
{
    /// <summary>
    /// One derived metaball for a limb chain (CC-018). Positions are in the limb's
    /// local morphology frame; the SDF compiler wraps them with the part's
    /// creature-space transform. Entirely derived state — never serialized as
    /// authoritative DNA (ADR-001 §5).
    /// </summary>
    public readonly struct LimbMetaball
    {
        public readonly Vector3 Position;
        public readonly float Radius;

        public LimbMetaball(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// Derives the between-joint metaball sequence for a <see cref="LimbChain"/>
    /// (CC-018 Phase 4). Metaball sampling is a GEOMETRY concern, not a limb
    /// authoring concern: the author defines joints + a 1D thickness profile, and
    /// this generator owns how densely the chain is sampled. Sampling density can
    /// change without changing limb DNA.
    ///
    /// Per segment <c>J[i] → J[i+1]</c>, the sample count is
    /// <c>max(1, ceil(segmentLength / DesiredSampleSpacing))</c>, giving an
    /// inter-ball spacing of at most <see cref="DesiredSampleSpacing"/>. Each ball
    /// samples <c>radius = Thickness.Evaluate(t)</c> where t is the normalized
    /// cumulative chain arc length (0 = root, 1 = tip). Deterministic and pure —
    /// no UnityEditor, no SDF dependency.
    /// </summary>
    public static class LimbMetaballSampler
    {
        /// <summary>
        /// Desired center-to-center spacing of derived metaballs. The generator
        /// derives the exact count from segment length; this is a fidelity knob,
        /// not an authored value.
        /// </summary>
        public const float DesiredSampleSpacing = 0.1f;

        /// <summary>
        /// Samples a derived <see cref="ResolvedLimb"/> (CC-056A). Segment
        /// lengths, total length, and arc length come from the resolved model
        /// instead of being re-derived here; this generator owns only the
        /// fidelity knob (per-segment ball count). Pure and deterministic, and
        /// bit-identical to sampling the source chain directly.
        /// </summary>
        public static List<LimbMetaball> Sample(ResolvedLimb limb)
        {
            if (limb.JointPositions == null || limb.JointPositions.Count == 0)
            {
                throw new DomainException("Cannot sample a ResolvedLimb with no joints.");
            }

            var result = new List<LimbMetaball>();
            if (limb.TotalLength <= 1e-6f)
            {
                // Defensive degenerate guard (the validator rejects zero-length
                // chains; this keeps the generator total for direct calls).
                float radius = limb.Thickness == null ? 0f : limb.Thickness.Evaluate(0.5f);
                result.Add(new LimbMetaball(limb.RootSocket, radius));
                return result;
            }

            ThicknessProfile thickness = limb.Thickness ?? ThicknessProfile.CreateDefault();
            float totalLength = limb.TotalLength;
            float cumulative = 0f;

            for (int i = 0; i < limb.JointPositions.Count - 1; i++)
            {
                Vector3 start = limb.JointPositions[i];
                Vector3 end = limb.JointPositions[i + 1];
                float segmentLength = limb.SegmentLengths[i];

                int count = Mathf.Max(1, Mathf.CeilToInt(segmentLength / DesiredSampleSpacing));
                for (int k = 0; k < count; k++)
                {
                    float fraction = count > 1 ? (float)k / count : 0f;
                    Vector3 position = Vector3.Lerp(start, end, fraction);
                    float t = (cumulative + segmentLength * fraction) / totalLength;
                    result.Add(new LimbMetaball(position, thickness.Evaluate(t)));
                }
                cumulative += segmentLength;
            }

            // The terminal joint (t = 1) is a stable semantic point; ensure a
            // metaball sits there so the tip is closed and children attach to a
            // real surface.
            result.Add(new LimbMetaball(limb.TerminalSocket, thickness.Evaluate(1f)));

            return result;
        }
    }
}
