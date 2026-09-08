using System;
using System.Collections.Generic;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;
using UnityEngine;

namespace ProceduralCreature.Skeleton
{
    /// <summary>
    /// Derives a density-independent anatomical Body backbone from the resolved
    /// dense spline. Dense Body samples remain morphology/SDF data, while the rig
    /// uses a separate canonical-arc segmentation so a finer morphology sample
    /// grid does not silently remove or add character joints.
    ///
    /// Limb count, order, type, and attachment position are data-driven; there is
    /// no biped/quadruped topology mode. The Body backbone is deliberately
    /// segmented rather than collapsed to one long chest/head bone: articulated
    /// regions must follow the authored centerline closely enough for posing and
    /// skinning to remain useful on strongly curved bodies and necks.
    /// </summary>
    public static class AnatomicalBodyRigLayout
    {
        public const string BodyRootBoneId = "body_root";
        public const string SpineBoneId = "body_spine";
        public const string HeadBoneId = "body_head";
        public const string TailBoneId = "body_tail";

        private const float EpsilonSqr = 1e-10f;
        private const float InteriorMinT = 0.05f;
        private const float InteriorMaxT = 0.95f;
        private const float DefaultBodyRootT = 0.5f;
        private const float BodySegmentArcStep = 0.12f;
        private const int MaxBodyBranchSegments = 8;

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

        public static IReadOnlyList<BoneSpec> Build(ResolvedCreatureSnapshot snapshot)
        {
            if (snapshot == null) throw new DomainException("snapshot must not be null.");
            return snapshot.HasBody
                ? Build(snapshot.Body, snapshot.Forward, snapshot.PartsById.Values)
                : Array.Empty<BoneSpec>();
        }

        public static IReadOnlyList<BoneSpec> Build(ResolvedBody body, Vector3 forward)
            => Build(body, forward, null);

        public static string ResolveAttachmentBoneId(ResolvedCreatureSnapshot snapshot, Vector3 position, bool mirrored, uint anchorSampleId = 0u)
        {
            if (snapshot == null) throw new DomainException("snapshot must not be null.");
            if (!snapshot.HasBody) return null;
            return ResolveAttachmentBoneId(snapshot.Body, snapshot.Forward, position, mirrored, anchorSampleId, snapshot.PartsById.Values);
        }

        public static string ResolveAttachmentBoneId(ResolvedBody body, Vector3 forward, Vector3 position, bool mirrored, uint anchorSampleId = 0u)
            => ResolveAttachmentBoneId(body, forward, position, mirrored, anchorSampleId, null);

        private static string ResolveAttachmentBoneId(ResolvedBody body, Vector3 forward, Vector3 position, bool mirrored,
            uint anchorSampleId, IEnumerable<ResolvedPartSnapshot> resolvedParts)
        {
            if (body.SamplePositions == null || body.SamplePositions.Count == 0) return null;
            if (mirrored) position = MirrorUtility.ReflectPointAcrossX(position);

            Vector3 axis = NormalizeForward(forward);
            bool storedHeadToTail = IsStoredHeadToTail(body.SamplePositions, axis);
            IReadOnlyList<BoneSpec> bones = Build(body, forward, resolvedParts);

            float canonicalT = -1f;
            if (anchorSampleId != 0u && body.SampleIds != null)
            {
                for (int i = 0; i < body.SampleIds.Count; i++)
                {
                    if (body.SampleIds[i] == anchorSampleId)
                    {
                        canonicalT = CanonicalArcT(body, storedHeadToTail, i);
                        break;
                    }
                }
            }

            if (canonicalT < 0f) canonicalT = CanonicalArcTAtPoint(body, storedHeadToTail, position);
            return ResolveBoneAtCanonicalT(bones, canonicalT);
        }

        private static IReadOnlyList<BoneSpec> Build(ResolvedBody body, Vector3 forward, IEnumerable<ResolvedPartSnapshot> resolvedParts)
        {
            if (body.SamplePositions == null || body.SamplePositions.Count == 0) return Array.Empty<BoneSpec>();

            Vector3 axis = NormalizeForward(forward);
            bool storedHeadToTail = IsStoredHeadToTail(body.SamplePositions, axis);
            var attachmentTs = new List<float>();
            if (resolvedParts != null)
            {
                foreach (ResolvedPartSnapshot part in resolvedParts)
                {
                    if (!part.HasLimb || part.ParentId != CreatureDefinition.BodyId) continue;
                    if (part.Limb.JointPositions == null || part.Limb.JointPositions.Count == 0) continue;
                    Vector3 root = part.PartFrameToCreatureSpace.MultiplyPoint3x4(part.Limb.RootSocket);
                    attachmentTs.Add(CanonicalArcTAtPoint(body, storedHeadToTail, root));
                }
            }

            float bodyRootT = DetermineBodyRootT(attachmentTs);
            float bodyRootHeadStepT = Mathf.Max(0f, bodyRootT - BodySegmentArcStep);
            Vector3 bodyRoot = EvaluateCanonical(body, storedHeadToTail, bodyRootT);
            Vector3 head = EvaluateCanonical(body, storedHeadToTail, 0f);
            Vector3 tail = EvaluateCanonical(body, storedHeadToTail, 1f);
            var result = new List<BoneSpec>(16);

            Vector3 firstSpine = EvaluateCanonical(body, storedHeadToTail, bodyRootHeadStepT);
            result.Add(CreateSpec(BodyRootBoneId, null, bodyRoot, firstSpine, bodyRootT, bodyRootHeadStepT,
                EvaluateRadiusCanonical(body, storedHeadToTail, (bodyRootT + bodyRootHeadStepT) * 0.5f), axis));

            float previousT = bodyRootHeadStepT;
            string previousId = BodyRootBoneId;
            int spineIndex = 0;
            while (previousT > 0f && spineIndex < MaxBodyBranchSegments)
            {
                float endT = Mathf.Max(0f, previousT - BodySegmentArcStep);
                string id = IndexedBoneId(SpineBoneId, spineIndex);
                Vector3 start = EvaluateCanonical(body, storedHeadToTail, previousT);
                Vector3 end = endT <= Mathf.Epsilon ? head : EvaluateCanonical(body, storedHeadToTail, endT);
                result.Add(CreateSpec(id, previousId, start, end, previousT, endT,
                    EvaluateRadiusCanonical(body, storedHeadToTail, (previousT + endT) * 0.5f), axis));
                previousId = id;
                previousT = endT;
                spineIndex++;
            }

            result.Add(CreateTerminalSpec(HeadBoneId, previousId, head, 0f,
                EvaluateRadiusCanonical(body, storedHeadToTail, 0f), axis));

            previousT = bodyRootT;
            previousId = BodyRootBoneId;
            int tailIndex = 0;
            while (previousT < 1f && tailIndex < MaxBodyBranchSegments)
            {
                float endT = Mathf.Min(1f, previousT + BodySegmentArcStep);
                string id = IndexedBoneId(TailBoneId, tailIndex);
                Vector3 start = EvaluateCanonical(body, storedHeadToTail, previousT);
                Vector3 end = endT >= 1f ? tail : EvaluateCanonical(body, storedHeadToTail, endT);
                result.Add(CreateSpec(id, previousId, start, end, previousT, endT,
                    EvaluateRadiusCanonical(body, storedHeadToTail, (previousT + endT) * 0.5f), axis));
                previousId = id;
                previousT = endT;
                tailIndex++;
            }

            return result;
        }

        private static string IndexedBoneId(string baseId, int index)
            => index == 0 ? baseId : baseId + "_" + index;

        private static BoneSpec CreateSpec(string id, string parentId, Vector3 start, Vector3 end, float startT, float endT, float radius, Vector3 fallbackAxis)
        {
            Vector3 direction = end - start;
            bool hasSegment = direction.sqrMagnitude > EpsilonSqr;
            return new BoneSpec(id, parentId, start, hasSegment ? end : start, hasSegment,
                ResolveRotation(direction, fallbackAxis), startT, endT, Mathf.Max(0.001f, radius));
        }

        private static BoneSpec CreateTerminalSpec(string id, string parentId, Vector3 position, float t, float radius, Vector3 fallbackAxis)
            => new BoneSpec(id, parentId, position, position, false, ResolveRotation(fallbackAxis, fallbackAxis), t, t, Mathf.Max(0.001f, radius));

        private static float DetermineBodyRootT(IReadOnlyList<float> attachmentTs)
        {
            if (attachmentTs == null || attachmentTs.Count == 0) return DefaultBodyRootT;
            var sorted = new List<float>(attachmentTs.Count);
            for (int i = 0; i < attachmentTs.Count; i++)
                if (NumericValidity.IsFinite(attachmentTs[i])) sorted.Add(Mathf.Clamp01(attachmentTs[i]));
            if (sorted.Count == 0) return DefaultBodyRootT;
            sorted.Sort();
            int middle = sorted.Count / 2;
            float median = sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) * 0.5f;
            return ClampInterior(median);
        }

        private static string ResolveBoneAtCanonicalT(IReadOnlyList<BoneSpec> bones, float t)
        {
            if (bones == null || bones.Count == 0) return null;
            if (t <= 0f) return HeadBoneId;
            string terminalTailId = TailBoneId;
            for (int i = 0; i < bones.Count; i++)
                if (bones[i].Id == TailBoneId || bones[i].Id.StartsWith(TailBoneId + "_", StringComparison.Ordinal)) terminalTailId = bones[i].Id;
            if (t >= 1f) return terminalTailId;
            string best = BodyRootBoneId;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < bones.Count; i++)
            {
                BoneSpec bone = bones[i];
                if (!bone.HasSegment) continue;
                float min = Mathf.Min(bone.StartT, bone.EndT), max = Mathf.Max(bone.StartT, bone.EndT);
                if (t >= min && t <= max) return bone.Id;
                float distance = t < min ? min - t : t - max;
                if (distance < bestDistance) { bestDistance = distance; best = bone.Id; }
            }
            return best;
        }

        private static Quaternion ResolveRotation(Vector3 direction, Vector3 fallbackAxis)
        {
            Vector3 forward = direction.sqrMagnitude > EpsilonSqr ? direction.normalized : fallbackAxis;
            if (forward.sqrMagnitude <= EpsilonSqr) forward = Vector3.forward;
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.9999f)
            {
                up = Vector3.forward;
                if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.9999f) up = Vector3.right;
            }
            return Quaternion.LookRotation(forward, up);
        }

        private static float CanonicalArcTAtPoint(ResolvedBody body, bool storedHeadToTail, Vector3 point)
        {
            if (body.SamplePositions.Count == 1) return 0f;
            float bestDistance = float.PositiveInfinity, bestStoredT = 0f;
            for (int i = 0; i < body.SamplePositions.Count - 1; i++)
            {
                Vector3 a = body.SamplePositions[i], b = body.SamplePositions[i + 1], ab = b - a;
                float lengthSqr = ab.sqrMagnitude;
                float localT = lengthSqr > EpsilonSqr ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr) : 0f;
                Vector3 closest = a + localT * ab;
                float distance = (point - closest).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestStoredT = Mathf.Lerp(body.NormalizedArcLengthAtSample[i], body.NormalizedArcLengthAtSample[i + 1], localT);
            }
            return storedHeadToTail ? bestStoredT : 1f - bestStoredT;
        }

        private static float CanonicalArcT(ResolvedBody body, bool storedHeadToTail, int sampleIndex)
        {
            float storedT = Mathf.Clamp01(body.NormalizedArcLengthAtSample[sampleIndex]);
            return storedHeadToTail ? storedT : 1f - storedT;
        }

        private static Vector3 EvaluateCanonical(ResolvedBody body, bool storedHeadToTail, float canonicalT)
            => EvaluateStored(body, storedHeadToTail ? canonicalT : 1f - canonicalT);

        private static Vector3 EvaluateStored(ResolvedBody body, float storedT)
        {
            if (body.SamplePositions.Count == 1) return body.SamplePositions[0];
            int segment = body.SamplePositions.Count - 2;
            float clamped = Mathf.Clamp01(storedT);
            for (int i = 0; i < body.NormalizedArcLengthAtSample.Count - 1; i++)
                if (clamped <= body.NormalizedArcLengthAtSample[i + 1]) { segment = i; break; }
            float startT = body.NormalizedArcLengthAtSample[segment], endT = body.NormalizedArcLengthAtSample[segment + 1];
            float span = endT - startT;
            float localT = span > EpsilonSqr ? (clamped - startT) / span : 0f;
            return Vector3.Lerp(body.SamplePositions[segment], body.SamplePositions[segment + 1], Mathf.Clamp01(localT));
        }

        private static float EvaluateRadiusCanonical(ResolvedBody body, bool storedHeadToTail, float canonicalT)
        {
            if (body.SampleRadii == null || body.SampleRadii.Count == 0) return 0.5f;
            float storedT = storedHeadToTail ? canonicalT : 1f - canonicalT;
            if (body.SampleRadii.Count == 1) return body.SampleRadii[0];
            float clamped = Mathf.Clamp01(storedT);
            int segment = body.SampleRadii.Count - 2;
            for (int i = 0; i < body.NormalizedArcLengthAtSample.Count - 1; i++)
                if (clamped <= body.NormalizedArcLengthAtSample[i + 1]) { segment = i; break; }
            float startT = body.NormalizedArcLengthAtSample[segment], endT = body.NormalizedArcLengthAtSample[segment + 1];
            float span = endT - startT;
            float localT = span > EpsilonSqr ? (clamped - startT) / span : 0f;
            return Mathf.Lerp(body.SampleRadii[segment], body.SampleRadii[segment + 1], Mathf.Clamp01(localT));
        }

        private static bool IsStoredHeadToTail(IReadOnlyList<Vector3> positions, Vector3 forward)
        {
            if (positions.Count < 2) return true;
            return Vector3.Dot(positions[0], forward) >= Vector3.Dot(positions[positions.Count - 1], forward);
        }

        private static Vector3 NormalizeForward(Vector3 forward)
            => forward.sqrMagnitude > EpsilonSqr ? forward.normalized : Vector3.forward;

        private static float ClampInterior(float value)
            => Mathf.Clamp(value, InteriorMinT, InteriorMaxT);
    }
}
