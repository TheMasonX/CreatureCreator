using System.Collections.Generic;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;
using ProceduralCreature.Skeleton;
using UnityEngine;
using SkeletonModel = ProceduralCreature.Skeleton.Skeleton;

namespace ProceduralCreature.Animation.Binding
{
    /// <summary>
    /// Converts the resolved morphology model into the per-bone radius array the
    /// welded-surface skinning adapter consumes. This is the adapter/runtime bridge
    /// between the authoritative Body/limb morphology and the pure weight-authoring
    /// core: the authoring core still takes a plain <c>radiiByBoneIndex</c> array
    /// and falls back to <see cref="ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius"/>
    /// when a bone has no morphology radius. This bridge owns only mapping from the
    /// resolved body/limb snapshot into that array; it never mutates or re-derives
    /// DNA.
    /// </summary>
    public static class MorphologyInfluenceRadiusBridge
    {
        /// <summary>
        /// Builds a per-bone radius array for the given resolved creature. Missing
        /// body/limb entries and non-finite values fall back deterministically to the
        /// default influence radius so the authoring core remains total and stable.
        /// </summary>
        public static float[] BuildRadiiByBoneIndex(CreatureDefinition definition)
        {
            if (definition == null)
            {
                throw new DomainException("definition must not be null.");
            }

            SkeletonModel skeleton = SkeletonInferrer.Infer(definition);
            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);
            return BuildRadiiByBoneIndex(SkeletonSnapshot.Capture(skeleton), snapshot);
        }

        /// <summary>
        /// Builds a radius array in the exact bone ordering of <paramref name="skeleton"/>,
        /// reading radius values from the resolved Body and limb morphology that generated
        /// the welded surface. Bones with no resolved morphology radius keep the default
        /// influence radius (0.5), which is the deterministic fallback contract shared by
        /// <see cref="ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences"/>.
        /// </summary>
        public static float[] BuildRadiiByBoneIndex(SkeletonSnapshot skeleton, ResolvedCreatureSnapshot snapshot)
        {
            if (skeleton == null)
            {
                throw new DomainException("skeleton must not be null.");
            }
            if (snapshot == null)
            {
                throw new DomainException("snapshot must not be null.");
            }

            var result = new float[skeleton.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius;
            }

            if (snapshot.HasBody)
            {
                ResolvedBody body = snapshot.Body;
                for (int i = 0; i < body.SampleIds.Count; i++)
                {
                    string boneId = SemanticBoneResolver.ResolveBodySocketBoneId(body.SampleIds[i]);
                    if (!skeleton.TryGetIndex(boneId, out int boneIndex))
                    {
                        continue;
                    }

                    float radius = body.SampleRadii[i];
                    if (!NumericValidity.IsFinite(radius) || radius <= 0f)
                    {
                        radius = ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius;
                    }
                    result[boneIndex] = radius;
                }
            }

            foreach (ResolvedPartSnapshot part in snapshot.PartsById.Values)
            {
                if (!part.HasLimb) continue;

                ResolvedLimb limb = part.Limb;
                if (limb.JointPositions == null || limb.JointPositions.Count < 2) continue;

                for (int segmentIndex = 0; segmentIndex < limb.JointPositions.Count - 1; segmentIndex++)
                {
                    float radius = ResolveSegmentRadius(limb, segmentIndex);
                    string segmentBoneId = SemanticBoneResolver.ResolveLimbSegmentBoneId(
                        part.Id, segmentIndex, mirrored: false);
                    if (skeleton.TryGetIndex(segmentBoneId, out int localIndex))
                    {
                        result[localIndex] = radius;
                    }

                    string mirroredBoneId = SemanticBoneResolver.ResolveLimbSegmentBoneId(
                        part.Id, segmentIndex, mirrored: true);
                    if (skeleton.TryGetIndex(mirroredBoneId, out int mirroredIndex))
                    {
                        result[mirroredIndex] = radius;
                    }
                }
            }

            return result;
        }

        private static float ResolveSegmentRadius(ResolvedLimb limb, int segmentIndex)
        {
            if (limb.Thickness == null || limb.JointPositions == null || limb.JointPositions.Count < 2)
            {
                return ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius;
            }

            if (segmentIndex < 0 || segmentIndex >= limb.JointPositions.Count - 1)
            {
                return ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius;
            }

            float segmentStartT = segmentIndex < limb.NormalizedArcLengthAtJoint.Count
                ? limb.NormalizedArcLengthAtJoint[segmentIndex]
                : 0f;
            float segmentEndT = segmentIndex + 1 < limb.NormalizedArcLengthAtJoint.Count
                ? limb.NormalizedArcLengthAtJoint[segmentIndex + 1]
                : 1f;
            float sampleT = Mathf.Clamp01((segmentStartT + segmentEndT) * 0.5f);

            float radius = limb.Thickness.Evaluate(sampleT);
            if (!NumericValidity.IsFinite(radius) || radius <= 0f)
            {
                return ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius;
            }

            return radius;
        }
    }
}
