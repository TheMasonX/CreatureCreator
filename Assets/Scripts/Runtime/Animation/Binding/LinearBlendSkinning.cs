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
    ///
    /// INVARIANT (enforced by <see cref="LinearBlendSkinning.Deform"/>): both
    /// <see cref="Position"/> and <see cref="Rotation"/> must be FINITE — no NaN or
    /// Infinity in any component — for every rest and posed frame.
    /// <see cref="LinearBlendSkinning.Deform"/> enforces finiteness only; it does not
    /// enforce that <see cref="Rotation"/> is a normalized (unit-length) rotation.
    /// Rotations are expected to be proper unit rotations, and keeping them normalized
    /// is the caller's responsibility, not re-normalized here.
    /// </summary>
    public readonly struct BonePose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public BonePose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    public readonly struct VertexInfluence
    {
        public readonly int BoneIndex;
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
    /// follow posed bones without any <c>SkinnedMeshRenderer</c> or scene object.
    /// </summary>
    public static class LinearBlendSkinning
    {
        public const int MaxBoneInfluencesPerVertex = 4;
        public const float RestRoundTripTolerance = 1e-3f;

        /// <summary>
        /// Deforms creature-space rest geometry by the supplied absolute bone frames.
        /// Per-vertex influence lists must contain distinct bone indices.
        /// </summary>
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
                ValidateBoneFrame(rest[bone], $"rest[{bone}]");
                ValidateBoneFrame(posed[bone], $"posed[{bone}]");
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
                if (influences.Count > MaxBoneInfluencesPerVertex)
                {
                    throw new DomainException(
                        $"Rest vertex {vertex} has {influences.Count} influences, exceeding " +
                        $"MaxBoneInfluencesPerVertex ({MaxBoneInfluencesPerVertex}).");
                }

                Vector3 restVertex = restVertices[vertex];
                if (!NumericValidity.IsFinite(restVertex))
                {
                    throw new DomainException($"Rest vertex {vertex} is not finite.");
                }
                Vector3 blended = Vector3.zero;
                float totalWeight = 0f;
                for (int influenceIndex = 0; influenceIndex < influences.Count; influenceIndex++)
                {
                    VertexInfluence influence = influences[influenceIndex];
                    ValidateInfluence(influence, vertex, rest.Count);
                    for (int priorInfluenceIndex = 0; priorInfluenceIndex < influenceIndex; priorInfluenceIndex++)
                    {
                        if (influences[priorInfluenceIndex].BoneIndex == influence.BoneIndex)
                        {
                            throw new DomainException(
                                $"bindings[{vertex}] contains duplicate bone index {influence.BoneIndex}.");
                        }
                    }

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

        private static void ValidateBoneFrame(BonePose frame, string label)
        {
            if (!NumericValidity.IsFinite(frame.Position))
            {
                throw new DomainException($"{label} bone frame has a non-finite Position.");
            }
            if (!NumericValidity.IsFinite(frame.Rotation))
            {
                throw new DomainException($"{label} bone frame has a non-finite Rotation.");
            }
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
