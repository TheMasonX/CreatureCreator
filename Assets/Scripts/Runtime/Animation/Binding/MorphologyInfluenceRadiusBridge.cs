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
    /// Converts resolved morphology into the per-bone radius data consumed by
    /// welded-surface skinning. Body radii come from the compact anatomical rig
    /// layout; limb radii come from the authored limb thickness profile.
    /// </summary>
    public static class MorphologyInfluenceRadiusBridge
    {
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

        public static float[] BuildRadiiByBoneIndex(
            SkeletonSnapshot skeleton,
            ResolvedCreatureSnapshot snapshot)
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

            // Body samples no longer map one-to-one to bones. The compact layout is
            // the authoritative Body rig/radius representation shared by inference,
            // attachment mapping, and skin-binding preparation.
            if (snapshot.HasBody)
            {
                IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bodyBones =
                    AnatomicalBodyRigLayout.Build(snapshot);
                for (int i = 0; i < bodyBones.Count; i++)
                {
                    AnatomicalBodyRigLayout.BoneSpec spec = bodyBones[i];
                    if (skeleton.TryGetIndex(spec.Id, out int boneIndex))
                    {
                        result[boneIndex] = NumericValidity.IsFinite(spec.Radius) && spec.Radius > 0f
                            ? spec.Radius
                            : ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius;
                    }
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
