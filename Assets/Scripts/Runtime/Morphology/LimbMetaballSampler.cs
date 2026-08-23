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

        public static List<LimbMetaball> Sample(LimbChain chain)
        {
            if (chain == null)
            {
                throw new DomainException("Cannot sample a null LimbChain.");
            }
            if (chain.Joints == null || chain.Joints.Count == 0)
            {
                throw new DomainException("Cannot sample a LimbChain with no joints.");
            }

            float totalLength = 0f;
            for (int i = 0; i < chain.Joints.Count - 1; i++)
            {
                totalLength += Vector3.Distance(chain.Joints[i].Position, chain.Joints[i + 1].Position);
            }

            var result = new List<LimbMetaball>();
            if (totalLength <= 1e-6f)
            {
                // Defensive degenerate guard (the validator rejects zero-length
                // chains; this keeps the generator total for direct calls).
                float radius = chain.Thickness == null ? 0f : chain.Thickness.Evaluate(0.5f);
                result.Add(new LimbMetaball(chain.Joints[0].Position, radius));
                return result;
            }

            ThicknessProfile thickness = chain.Thickness ?? ThicknessProfile.CreateDefault();
            float cumulative = 0f;

            for (int i = 0; i < chain.Joints.Count - 1; i++)
            {
                Vector3 start = chain.Joints[i].Position;
                Vector3 end = chain.Joints[i + 1].Position;
                float segmentLength = Vector3.Distance(start, end);

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
            result.Add(new LimbMetaball(
                chain.Joints[chain.Joints.Count - 1].Position,
                thickness.Evaluate(1f)));

            return result;
        }
    }
}
