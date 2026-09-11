using System;
using System.Collections.Generic;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Animation.Binding
{
    public readonly struct InfluenceDomain
    {
        private readonly string[] allowedDomainIds;
        public string DomainId { get; }
        public InfluenceDomain(string domainId, params string[] allowedDomainIds)
        {
            if (string.IsNullOrEmpty(domainId)) throw new DomainException("Influence domain id must not be empty.");
            DomainId = domainId;
            this.allowedDomainIds = allowedDomainIds == null || allowedDomainIds.Length == 0
                ? new[] { domainId } : (string[])allowedDomainIds.Clone();
        }
        public bool Allows(string candidateDomainId)
        {
            if (string.IsNullOrEmpty(candidateDomainId)) return false;
            for (int i = 0; i < allowedDomainIds.Length; i++)
                if (string.Equals(allowedDomainIds[i], candidateDomainId, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public readonly struct BoneSegmentInfluence
    {
        public readonly int BoneIndex;
        public readonly bool IsMirrored;
        public readonly Vector3 Start;
        public readonly Vector3 End;
        public readonly float Radius;
        public readonly string DomainId;

        public BoneSegmentInfluence(int boneIndex, bool isMirrored, Vector3 start, Vector3 end, float radius)
            : this(boneIndex, isMirrored, start, end, radius, string.Empty) { }

        public BoneSegmentInfluence(int boneIndex, bool isMirrored, Vector3 start, Vector3 end, float radius, string domainId)
        {
            BoneIndex = boneIndex;
            IsMirrored = isMirrored;
            Start = start;
            End = end;
            Radius = radius;
            DomainId = domainId ?? string.Empty;
        }
    }

    public static class ImplicitSurfaceWeightAuthoring
    {
        public const int MaxInfluencesPerVertex = LinearBlendSkinning.MaxBoneInfluencesPerVertex;
        public const float RadiusScale = 3f;
        public const float WeightFalloffPower = 2f;
        public const float DefaultInfluenceRadius = 0.5f;

        public static List<BoneSegmentInfluence> BuildSegmentInfluences(
            SkeletonSnapshot skeleton, IReadOnlyList<float> radiiByBoneIndex = null)
        {
            if (skeleton == null) throw new DomainException("skeleton must not be null.");
            var result = new List<BoneSegmentInfluence>(skeleton.Count);
            for (int i = 0; i < skeleton.Count; i++)
            {
                BoneSnapshot bone = skeleton[i];
                if (!bone.HasSegment) continue;
                float radius = DefaultInfluenceRadius;
                if (radiiByBoneIndex != null && i < radiiByBoneIndex.Count)
                {
                    float supplied = radiiByBoneIndex[i];
                    if (supplied > 0f && NumericValidity.IsFinite(supplied)) radius = supplied;
                }
                result.Add(new BoneSegmentInfluence(i, bone.IsMirrored, bone.Position, bone.EndPosition, radius, ResolveDomainId(bone)));
            }
            return result;
        }

        public static List<BoneSegmentInfluence> BuildBindingInfluences(
            SkeletonSnapshot skeleton, IReadOnlyList<float> radiiByBoneIndex = null)
        {
            if (skeleton == null) throw new DomainException("skeleton must not be null.");
            List<BoneSegmentInfluence> result = BuildSegmentInfluences(skeleton, radiiByBoneIndex);
            var included = new bool[skeleton.Count];
            for (int i = 0; i < result.Count; i++) included[result[i].BoneIndex] = true;
            for (int i = 0; i < skeleton.Count; i++)
            {
                if (included[i]) continue;
                float radius = DefaultInfluenceRadius;
                if (radiiByBoneIndex != null && i < radiiByBoneIndex.Count)
                {
                    float supplied = radiiByBoneIndex[i];
                    if (supplied > 0f && NumericValidity.IsFinite(supplied)) radius = supplied;
                }
                Vector3 position = skeleton[i].Position;
                result.Add(new BoneSegmentInfluence(i, skeleton[i].IsMirrored, position, position, radius, ResolveDomainId(skeleton[i])));
            }
            return result;
        }

        private static string ResolveDomainId(BoneSnapshot bone)
        {
            string sourceId = string.IsNullOrEmpty(bone.SourcePartId) ? bone.Id : bone.SourcePartId;
            return bone.IsMirrored ? sourceId + SemanticBoneResolver.MirrorSuffix : sourceId;
        }

        public static VertexInfluence[][] Author(
            IReadOnlyList<BoneSegmentInfluence> segments,
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<InfluenceDomain> vertexDomains = null)
        {
            if (segments == null) throw new DomainException("segments must not be null.");
            if (restVertices == null) throw new DomainException("restVertices must not be null.");
            if (vertexDomains != null && vertexDomains.Count != restVertices.Count)
                throw new DomainException("vertexDomains must match restVertices count.");
            if (segments.Count == 0) throw new DomainException("At least one eligible bone segment is required to author weights.");

            for (int s = 0; s < segments.Count; s++)
            {
                BoneSegmentInfluence seg = segments[s];
                if (seg.BoneIndex < 0) throw new DomainException($"Segment {s} has a negative BoneIndex.");
                if (!NumericValidity.IsFinite(seg.Start) || !NumericValidity.IsFinite(seg.End))
                    throw new DomainException($"Segment {s} has a non-finite endpoint.");
                if (!(seg.Radius > 0f) || float.IsNaN(seg.Radius) || float.IsInfinity(seg.Radius))
                    throw new DomainException($"Segment {s} must have a finite positive Radius.");
            }

            var result = new VertexInfluence[restVertices.Count][];
            var weightBySegment = new float[segments.Count];
            var candidates = new List<int>(segments.Count);

            for (int v = 0; v < restVertices.Count; v++)
            {
                Vector3 vertex = restVertices[v];
                if (!NumericValidity.IsFinite(vertex)) throw new DomainException($"Rest vertex {v} is not finite.");

                candidates.Clear();
                for (int s = 0; s < segments.Count; s++)
                {
                    BoneSegmentInfluence seg = segments[s];
                    if (vertexDomains != null && !vertexDomains[v].Allows(seg.DomainId))
                    {
                        weightBySegment[s] = 0f;
                        continue;
                    }
                    float distance = DistanceToSegment(vertex, seg.Start, seg.End);
                    float effectiveRadius = seg.Radius * RadiusScale;
                    float falloff = 1f - distance / effectiveRadius;
                    weightBySegment[s] = falloff > 0f ? Mathf.Pow(falloff, WeightFalloffPower) : 0f;
                    if (weightBySegment[s] > 0f) candidates.Add(s);
                }

                if (candidates.Count == 0 && vertexDomains != null)
                {
                    // Generated surface/domain correspondence is authoritative for
                    // cross-chain isolation, but the falloff radius is only a weighting
                    // neighborhood. A generated surface vertex can legitimately fall
                    // just outside every falloff tube (notably at compact-body chords,
                    // smooth unions, and attachment transitions). Do not fail an entire
                    // regeneration or leak into a sibling/opposite-side domain. Instead,
                    // bind deterministically to the nearest segment admitted by the
                    // already-resolved anatomical domain. This is a totality guard for
                    // the domain-constrained production path; the unrestricted helper
                    // remains strict so accidental omissions are still surfaced.
                    float nearestSqr = float.PositiveInfinity;
                    int nearestSegment = -1;
                    for (int s = 0; s < segments.Count; s++)
                    {
                        BoneSegmentInfluence seg = segments[s];
                        if (!vertexDomains[v].Allows(seg.DomainId)) continue;
                        float distanceSqr = SqrDistanceToSegment(vertex, seg.Start, seg.End);
                        if (distanceSqr < nearestSqr ||
                            (Mathf.Approximately(distanceSqr, nearestSqr) &&
                             (nearestSegment < 0 || seg.BoneIndex < segments[nearestSegment].BoneIndex)))
                        {
                            nearestSqr = distanceSqr;
                            nearestSegment = s;
                        }
                    }

                    if (nearestSegment >= 0)
                    {
                        candidates.Add(nearestSegment);
                        weightBySegment[nearestSegment] = 1f;
                    }
                }

                if (candidates.Count == 0)
                    throw new DomainException($"Rest vertex {v} has no eligible bone segment within influence range or resolved domain.");

                candidates.Sort((int left, int right) =>
                {
                    int byWeight = weightBySegment[right].CompareTo(weightBySegment[left]);
                    if (byWeight != 0) return byWeight;
                    return segments[left].BoneIndex.CompareTo(segments[right].BoneIndex);
                });

                int take = Math.Min(MaxInfluencesPerVertex, candidates.Count);
                float totalWeight = 0f;
                for (int i = 0; i < take; i++) totalWeight += weightBySegment[candidates[i]];
                if (!(totalWeight > 0f)) throw new DomainException($"Rest vertex {v} has no net bone weight.");

                var influences = new VertexInfluence[take];
                for (int i = 0; i < take; i++)
                {
                    int segmentIndex = candidates[i];
                    influences[i] = new VertexInfluence(segments[segmentIndex].BoneIndex,
                        weightBySegment[segmentIndex] / totalWeight);
                }
                result[v] = influences;
            }
            return result;
        }

        public static float WeightFor(Vector3 vertex, BoneSegmentInfluence segment)
        {
            float distance = DistanceToSegment(vertex, segment.Start, segment.End);
            float effectiveRadius = segment.Radius * RadiusScale;
            float falloff = 1f - distance / effectiveRadius;
            return falloff > 0f ? Mathf.Pow(falloff, WeightFalloffPower) : 0f;
        }

        public static float SqrDistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            if (abSqr <= 1e-12f) return (point - a).sqrMagnitude;
            float t = Vector3.Dot(point - a, ab) / abSqr;
            t = Mathf.Clamp01(t);
            Vector3 closest = a + t * ab;
            return (point - closest).sqrMagnitude;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b) =>
            Mathf.Sqrt(SqrDistanceToSegment(point, a, b));
    }
}