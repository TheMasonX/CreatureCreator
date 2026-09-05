using System;
using System.Collections.Generic;
using ProceduralCreature.Common;
using UnityEngine;

namespace ProceduralCreature.Animation.Binding
{
    /// <summary>
    /// One bone's frame in creature space. An ABSOLUTE frame (position + rotation),
    /// exactly how <c>Skeleton.BoneSnapshot</c> stores <c>Position</c>/<c>Rotation</c>
    /// and how <c>Animation.CreatureRig</c> applies a pose in world space — never a
    /// hierarchy-relative frame. Using absolute frames is what makes per-vertex
    /// linear-blend interpolation well defined across a chain whose bones share no
    /// common parent transform.
    /// </summary>
    public readonly struct BonePose
    {
        /// <summary>Creature-space origin of the bone frame.</summary>
        public readonly Vector3 Position;

        /// <summary>Creature-space orientation of the bone frame.</summary>
        public readonly Quaternion Rotation;

        public BonePose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    /// <summary>
    /// One weighted influence of a single bone on one rest-space vertex.
    /// </summary>
    public readonly struct VertexInfluence
    {
        /// <summary>
        /// Index into the bone order fixed by <c>Skeleton.SkeletonSnapshot</c> capture.
        /// That order is established by the existing semantic resolution
        /// (<c>Skeleton.SemanticBoneResolver</c> / <c>SkeletonInferrer</c>); this module
        /// never maps a part to a bone or re-discovers identity, so a caller hands it
        /// only indices into an already-resolved bone set (see the binding contract).
        /// </summary>
        public readonly int BoneIndex;

        /// <summary>
        /// Non-negative blend weight. Authored weights sum to 1 for a vertex;
        /// <see cref="LinearBlendSkinning.Deform"/> normalizes defensively so a partial
        /// sum never silently becomes a second weighting convention.
        /// </summary>
        public readonly float Weight;

        public VertexInfluence(int boneIndex, float weight)
        {
            BoneIndex = boneIndex;
            Weight = weight;
        }
    }

    /// <summary>
    /// Linear-blend skinning over an already-resolved set of bones. This is the
    /// documented pure-math deformation path that lets generated rest-space geometry
    /// follow posed bones without any <c>SkinnedMeshRenderer</c> or scene object
    /// (CC-073 acceptance: "a <c>SkinnedMeshRenderer</c> or a documented equivalent
    /// deformation path"). It consumes resolved bone frames and returns posed vertex
    /// positions. It authors no DNA, derives no bones, and owns no pose state.
    ///
    /// GEOMETRY BINDING CONTRACT (recorded before implementation choices became the
    /// convention — TSK-0077 / CC-073, C4.1). Conventions:
    ///
    /// * REST-SPACE CONVENTION: each input vertex is stored in creature space at the
    ///   rest/authored pose. Bind offsets are therefore measured from a bone's rest
    ///   frame, never from a mutable current pose. <c>GeneratedCreature</c> is a
    ///   generation result and remains pose-free; poses are supplied per call.
    /// * BONE-INDEX CONVENTION: a bone is addressed by its index in the order fixed by
    ///   <c>Skeleton.SkeletonSnapshot</c> capture, which the existing semantic
    ///   resolution (SkeletonInferrer / SemanticBoneResolver) establishes. Callers map
    ///   resolved anatomy to those indices; this module never maps a part to a bone,
    ///   never searches nearest-bone, and never reads mesh names or Unity transform
    ///   order.
    /// * BIND-POSE CONVENTION: the bind pose is the rest pose (the same frames the
    ///   generated rest mesh is authored in). A bone's bind offset for a vertex is
    ///   <c>inv(rest.Rotation) * (vertex - rest.Position)</c>. Because bones carry no
    ///   scale, bind and posed frames are pure rotation + translation, so the inverse
    ///   is the quaternion inverse — exact, no matrix inversion error.
    /// * WEIGHT CONVENTION: per-vertex influences are non-negative and authored to sum
    ///   to 1; <see cref="MaxBoneInfluencesPerVertex"/> caps standard authored
    ///   influence count. <see cref="Deform"/> normalizes by the total weight so a
    ///   partially-authored sum still yields a unit blend rather than a second,
    ///   unnormalized convention. A vertex with no net weight is an error.
    /// * MIRROR CONVENTION: a mirrored limb flows through the EXISTING semantic
    ///   resolution and <c>Skeleton.MirrorUtility</c>. Positions reflect across the
    ///   X = 0 plane (<c>ReflectPointAcrossX</c>); rotations are conjugated across that
    ///   plane via <c>MirrorAcrossXPlane</c> so the mirrored rotation stays a proper
    ///   rotation. The same bind weights/offsets apply to the reflected geometry. This
    ///   module never re-discovers mirror identity. Deformation commutes with this
    ///   reflection (verified by an algebraic mirror test); the full mirrored-morphology
    ///   numeric proof over a whole <c>CreatureDefinition</c> is deferred to the next
    ///   slice.
    /// * OWNERSHIP BOUNDARY: this module is a consumer of resolved anatomy, not another
    ///   anatomical authority. It holds no skeleton, no <c>CreatureDefinition</c>, no
    ///   <c>GeneratedCreature</c>, and no pose/animation state.
    ///
    /// REST-POSE INVARIANT: when posed equals rest, Deform returns each input vertex
    /// unchanged (per influence, <c>pos + rot * (inv(rot) * (v - pos)) == v</c>, and
    /// unit weights recombine them), within <see cref="RestRoundTripTolerance"/>. The
    /// welded Body surface is deliberately out of scope until its own weighting model
    /// is separately validated.
    /// </summary>
    public static class LinearBlendSkinning
    {
        /// <summary>
        /// Standard ceiling on authored bone influences per vertex (the weight
        /// convention's bind limit). The two-segment fixture uses at most two.
        /// </summary>
        public const int MaxBoneInfluencesPerVertex = 4;

        /// <summary>
        /// Named per-component tolerance for the rest-pose round-trip invariant: a
        /// rest mesh bound and deformed back to the rest pose must reproduce the
        /// original vertices within this distance per component.
        /// </summary>
        public const float RestRoundTripTolerance = 1e-3f;

        /// <summary>
        /// Deforms <paramref name="restVertices"/> (creature-space rest geometry) by
        /// <paramref name="bindings"/> into <paramref name="posed"/> bone frames,
        /// returning one posed creature-space vertex per input vertex.
        /// </summary>
        /// <param name="rest">Rest (bind) bone frames, indexed by <see cref="VertexInfluence.BoneIndex"/>.</param>
        /// <param name="posed">Posed bone frames, same length and order as <paramref name="rest"/>.</param>
        /// <param name="restVertices">Rest-space vertex positions, one per binding.</param>
        /// <param name="bindings">Per-vertex influences; must match <paramref name="restVertices"/> length.</param>
        public static Vector3[] Deform(
            IReadOnlyList<BonePose> rest,
            IReadOnlyList<BonePose> posed,
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<IReadOnlyList<VertexInfluence>> bindings)
        {
            if (rest == null) throw new DomainException("rest must not be null.");
            if (posed == null) throw new DomainException("posed must not be null.");
            if (restVertices == null) throw new DomainException("restVertices must not be null.");
            if (bindings == null) throw new DomainException("bindings must not be null.");
            if (rest.Count != posed.Count)
            {
                throw new DomainException("rest and posed must contain the same number of bones.");
            }
            if (restVertices.Count != bindings.Count)
            {
                throw new DomainException("bindings must contain one entry per rest vertex.");
            }
            if (rest.Count == 0)
            {
                throw new DomainException("At least one bone is required to deform vertices.");
            }

            var inverseRestRotation = new Quaternion[rest.Count];
            for (int bone = 0; bone < rest.Count; bone++)
            {
                inverseRestRotation[bone] = Quaternion.Inverse(rest[bone].Rotation);
            }

            var output = new Vector3[restVertices.Count];
            for (int vertex = 0; vertex < restVertices.Count; vertex++)
            {
                IReadOnlyList<VertexInfluence> influences = bindings[vertex];
                if (influences == null) throw new DomainException($"bindings[{vertex}] must not be null.");
                if (influences.Count == 0)
                {
                    throw new DomainException($"Rest vertex {vertex} has no bone influences.");
                }

                Vector3 restVertex = restVertices[vertex];
                Vector3 blended = Vector3.zero;
                float totalWeight = 0f;
                for (int influenceIndex = 0; influenceIndex < influences.Count; influenceIndex++)
                {
                    VertexInfluence influence = influences[influenceIndex];
                    ValidateInfluence(influence, vertex, rest.Count);

                    // Bind offset from the bone's REST frame (quaternion inverse is exact for
                    // scale-free frames), then carry it into the bone's POSED frame.
                    Vector3 localOffset = inverseRestRotation[influence.BoneIndex]
                        * (restVertex - rest[influence.BoneIndex].Position);
                    BonePose posedBone = posed[influence.BoneIndex];
                    Vector3 posedPosition = posedBone.Position + posedBone.Rotation * localOffset;

                    blended += influence.Weight * posedPosition;
                    totalWeight += influence.Weight;
                }

                if (totalWeight <= 0f || float.IsNaN(totalWeight) || float.IsInfinity(totalWeight))
                {
                    throw new DomainException($"Rest vertex {vertex} has no net bone weight.");
                }
                output[vertex] = blended / totalWeight;
            }
            return output;
        }

        private static void ValidateInfluence(VertexInfluence influence, int vertex, int boneCount)
        {
            if (float.IsNaN(influence.Weight) || float.IsInfinity(influence.Weight))
            {
                throw new DomainException($"bindings[{vertex}] has a non-finite weight.");
            }
            if (influence.Weight < 0f)
            {
                throw new DomainException($"bindings[{vertex}] has a negative weight.");
            }
            if (influence.BoneIndex < 0 || influence.BoneIndex >= boneCount)
            {
                throw new DomainException(
                    $"bindings[{vertex}] references bone {influence.BoneIndex} outside the bone set.");
            }
        }
    }
}
