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

        /// <summary>Radius used when a bone has no finite positive resolved morphology radius.</summary>
        public const float DefaultInfluenceRadius = InfluenceWeightingPolicy.DefaultBoneRadius;

        public static List<BoneSegmentInfluence> BuildSegmentInfluences(
            SkeletonSnapshot skeleton, IReadOnlyList<float> radiiByBoneIndex = null, InfluenceWeightingPolicy? policy = null)
        {
            InfluenceWeightingPolicy resolvedPolicy = policy ?? InfluenceWeightingPolicy.Default;
            if (skeleton == null) throw new DomainException("skeleton must not be null.");
            var result = new List<BoneSegmentInfluence>(skeleton.Count);
            for (int i = 0; i < skeleton.Count; i++)
            {
                BoneSnapshot bone = skeleton[i];
                if (!bone.HasSegment) continue;
                float radius = ResolveBoneRadius(radiiByBoneIndex, i, resolvedPolicy);
                result.Add(new BoneSegmentInfluence(i, bone.IsMirrored, bone.Position, bone.EndPosition, radius, ResolveDomainId(bone)));
            }
            return result;
        }

        public static List<BoneSegmentInfluence> BuildBindingInfluences(
            SkeletonSnapshot skeleton, IReadOnlyList<float> radiiByBoneIndex = null, InfluenceWeightingPolicy? policy = null)
        {
            InfluenceWeightingPolicy resolvedPolicy = policy ?? InfluenceWeightingPolicy.Default;
            if (skeleton == null) throw new DomainException("skeleton must not be null.");
            List<BoneSegmentInfluence> result = BuildSegmentInfluences(skeleton, radiiByBoneIndex, resolvedPolicy);
            var included = new bool[skeleton.Count];
            for (int i = 0; i < result.Count; i++) included[result[i].BoneIndex] = true;
            for (int i = 0; i < skeleton.Count; i++)
            {
                if (included[i]) continue;
                float radius = ResolveBoneRadius(radiiByBoneIndex, i, resolvedPolicy);
                Vector3 position = skeleton[i].Position;
                result.Add(new BoneSegmentInfluence(i, skeleton[i].IsMirrored, position, position, radius, ResolveDomainId(skeleton[i])));
            }
            return result;
        }

        private static float ResolveBoneRadius(IReadOnlyList<float> radiiByBoneIndex, int boneIndex, InfluenceWeightingPolicy policy)
        {
            if (radiiByBoneIndex != null && boneIndex < radiiByBoneIndex.Count)
            {
                float supplied = radiiByBoneIndex[boneIndex];
                if (supplied > 0f && NumericValidity.IsFinite(supplied)) return supplied;
            }
            return policy.FallbackBoneRadius;
        }

        private static string ResolveDomainId(BoneSnapshot bone)
        {
            string sourceId = string.IsNullOrEmpty(bone.SourcePartId) ? bone.Id : bone.SourcePartId;
            return bone.IsMirrored ? sourceId + SemanticBoneResolver.MirrorSuffix : sourceId;
        }

        public static VertexInfluence[][] Author(
            IReadOnlyList<BoneSegmentInfluence> segments,
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<InfluenceDomain> vertexDomains = null,
            InfluenceWeightingPolicy? policy = null)
        {
            InfluenceWeightingPolicy resolvedPolicy = policy ?? InfluenceWeightingPolicy.Default;
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
                FillCandidates(vertex, segments, vertexDomains, v, resolvedPolicy, weightBySegment, candidates);

                if (candidates.Count == 0 && resolvedPolicy.ChainAwareLocality)
                {
                    // Totality guard: the longitudinal gate must never leave a vertex
                    // with no influence, for example a surface point that sits outside
                    // every bone's own span. Fall back to the ungated radial model for
                    // that vertex only and keep the resolved domain filter.
                    candidates.Clear();
                    FillCandidates(vertex, segments, vertexDomains, v,
                        resolvedPolicy.WithoutChainAwareLocality(), weightBySegment, candidates);
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

        public static float WeightFor(Vector3 vertex, BoneSegmentInfluence segment, InfluenceWeightingPolicy? policy = null)
        {
            return FalloffFor(vertex, segment, policy ?? InfluenceWeightingPolicy.Default);
        }

        private static void FillCandidates(
            Vector3 vertex,
            IReadOnlyList<BoneSegmentInfluence> segments,
            IReadOnlyList<InfluenceDomain> vertexDomains,
            int vertexIndex,
            InfluenceWeightingPolicy policy,
            float[] weightBySegment,
            List<int> candidates)
        {
            for (int s = 0; s < segments.Count; s++)
            {
                BoneSegmentInfluence seg = segments[s];
                if (vertexDomains != null && !vertexDomains[vertexIndex].Allows(seg.DomainId))
                {
                    weightBySegment[s] = 0f;
                    continue;
                }
                weightBySegment[s] = FalloffFor(vertex, seg, policy);
                if (weightBySegment[s] > 0f) candidates.Add(s);
            }
        }

        /// <summary>
        /// Radial falloff plus the optional chain-aware longitudinal gate. A bone only
        /// reaches its own span plus a blend band past each endpoint, so a forearm
        /// segment cannot reach up and drag the upper arm.
        /// </summary>
        private static float FalloffFor(Vector3 vertex, BoneSegmentInfluence segment, InfluenceWeightingPolicy policy)
        {
            float effectiveRadius = segment.Radius * policy.RadiusScale;
            if (!(effectiveRadius > 0f)) return 0f;

            Vector3 ab = segment.End - segment.Start;
            float abSqr = ab.sqrMagnitude;
            if (abSqr <= 1e-12f)
            {
                float pointDistance = Vector3.Distance(vertex, segment.Start);
                float pointFalloff = 1f - pointDistance / effectiveRadius;
                return pointFalloff > 0f ? Mathf.Pow(pointFalloff, policy.FalloffPower) : 0f;
            }

            if (policy.ChainAwareLocality)
            {
                // Bone-local span gate: the unclamped projection must fall inside the
                // segment plus a band of LongitudinalBlendMarginRadii bone radii at each
                // end. Geometry behind the bone's own start belongs to an upstream bone.
                float abLength = Mathf.Sqrt(abSqr);
                float longitudinalT = Vector3.Dot(vertex - segment.Start, ab) / abSqr;
                float marginT = policy.LongitudinalBlendMarginRadii * segment.Radius / abLength;
                if (longitudinalT < -marginT || longitudinalT > 1f + marginT) return 0f;
            }

            float distance = DistanceToSegment(vertex, segment.Start, segment.End);
            float falloff = 1f - distance / effectiveRadius;
            return falloff > 0f ? Mathf.Pow(falloff, policy.FalloffPower) : 0f;
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