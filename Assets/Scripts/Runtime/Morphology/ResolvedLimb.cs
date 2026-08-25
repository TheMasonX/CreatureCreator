using System;
using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Morphology
{
    /// <summary>
    /// The derived, immutable geometry guide for a <see cref="LimbChain"/>
    /// (CC-056A, increment A of the canonical resolved morphology layer).
    /// Resolves the authored chain once into joint positions, segment lengths,
    /// total length, and normalized arc lengths so every consumer — metaball
    /// sampling, skeleton inference, resolved-envelope validation, and later
    /// animation — interprets the chain identically instead of re-deriving it
    /// independently.
    ///
    /// Joints stay in the owning part's local morphology frame (the same frame
    /// <see cref="LimbChain.Joints"/> uses); creature-space placement is a
    /// separate concern owned by <see cref="CreaturePartWorldTransformResolver"/>
    /// (CC-051 / CC-056B).
    ///
    /// The centerline is the joint polyline (v1). CC-055 decides whether a future
    /// smooth centerline replaces it; until then the authored joints ARE the
    /// centerline and this type makes that explicit.
    ///
    /// Entirely derived state: never serialized and never written back into DNA
    /// (ADR-001 §5). <see cref="Resolve"/> is pure and deterministic, and the
    /// arrays it stores are private copies, so mutating the input chain after
    /// resolution cannot change this snapshot.
    /// </summary>
    public readonly struct ResolvedLimb
    {
        /// <summary>Joint positions in the part-local morphology frame.</summary>
        public readonly IReadOnlyList<Vector3> JointPositions;

        /// <summary>Length of each segment Joints[i] → Joints[i+1].</summary>
        public readonly IReadOnlyList<float> SegmentLengths;

        /// <summary>Total polyline length (sum of <see cref="SegmentLengths"/>).</summary>
        public readonly float TotalLength;

        /// <summary>
        /// Normalized cumulative arc length at each joint (0 = root, 1 = tip).
        /// A degenerate (zero-length) chain resolves every entry to 0.
        /// </summary>
        public readonly IReadOnlyList<float> NormalizedArcLengthAtJoint;

        /// <summary>
        /// The authored 1D thickness profile over normalized arc length.
        /// <see cref="Resolve"/> falls back to the default tapering profile when
        /// the chain has none, so this is never null after resolution.
        /// </summary>
        public readonly ThicknessProfile Thickness;

        private ResolvedLimb(IReadOnlyList<Vector3> jointPositions, IReadOnlyList<float> segmentLengths, float totalLength,
            IReadOnlyList<float> normalizedArcLengthAtJoint, ThicknessProfile thickness)
        {
            JointPositions = jointPositions;
            SegmentLengths = segmentLengths;
            TotalLength = totalLength;
            NormalizedArcLengthAtJoint = normalizedArcLengthAtJoint;
            Thickness = thickness;
        }

        /// <summary>The joint polyline (v1 centerline). Same values as <see cref="JointPositions"/>.</summary>
        public IReadOnlyList<Vector3> Centerline => JointPositions;

        /// <summary>The chain root socket: the first joint's local position (≈ the part origin).</summary>
        public Vector3 RootSocket => JointPositions[0];

        /// <summary>The chain terminal socket: the last joint's local position (children attach here).</summary>
        public Vector3 TerminalSocket => JointPositions[JointPositions.Count - 1];

        /// <summary>
        /// Resolves the authored <see cref="LimbChain"/> into a stable derived
        /// snapshot. Throws <see cref="DomainException"/> on a null chain, an
        /// empty joint list, or a null joint entry (the validator rejects these
        /// before generation; the guards keep direct calls total). The returned
        /// arrays are copies, so later mutation of the chain is invisible here.
        /// </summary>
        public static ResolvedLimb Resolve(LimbChain chain)
        {
            if (chain == null)
            {
                throw new DomainException("Cannot resolve a null LimbChain.");
            }
            if (chain.Joints == null || chain.Joints.Count == 0)
            {
                throw new DomainException("Cannot resolve a LimbChain with no joints.");
            }

            int count = chain.Joints.Count;
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                LimbJoint joint = chain.Joints[i];
                if (joint == null)
                {
                    throw new DomainException(
                        "LimbChain contains a null joint; validation should have rejected it.");
                }
                positions[i] = joint.Position;
            }

            int segmentCount = count - 1;
            var segmentLengths = new float[Math.Max(segmentCount, 0)];
            float totalLength = 0f;
            for (int i = 0; i < segmentCount; i++)
            {
                segmentLengths[i] = Vector3.Distance(positions[i], positions[i + 1]);
                totalLength += segmentLengths[i];
            }

            var normalizedArcLength = new float[count];
            if (totalLength <= 1e-6f)
            {
                // Degenerate (coincident/zero-length) chain: every joint sits at t = 0.
                for (int i = 0; i < count; i++) normalizedArcLength[i] = 0f;
            }
            else
            {
                float cumulative = 0f;
                normalizedArcLength[0] = 0f;
                for (int i = 0; i < segmentCount; i++)
                {
                    cumulative += segmentLengths[i];
                    normalizedArcLength[i + 1] = cumulative / totalLength;
                }
                // Pin the terminal to exactly 1 (defensive against float accumulation).
                normalizedArcLength[count - 1] = 1f;
            }

            ThicknessProfile thickness = chain.Thickness == null
                ? ThicknessProfile.CreateDefault()
                : chain.Thickness.Clone();

            return new ResolvedLimb(
                Array.AsReadOnly(positions),
                Array.AsReadOnly(segmentLengths),
                totalLength,
                Array.AsReadOnly(normalizedArcLength),
                thickness);
        }
    }
}
