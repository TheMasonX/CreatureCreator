using System;
using System.Collections.Generic;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Animation.Binding
{
    /// <summary>
    /// Explicit domain eligibility for one rest-space vertex. This is a binding
    /// contract, not a mesh-part attribution: callers may configure a transition
    /// vertex to admit its own chain domain and named adjacent domains.
    /// </summary>
    public readonly struct InfluenceDomain
    {
        private readonly string[] allowedDomainIds;

        public string DomainId { get; }

        public InfluenceDomain(string domainId, params string[] allowedDomainIds)
        {
            if (string.IsNullOrEmpty(domainId))
            {
                throw new DomainException("Influence domain id must not be empty.");
            }

            DomainId = domainId;
            this.allowedDomainIds = allowedDomainIds == null || allowedDomainIds.Length == 0
                ? new[] { domainId }
                : (string[])allowedDomainIds.Clone();
        }

        public bool Allows(string candidateDomainId)
        {
            if (string.IsNullOrEmpty(candidateDomainId)) return false;
            for (int i = 0; i < allowedDomainIds.Length; i++)
            {
                if (string.Equals(allowedDomainIds[i], candidateDomainId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// One eligible influence primitive for the implicit welded surface: the axis
    /// segment of a bone that carries geometry, plus the influence radius that
    /// converts a rest-space distance into a blend weight.
    ///
    /// The generated implicit welded surface (Body + all SDF limb metaballs) is
    /// ONE marching-cubes mesh whose item carries <c>SourcePartId = ""</c> — it has
    /// NO per-vertex part attribution. Weights are therefore geometric: for a rest
    /// vertex we measure distance to each eligible segment axis (the same axes the
    /// SDF built the surface from), not to an authored per-vertex part. A primitive
    /// is the concrete shape that distance is measured against.
    ///
    /// RADIUS SOURCE (why it is not on a Bone): <see cref="Skeleton.Bone"/> carries no
    /// radius — a radius is morphology, not skeleton. A primitive's <see cref="Radius"/>
    /// must come from the resolved DNA morphology the welded surface was built from
    /// (a Body sample radius, or a limb's metaball thickness at that segment). The
    /// <see cref="ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences"/> helper lets a
    /// caller supply one radius per bone; the morphology-to-radius bridge over a full
    /// <see cref="ResolvedCreatureSnapshot"/> is owned by the mesh-asset/rendering
    /// adapter (TSK-0132) so this pure authoring core never guesses a radius from the
    /// skeleton alone.
    /// </summary>
    public readonly struct BoneSegmentInfluence
    {
        /// <summary>
        /// Index into the bone order fixed by <see cref="SkeletonSnapshot.Capture"/>.
        /// This is the shared bind-index ordering contract consumed by the
        /// <see cref="LinearBlendSkinning"/> adapter (TSK-0132) and rigid mesh-asset
        /// weighting (TSK-0130); this module never maps a part to a bone and never
        /// re-discovers identity.
        /// </summary>
        public readonly int BoneIndex;

        /// <summary>
        /// True when this bone is the mirrored copy of an authored part (its id ends
        /// in <see cref="SemanticBoneResolver.MirrorSuffix"/>). Mirrored influences
        /// are ordinary segment primitives at their reflected creature-space rest
        /// position — mirror identity is resolved once by the skeleton, never
        /// re-discovered here.
        /// </summary>
        public readonly bool IsMirrored;

        /// <summary>Segment start, in creature-space rest pose.</summary>
        public readonly Vector3 Start;

        /// <summary>Segment end, in creature-space rest pose.</summary>
        public readonly Vector3 End;

        /// <summary>Influence radius (metaball thickness-derived), used only to convert
        /// distance to a normalized, smooth blend weight. Must be positive.</summary>
        public readonly float Radius;

        /// <summary>
        /// Stable anatomical domain key. Build helpers source this from the bone's
        /// resolved SourcePartId, so all segments in one authored chain share a
        /// domain without re-deriving DNA or reading mesh vertices.
        /// </summary>
        public readonly string DomainId;

        public BoneSegmentInfluence(int boneIndex, bool isMirrored,
            Vector3 start, Vector3 end, float radius)
            : this(boneIndex, isMirrored, start, end, radius, string.Empty)
        {
        }

        public BoneSegmentInfluence(int boneIndex, bool isMirrored,
            Vector3 start, Vector3 end, float radius, string domainId)
        {
            BoneIndex = boneIndex;
            IsMirrored = isMirrored;
            Start = start;
            End = end;
            Radius = radius;
            DomainId = domainId ?? string.Empty;
        }
    }

    /// <summary>
    /// Authors deterministic, skeleton-aware, pose-independent BUILD-TIME per-vertex
    /// weights for the implicit welded surface. This is pure data on the generation
    /// output: it reads resolved rest-space vertices plus the resolved skeleton and
    /// emits one <see cref="VertexInfluence"/> list per vertex (up to
    /// <see cref="MaxInfluencesPerVertex"/>, normalized), which a renderer adapter
    /// applies once at bind time. It never runs during pose application and holds no
    /// skeleton, no <see cref="ResolvedCreatureSnapshot"/>, no pose state.
    ///
    /// MODEL (the accepted welded-surface approach — TSK-0131 / TSK-0077):
    ///
    /// * ELIGIBILITY: because the welded mesh carries no per-vertex part attribution,
    ///   eligible influences are the welded rig's segment-carrying bones (the Body
    ///   spine and limb segment bones) whose SDF axes generated that very surface. A
    ///   vertex therefore binds to the local chain segments near it. There is no
    ///   fabricated per-part attribution on a surface that has none.
    /// * DISTANCE IS TO THE SEGMENT AXIS, NOT THE BONE CENTER: <see cref="WeightFor"/>
    ///   uses the closest point on the segment <c>[Start, End]</c>. On a bent chain a
    ///   vertex on the inside of the bend binds to the correct segment rather than to
    ///   whichever bone's CENTER happens to be nearest, so bent chains deform
    ///   correctly.
    /// * JOINT BLENDING: a vertex near a shared joint endpoint is roughly equidistant
    ///   to both adjacent segments, so both contribute and the surface blends smoothly
    ///   across the seam instead of popping between single-bone bindings.
    /// * MIRROR: the resolved skeleton already contains BOTH the authored chain and
    ///   its reflected copy (SkeletonInferrer emits both), each at its creature-space
    ///   rest position. Nearest-segment authoring therefore binds mirrored-side
    ///   surface vertices to the mirrored bones with no mirror-identity rediscovery.
    /// * DETERMINISM: selection is sorted by (weight descending, <see cref="BoneSegmentInfluence.BoneIndex"/>
    ///   ascending), so the top-four and the resulting weights are independent of the
    ///   order the segments happen to be stored in. Bone indices are the
    ///   <see cref="SkeletonSnapshot.Capture"/> order (the shared bind-index contract).
    /// * FINITE/TOTAL: non-finite vertices, non-positive radii, or a vertex with no
    ///   eligible influence throw <see cref="DomainException"/> (a welded-surface
    ///   vertex should always be within influence range of the segment that generated
    ///   it).
    ///
    /// REST-POSE INVARIANT: weights are authored relative to the REST mesh; feeding the
    /// rest skeleton to <see cref="LinearBlendSkinning.Deform"/> reproduces the input
    /// vertices (unit weights recombine each bind offset exactly).
    /// </summary>
    public static class ImplicitSurfaceWeightAuthoring
    {
        /// <summary>
        /// The bind ceiling on authored influences per vertex. Kept equal to the
        /// deformation oracle's enforced ceiling so any weight list this author
        /// produces is guaranteed consumable by <see cref="LinearBlendSkinning.Deform"/>.
        /// </summary>
        public const int MaxInfluencesPerVertex = LinearBlendSkinning.MaxBoneInfluencesPerVertex;

        /// <summary>
        /// A surface vertex sits at roughly one influence <see cref="BoneSegmentInfluence.Radius"/>
        /// from its generating axis (a capsule of radius R). To keep that surface shell
        /// inside the influence falloff and allow joint seams to blend, each segment's
        /// influence is extended to <c>Radius * RadiusScale</c> before the falloff is
        /// applied. A vertex exactly on the surface (d == Radius) still receives
        /// <c>(1 - 1/RadiusScale)^2</c> of the peak weight.
        /// </summary>
        public const float RadiusScale = 3f;

        /// <summary>Exponent of the smooth falloff <c>(1 - d/effectiveRadius)^power</c>.</summary>
        public const float WeightFalloffPower = 2f;

        /// <summary>
        /// Fallback influence radius when a caller supplies segments without an explicit
        /// per-bone radius. The morphology-to-radius bridge (TSK-0132) is expected to
        /// supply real radii; this default keeps the skeleton-only helper total and the
        /// synthetic fixtures self-contained.
        /// </summary>
        public const float DefaultInfluenceRadius = 0.5f;

        /// <summary>
        /// Builds the eligible segment influences from a resolved skeleton snapshot, in
        /// bone-index order. Every bone with <see cref="BoneSnapshot.HasSegment"/> true
        /// contributes one primitive spanning <c>[Position, EndPosition]</c>; bones
        /// without a segment (accessory root nodes, the limb terminal joint, the final
        /// Body sample) are point attachment seats, not welded-surface geometry, and are
        /// not influences. The last segment of a chain still reaches the tip, so the tip
        /// region is covered by the final segment.
        /// </summary>
        /// <param name="skeleton">Resolved rest skeleton; defines the bone-index order.</param>
        /// <param name="radiiByBoneIndex">
        /// Optional morphology-derived radius per bone index (Body sample radius / limb
        /// thickness at that segment). When null (or shorter than the skeleton), a
        /// missing entry falls back to <see cref="DefaultInfluenceRadius"/>.
        /// </param>
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
                    if (supplied > 0f) radius = supplied;
                }

                result.Add(new BoneSegmentInfluence(
                    i, bone.IsMirrored, bone.Position, bone.EndPosition, radius,
                    ResolveDomainId(bone)));
            }
            return result;
        }

        /// <summary>
        /// Builds all binding influences for a welded implicit surface. Segment bones
        /// use their authored axis; attachment bones use a point influence at their
        /// rest position so non-limb SDF parts (for example eyes and feet) can bind
        /// without changing the segment-only authoring contract.
        /// </summary>
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
                result.Add(new BoneSegmentInfluence(
                    i, skeleton[i].IsMirrored, position, position, radius,
                    ResolveDomainId(skeleton[i])));
            }

            return result;
        }

        private static string ResolveDomainId(BoneSnapshot bone)
        {
            string sourceId = string.IsNullOrEmpty(bone.SourcePartId) ? bone.Id : bone.SourcePartId;
            return bone.IsMirrored ? sourceId + SemanticBoneResolver.MirrorSuffix : sourceId;
        }

        /// <summary>
        /// Authors per-vertex influences for <paramref name="restVertices"/> over the
        /// given eligible <paramref name="segments"/>. Returns one
        /// <see cref="VertexInfluence"/> array per vertex (same count and order as
        /// <paramref name="restVertices"/>), each with up to
        /// <see cref="MaxInfluencesPerVertex"/> influences that sum to 1.
        /// </summary>
        public static VertexInfluence[][] Author(
            IReadOnlyList<BoneSegmentInfluence> segments,
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<InfluenceDomain> vertexDomains = null)
        {
            if (segments == null) throw new DomainException("segments must not be null.");
            if (restVertices == null) throw new DomainException("restVertices must not be null.");
            if (vertexDomains != null && vertexDomains.Count != restVertices.Count)
            {
                throw new DomainException("vertexDomains must match restVertices count.");
            }
            if (segments.Count == 0)
            {
                throw new DomainException("At least one eligible bone segment is required to author weights.");
            }

            for (int s = 0; s < segments.Count; s++)
            {
                BoneSegmentInfluence seg = segments[s];
                if (seg.BoneIndex < 0)
                {
                    throw new DomainException($"Segment {s} has a negative BoneIndex.");
                }
                if (!NumericValidity.IsFinite(seg.Start) || !NumericValidity.IsFinite(seg.End))
                {
                    throw new DomainException($"Segment {s} has a non-finite endpoint.");
                }
                if (!(seg.Radius > 0f) || float.IsNaN(seg.Radius) || float.IsInfinity(seg.Radius))
                {
                    throw new DomainException($"Segment {s} must have a finite positive Radius.");
                }
            }

            var result = new VertexInfluence[restVertices.Count][];
            var weightBySegment = new float[segments.Count];
            var candidates = new List<int>(segments.Count);

            for (int v = 0; v < restVertices.Count; v++)
            {
                Vector3 vertex = restVertices[v];
                if (!NumericValidity.IsFinite(vertex))
                {
                    throw new DomainException($"Rest vertex {v} is not finite.");
                }

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
                    weightBySegment[s] = falloff > 0f
                        ? Mathf.Pow(falloff, WeightFalloffPower)
                        : 0f;
                    if (weightBySegment[s] > 0f) candidates.Add(s);
                }

                if (candidates.Count == 0)
                {
                    throw new DomainException(
                        $"Rest vertex {v} has no eligible bone segment within influence range.");
                }

                // Deterministic selection: weight descending, BoneIndex ascending. Ties
                // therefore resolve identically regardless of the order the segments were
                // stored in, which keeps the result invariant to segment ordering.
                candidates.Sort((int left, int right) =>
                {
                    int byWeight = weightBySegment[right].CompareTo(weightBySegment[left]);
                    if (byWeight != 0) return byWeight;
                    return segments[left].BoneIndex.CompareTo(segments[right].BoneIndex);
                });

                int take = Math.Min(MaxInfluencesPerVertex, candidates.Count);
                float totalWeight = 0f;
                for (int i = 0; i < take; i++) totalWeight += weightBySegment[candidates[i]];
                if (!(totalWeight > 0f))
                {
                    throw new DomainException($"Rest vertex {v} has no net bone weight.");
                }

                var influences = new VertexInfluence[take];
                for (int i = 0; i < take; i++)
                {
                    int segmentIndex = candidates[i];
                    influences[i] = new VertexInfluence(
                        segments[segmentIndex].BoneIndex,
                        weightBySegment[segmentIndex] / totalWeight);
                }
                result[v] = influences;
            }

            return result;
        }

        /// <summary>
        /// The per-vertex, pre-selection influence weight of one segment against a rest
        /// vertex, exposed for direct unit testing of the falloff. Uses the closest
        /// point on the segment axis, so distance is to the geometry that generated the
        /// surface, not to a bone center.
        /// </summary>
        public static float WeightFor(Vector3 vertex, BoneSegmentInfluence segment)
        {
            float distance = DistanceToSegment(vertex, segment.Start, segment.End);
            float effectiveRadius = segment.Radius * RadiusScale;
            float falloff = 1f - distance / effectiveRadius;
            return falloff > 0f ? Mathf.Pow(falloff, WeightFalloffPower) : 0f;
        }

        /// <summary>Squared distance from <paramref name="point"/> to the finite segment
        /// <c>[a, b]</c>, by closest-point projection.</summary>
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

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(SqrDistanceToSegment(point, a, b));
        }
    }
}
