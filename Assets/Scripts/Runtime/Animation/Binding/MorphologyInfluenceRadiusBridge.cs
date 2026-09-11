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

            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);
            SkeletonModel skeleton = SkeletonInferrer.Infer(snapshot);
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
                    if (!skeleton.TryGetIndex(spec.Id, out int boneIndex)) continue;

                    float radius = NumericValidity.IsFinite(spec.Radius) && spec.Radius > 0f
                        ? spec.Radius
                        : ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius;

                    // A compact anatomical bone is a skinning proxy for the full
                    // morphology interval it represents. The radius therefore has to
                    // cover any centerline-to-chord deviation plus the local sample
                    // radius. Use the canonical interval regardless of whether this is
                    // a headward/spine or tailward bone.
                    if (spec.HasSegment)
                    {
                        radius = Mathf.Max(radius, ResolveBodyProxyRadius(spec, snapshot.Body, snapshot.Forward));
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

        private static float ResolveBodyProxyRadius(
            AnatomicalBodyRigLayout.BoneSpec bone,
            ResolvedBody body,
            Vector3 forward)
        {
            float radius = NumericValidity.IsFinite(bone.Radius) && bone.Radius > 0f
                ? bone.Radius
                : ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius;
            if (body.SamplePositions == null || body.SamplePositions.Count == 0)
            {
                return radius;
            }

            bool storedHeadToTail = IsStoredHeadToTail(body.SamplePositions, forward);
            float minT = Mathf.Min(bone.StartT, bone.EndT);
            float maxT = Mathf.Max(bone.StartT, bone.EndT);

            for (int i = 0; i < body.SamplePositions.Count; i++)
            {
                float storedT = body.NormalizedArcLengthAtSample != null
                    && i < body.NormalizedArcLengthAtSample.Count
                    ? body.NormalizedArcLengthAtSample[i]
                    : (body.SamplePositions.Count == 1 ? 0f : (float)i / (body.SamplePositions.Count - 1));
                float canonicalT = storedHeadToTail ? storedT : 1f - storedT;
                if (canonicalT < minT || canonicalT > maxT) continue;

                float distance = DistanceToSegment(body.SamplePositions[i], bone.Position, bone.EndPosition);
                float sampleRadius = body.SampleRadii != null && i < body.SampleRadii.Count
                    ? body.SampleRadii[i]
                    : radius;
                if (!NumericValidity.IsFinite(sampleRadius) || sampleRadius <= 0f)
                {
                    sampleRadius = radius;
                }

                float requiredRadius = distance + sampleRadius;
                if (NumericValidity.IsFinite(requiredRadius))
                {
                    radius = Mathf.Max(radius, requiredRadius);
                }
            }

            return Mathf.Max(0.001f, radius);
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared <= 1e-10f) return Vector3.Distance(point, a);

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSquared);
            return Vector3.Distance(point, a + ab * t);
        }

        private static bool IsStoredHeadToTail(IReadOnlyList<Vector3> positions, Vector3 forward)
        {
            if (positions == null || positions.Count < 2) return true;
            // Creature Forward points from tail toward head. Sample 0 is therefore
            // headward when its forward projection is greater than the last sample's.
            return Vector3.Dot(positions[0], forward)
                >= Vector3.Dot(positions[positions.Count - 1], forward);
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