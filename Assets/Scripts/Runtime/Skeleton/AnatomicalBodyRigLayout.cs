using System;
using System.Collections.Generic;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;
using UnityEngine;

namespace ProceduralCreature.Skeleton
{
    /// <summary>
    /// Derives a compact anatomical rig layout from the dense resolved Body spline.
    /// Body samples remain a morphology/SDF representation; they are no longer
    /// themselves bones. The layout is deterministic, density-independent, and
    /// uses load-bearing limb attachment evidence to place the pelvis when it is
    /// available.
    ///
    /// Stored Body sample order is not assumed to be head-to-tail. The layout first
    /// determines the anatomical head/tail direction from the creature Forward axis
    /// and then evaluates the polyline in canonical head->tail arc space.
    /// </summary>
    public static class AnatomicalBodyRigLayout
    {
        public const string PelvisBoneId = "body_pelvis";
        public const string SpineBoneId = "body_spine";
        public const string HeadBoneId = "body_head";
        public const string TailBoneId = "body_tail";

        public readonly struct BoneSpec
        {
            public readonly string Id;
            public readonly string ParentBoneId;
            public readonly Vector3 Position;
            public readonly Vector3 EndPosition;
            public readonly bool HasSegment;
            public readonly Quaternion Rotation;
            public readonly float StartT;
            public readonly float EndT;
            public readonly float Radius;

            internal BoneSpec(string id, string parentBoneId, Vector3 position, Vector3 endPosition,
                bool hasSegment, Quaternion rotation, float startT, float endT, float radius)
            {
                Id = id;
                ParentBoneId = parentBoneId;
                Position = position;
                EndPosition = endPosition;
                HasSegment = hasSegment;
                Rotation = rotation;
                StartT = startT;
                EndT = endT;
                Radius = radius;
            }
        }

        /// <summary>Builds the compact body rig using resolved limb attachment evidence.</summary>
        public static IReadOnlyList<BoneSpec> Build(ResolvedCreatureSnapshot snapshot)
        {
            if (snapshot == null) throw new DomainException("snapshot must not be null.");
            if (!snapshot.HasBody) return Array.Empty<BoneSpec>();
            return Build(snapshot.Body, snapshot.Forward, snapshot.PartsById.Values);
        }

        /// <summary>Builds a compact body rig without part evidence; used by defensive paths and tests.</summary>
        public static IReadOnlyList<BoneSpec> Build(ResolvedBody body, Vector3 forward)
        {
            return Build(body, forward, null);
        }

        private static IReadOnlyList<BoneSpec> Build(
            ResolvedBody body,
            Vector3 forward,
            IEnumerable<ResolvedPartSnapshot> resolvedParts)
        {
            if (body.SamplePositions == null || body.SamplePositions.Count == 0)
            {
                return Array.Empty<BoneSpec>();
            }

            Vector3 axis = forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.forward;
            bool storedHeadToTail = IsStoredHeadToTail(body.SamplePositions, axis);

            var legTs = new List<float>();
            var armTs = new List<float>();
            if (resolvedParts != null)
            {
                foreach (ResolvedPartSnapshot part in resolvedParts)
                {
                    if (!part.HasLimb || part.ParentId != CreatureDefinition.BodyId) continue;
                    if (part.Limb.JointPositions == null || part.Limb.JointPositions.Count == 0) continue;

                    Vector3 root = part.PartFrameToCreatureSpace.MultiplyPoint3x4(part.Limb.RootSocket);
                    float t = CanonicalArcTAtPoint(body, axis, storedHeadToTail, root);
                    if (part.PartType == PartType.Leg)
                    {
                        legTs.Add(t);
                    }
                    else if (part.PartType == PartType.Arm)
                    {
                        armTs.Add(t);
                    }
                }
            }

            float pelvisT = DeterminePelvisT(legTs, armTs);
            float chestT = DetermineChestT(pelvisT, legTs, armTs);

            // The compact body rig always has one root (Pelvis), one headward spine
            // chain, and a tail branch. Positions are evaluated from normalized arc
            // length, so increasing render/morphology sample density does not alter
            // topology or stable semantic IDs.
            Vector3 pelvis = EvaluateCanonical(body, storedHeadToTail, pelvisT);
            Vector3 chest = EvaluateCanonical(body, storedHeadToTail, chestT);
            Vector3 head = EvaluateCanonical(body, storedHeadToTail, 0f);
            Vector3 tail = EvaluateCanonical(body, storedHeadToTail, 1f);

            Vector3 spineDirection = chest - pelvis;
            Vector3 headDirection = head - chest;
            Vector3 tailDirection = tail - pelvis;

            float spineRadius = EvaluateRadiusCanonical(body, storedHeadToTail, (pelvisT + chestT) * 0.5f);
            float headRadius = EvaluateRadiusCanonical(body, storedHeadToTail, 0f);
            float tailRadius = EvaluateRadiusCanonical(body, storedHeadToTail, (pelvisT + 1f) * 0.5f);

            var result = new List<BoneSpec>(4)
            {
                CreateSpec(PelvisBoneId, null, pelvis, chest, pelvisT, chestT, spineRadius, spineDirection, axis),
                CreateSpec(SpineBoneId, PelvisBoneId, chest, head, chestT, 0f, headRadius, headDirection, axis),
                CreateTerminalSpec(HeadBoneId, SpineBoneId, head, 0f, headRadius, headDirection, axis),
                CreateSpec(TailBoneId, PelvisBoneId, pelvis, tail, pelvisT, 1f, tailRadius, tailDirection, axis),
            };

            return result;
        }

        /// <summary>
        /// Returns the semantic compact-body bone that should own an attachment at
        /// the given creature-space position. An anchor sample id is honored first,
        /// then ordinary nearest-segment selection is used.
        /// </summary>
        public static string ResolveAttachmentBoneId(
            IReadOnlyList<Vector3> samplePositions,
            IReadOnlyList<uint> sampleIds,
            Vector3 forward,
            Vector3 position,
            bool mirrored,
            uint anchorSampleId = 0u)
        {
            if (samplePositions == null || samplePositions.Count == 0 || sampleIds == null)
            {
                return null;
            }

            if (mirrored)
            {
                position = MirrorUtility.ReflectPointAcrossX(position);
            }

            ResolvedBody body = ResolvedBody.Resolve(BuildSyntheticSamples(samplePositions, sampleIds));
            Vector3 axis = forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.forward;
            bool storedHeadToTail = IsStoredHeadToTail(samplePositions, axis);
            IReadOnlyList<BoneSpec> bones = Build(body, forward);

            if (anchorSampleId != 0u)
            {
                for (int i = 0; i < sampleIds.Count; i++)
                {
                    if (sampleIds[i] != anchorSampleId) continue;
                    float t = CanonicalArcT(body, storedHeadToTail, i);
                    return ResolveBoneAtCanonicalT(bones, t);
                }
            }

            string closestId = null;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < bones.Count; i++)
            {
                BoneSpec bone = bones[i];
                float distance = bone.HasSegment
                    ? SqrDistanceToSegment(position, bone.Position, bone.EndPosition)
                    : (position - bone.Position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestId = bone.Id;
                }
            }
            return closestId;
        }

        private static BoneSpec CreateSpec(
            string id, string parentId, Vector3 start, Vector3 end,
            float startT, float endT, float radius, Vector3 direction, Vector3 fallbackAxis)
        {
            bool hasSegment = (end - start).sqrMagnitude > 1e-10f;
            Quaternion rotation = ResolveBoneRotation(direction, fallbackAxis);
            return new BoneSpec(
                id, parentId, start, hasSegment ? end : start, hasSegment,
                rotation, startT, endT, radius);
        }

        private static BoneSpec CreateTerminalSpec(
            string id, string parentId, Vector3 position,
            float t, float radius, Vector3 direction, Vector3 fallbackAxis)
        {
            return new BoneSpec(
                id, parentId, position, position, false,
                ResolveBoneRotation(direction, fallbackAxis), t, t, radius);
        }

        private static Quaternion ResolveBoneRotation(Vector3 direction, Vector3 fallbackAxis)
        {
            Vector3 forward = direction.sqrMagnitude > 1e-10f ? direction.normalized : fallbackAxis;
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.9999f)
            {
                up = Vector3.forward;
                if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.9999f) up = Vector3.right;
            }
            return Quaternion.LookRotation(forward, up);
        }

        private static float DeterminePelvisT(IReadOnlyList<float> legTs, IReadOnlyList<float> armTs)
        {
            if (legTs != null && legTs.Count > 0)
            {
                float min = float.PositiveInfinity;
                float max = float.NegativeInfinity;
                for (int i = 0; i < legTs.Count; i++)
                {
                    min = Mathf.Min(min, legTs[i]);
                    max = Mathf.Max(max, legTs[i]);
                }

                // Biped legs normally form one cluster. Quadruped legs tend to form
                // a headward/front and tailward/rear pair; choose the rear cluster as
                // the anatomical pelvis and leave the front cluster as the chest.
                if (max - min > 0.2f)
                {
                    float split = (min + max) * 0.5f;
                    float sum = 0f;
                    int count = 0;
                    for (int i = 0; i < legTs.Count; i++)
                    {
                        if (legTs[i] >= split)
                        {
                            sum += legTs[i];
                            count++;
                        }
                    }
                    if (count > 0) return ClampT(sum / count);
                }

                float total = 0f;
                for (int i = 0; i < legTs.Count; i++) total += legTs[i];
                return ClampT(total / legTs.Count);
            }

            if (armTs != null && armTs.Count > 0)
            {
                float total = 0f;
                for (int i = 0; i < armTs.Count; i++) total += armTs[i];
                return ClampT(total / armTs.Count);
            }

            return 0.5f;
        }

        private static float DetermineChestT(float pelvisT, IReadOnlyList<float> legTs, IReadOnlyList<float> armTs)
        {
            if (armTs != null && armTs.Count > 0)
            {
                float total = 0f;
                for (int i = 0; i < armTs.Count; i++) total += armTs[i];
                return ClampT(total / armTs.Count);
            }

            if (legTs != null && legTs.Count > 1)
            {
                float min = float.PositiveInfinity;
                float max = float.NegativeInfinity;
                for (int i = 0; i < legTs.Count; i++)
                {
                    min = Mathf.Min(min, legTs[i]);
                    max = Mathf.Max(max, legTs[i]);
                }
                if (max - min > 0.2f) return ClampT(min);
            }

            return ClampT(Mathf.Lerp(pelvisT, 0f, 0.55f));
        }

        private static float CanonicalArcTAtPoint(
            ResolvedBody body, Vector3 axis, bool storedHeadToTail, Vector3 point)
        {
            float bestDistance = float.PositiveInfinity;
            float bestStoredT = 0f;
            for (int i = 0; i < body.SamplePositions.Count - 1; i++)
            {
                Vector3 a = body.SamplePositions[i];
                Vector3 b = body.SamplePositions[i + 1];
                Vector3 ab = b - a;
                float lengthSqr = ab.sqrMagnitude;
                float local = lengthSqr > 1e-12f
                    ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr)
                    : 0f;
                Vector3 closest = a + local * ab;
                float distance = (point - closest).sqrMagnitude;
                if (distance >= bestDistance) continue;

                float aT = body.NormalizedArcLengthAtSample[i];
                float bT = body.NormalizedArcLengthAtSample[i + 1];
                bestStoredT = Mathf.Lerp(aT, bT, local);
                bestDistance = distance;
            }

            if (body.SamplePositions.Count == 1) bestStoredT = 0f;
            float canonical = storedHeadToTail ? bestStoredT : 1f - bestStoredT;

            // axis is intentionally consumed to keep the direction decision part of
            // the same named policy as endpoint classification; it also guards a
            // future zero-forward implementation from silently changing semantics.
            _ = axis;
            return ClampT(canonical);
        }

        private static Vector3 EvaluateCanonical(ResolvedBody body, bool storedHeadToTail, float canonicalT)
        {
            float storedT = storedHeadToTail ? canonicalT : 1f - canonicalT;
            return EvaluateStored(body, storedT);
        }

        private static float EvaluateRadiusCanonical(ResolvedBody body, bool storedHeadToTail, float canonicalT)
        {
            float storedT = storedHeadToTail ? canonicalT : 1f - canonicalT;
            if (body.SampleRadii == null || body.SampleRadii.Count == 0) return 0.5f;
            if (body.SampleRadii.Count == 1) return Mathf.Max(0.001f, body.SampleRadii[0]);

            float clamped = Mathf.Clamp01(storedT);
            int segment = body.SampleRadii.Count - 2;
            for (int i = 0; i < body.NormalizedArcLengthAtSample.Count - 1; i++)
            {
                if (clamped <= body.NormalizedArcLengthAtSample[i + 1])
                {
                    segment = i;
                    break;
                }
            }
            float aT = body.NormalizedArcLengthAtSample[segment];
            float bT = body.NormalizedArcLengthAtSample[segment + 1];
            float local = bT > aT + 1e-8f ? (clamped - aT) / (bT - aT) : 0f;
            return Mathf.Max(0.001f, Mathf.Lerp(body.SampleRadii[segment], body.SampleRadii[segment + 1], Mathf.Clamp01(local)));
        }

        private static Vector3 EvaluateStored(ResolvedBody body, float storedT)
        {
            float clamped = Mathf.Clamp01(storedT);
            if (body.SamplePositions.Count == 1) return body.SamplePositions[0];
            int segment = body.SamplePositions.Count - 2;
            for (int i = 0; i < body.NormalizedArcLengthAtSample.Count - 1; i++)
            {
                if (clamped <= body.NormalizedArcLengthAtSample[i + 1])
                {
                    segment = i;
                    break;
                }
            }
            float aT = body.NormalizedArcLengthAtSample[segment];
            float bT = body.NormalizedArcLengthAtSample[segment + 1];
            float local = bT > aT + 1e-8f ? (clamped - aT) / (bT - aT) : 0f;
            return Vector3.Lerp(body.SamplePositions[segment], body.SamplePositions[segment + 1], Mathf.Clamp01(local));
        }

        private static float CanonicalArcT(ResolvedBody body, bool storedHeadToTail, int sampleIndex)
        {
            float storedT = Mathf.Clamp01(body.NormalizedArcLengthAtSample[sampleIndex]);
            return ClampT(storedHeadToTail ? storedT : 1f - storedT);
        }

        private static string ResolveBoneAtCanonicalT(IReadOnlyList<BoneSpec> bones, float t)
        {
            // Head has priority at the exact head endpoint; tail has priority at
            // the tail endpoint. Otherwise choose the compact segment whose arc
            // interval contains the attachment.
            if (t <= 1e-5f) return HeadBoneId;
            if (t >= 1f - 1e-5f) return TailBoneId;

            string best = PelvisBoneId;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < bones.Count; i++)
            {
                BoneSpec bone = bones[i];
                if (!bone.HasSegment) continue;
                float min = Mathf.Min(bone.StartT, bone.EndT);
                float max = Mathf.Max(bone.StartT, bone.EndT);
                float distance = t < min ? min - t : (t > max ? t - max : 0f);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = bone.Id;
                }
            }
            return best;
        }

        private static bool IsStoredHeadToTail(IReadOnlyList<Vector3> positions, Vector3 forward)
        {
            if (positions.Count < 2) return true;
            float startProjection = Vector3.Dot(positions[0], forward);
            float endProjection = Vector3.Dot(positions[positions.Count - 1], forward);
            return endProjection >= startProjection;
        }

        private static float ClampT(float t)
        {
            return Mathf.Clamp(t, 0.05f, 0.95f);
        }

        private static float SqrDistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr <= 1e-12f) return (point - a).sqrMagnitude;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr);
            Vector3 closest = a + t * ab;
            return (point - closest).sqrMagnitude;
        }

        private static List<BodySample> BuildSyntheticSamples(
            IReadOnlyList<Vector3> positions, IReadOnlyList<uint> ids)
        {
            var samples = new List<BodySample>(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                samples.Add(new BodySample { Id = ids[i], Position = positions[i], Radius = 1f });
            }
            return samples;
        }
    }
}
