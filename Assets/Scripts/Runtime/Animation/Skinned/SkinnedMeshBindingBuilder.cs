using System.Collections.Generic;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Animation.Skinned
{
    /// <summary>
    /// Pure build-time conversion of authored binding data into the Unity mesh
    /// skinning arrays a <c>SkinnedMeshRenderer</c> consumes: <c>Mesh.boneWeights</c>
    /// (per-vertex <see cref="BoneWeight"/>, capped at
    /// <see cref="LinearBlendSkinning.MaxBoneInfluencesPerVertex"/>) and
    /// <c>Mesh.bindposes</c> (full <c>Matrix4x4</c> inverse-rest-bone-in-mesh-local).
    ///
    /// This module performs NO authoring (weights come in already authored by
    /// <see cref="ImplicitSurfaceWeightAuthoring"/> / TSK-0131) and NO scene/object
    /// work — it is pure data conversion and is headless-unit-testable. It never
    /// re-derives weights, never re-discovers bone identity, and owns no pose state.
    ///
    /// SPACE CONVENTION (shared with TSK-0131 / the rig): mesh-local ==
    /// creature-space == rig-host-space at identity. A bone's rest frame in mesh
    /// space is <c>TRS(rest.Position, rest.Rotation, one)</c>, so the bind pose is
    /// its exact inverse (bind == rest). Because frames are rigid (rotation +
    /// translation only, no scale) the inverse is an exact rigid inverse.
    ///
    /// DEFORMATION EQUIVALENCE (why bind == rest reproduces the mesh): Unity deforms
    /// a vertex by <c>Σ w_i · bones[i] · bindPose[i] · v</c>. With bindPose[i] =
    /// inv(rest_i) and the rig at rest (<c>bones[i] == rest_i</c>) the identity
    /// <c>bones[i]·bindPose[i] = I</c> returns <c>v</c> unchanged; posed it becomes
    /// the same per-bone carry <c>posed_i · inv(rest_i)</c> that
    /// <see cref="LinearBlendSkinning.Deform"/> computes, which is what makes posed
    /// SMR == LBS oracle possible on the same real geometry.
    /// </summary>
    public static class SkinnedMeshBindingBuilder
    {
        /// <summary>
        /// Computes one bind pose per bone, index-parallel to
        /// <see cref="SkeletonSnapshot.Capture"/> order (the shared bind-index
        /// contract). <c>bindPose[i]</c> is the exact inverse of the bone's rest
        /// frame <c>TRS(Position, Rotation, one)</c> in mesh space (bind == rest).
        /// Every position and rotation component must be finite because a non-finite
        /// rest frame cannot produce a meaningful Unity bind pose.
        /// </summary>
        public static Matrix4x4[] ComputeBindposes(SkeletonSnapshot rest)
        {
            if (rest == null) throw new DomainException("rest skeleton must not be null.");
            if (rest.Count == 0) throw new DomainException("At least one bone is required to bind a mesh.");

            var bindposes = new Matrix4x4[rest.Count];
            for (int bone = 0; bone < rest.Count; bone++)
            {
                BoneSnapshot snapshot = rest[bone];
                if (!NumericValidity.IsFinite(snapshot.Position))
                {
                    throw new DomainException($"Bone {bone} bind position must be finite.");
                }
                if (!NumericValidity.IsFinite(snapshot.Rotation))
                {
                    throw new DomainException($"Bone {bone} bind rotation must be finite.");
                }

                Matrix4x4 restFrame = Matrix4x4.TRS(snapshot.Position, snapshot.Rotation, Vector3.one);
                Matrix4x4 bindPose = restFrame.inverse;
                if (!NumericValidity.IsFinite(bindPose))
                {
                    throw new DomainException($"Bone {bone} bind pose must be finite.");
                }
                bindposes[bone] = bindPose;
            }
            return bindposes;
        }

        /// <summary>
        /// Converts authored per-vertex influences into Unity <see cref="BoneWeight"/>,
        /// one per vertex in the same order. Each influence list is capped at
        /// <see cref="LinearBlendSkinning.MaxBoneInfluencesPerVertex"/>. Indices must
        /// be unique, weights finite and non-negative, and every vertex must have
        /// positive finite total weight; these are the same safety invariants enforced
        /// by <see cref="LinearBlendSkinning.Deform"/> so the Unity adapter cannot widen
        /// the accepted domain of its deformation oracle.
        /// </summary>
        public static BoneWeight[] BuildBoneWeights(IReadOnlyList<VertexInfluence[]> influences, int boneCount)
        {
            if (influences == null) throw new DomainException("influences must not be null.");
            if (boneCount <= 0) throw new DomainException("boneCount must be positive.");

            var boneWeights = new BoneWeight[influences.Count];
            for (int vertex = 0; vertex < influences.Count; vertex++)
            {
                VertexInfluence[] vertexInfluences = influences[vertex];
                if (vertexInfluences == null)
                {
                    throw new DomainException($"Vertex {vertex} has a null influence list.");
                }
                if (vertexInfluences.Length == 0)
                {
                    throw new DomainException($"Vertex {vertex} has no bone influences.");
                }
                if (vertexInfluences.Length > LinearBlendSkinning.MaxBoneInfluencesPerVertex)
                {
                    throw new DomainException(
                        $"Vertex {vertex} has {vertexInfluences.Length} influences, exceeding " +
                        $"MaxBoneInfluencesPerVertex ({LinearBlendSkinning.MaxBoneInfluencesPerVertex}).");
                }

                int fallbackBone = vertexInfluences[0].BoneIndex;
                if (fallbackBone < 0 || fallbackBone >= boneCount)
                {
                    throw new DomainException($"Vertex {vertex} references bone {fallbackBone} outside the bone set.");
                }

                BoneWeight weight = new BoneWeight
                {
                    boneIndex0 = fallbackBone,
                    weight0 = 0f,
                    boneIndex1 = fallbackBone,
                    weight1 = 0f,
                    boneIndex2 = fallbackBone,
                    weight2 = 0f,
                    boneIndex3 = fallbackBone,
                    weight3 = 0f,
                };

                float totalWeight = 0f;
                for (int i = 0; i < vertexInfluences.Length; i++)
                {
                    VertexInfluence influence = vertexInfluences[i];
                    if (influence.BoneIndex < 0 || influence.BoneIndex >= boneCount)
                    {
                        throw new DomainException(
                            $"Vertex {vertex} references bone {influence.BoneIndex} outside the bone set.");
                    }
                    if (!NumericValidity.IsFinite(influence.Weight))
                    {
                        throw new DomainException($"Vertex {vertex} has a non-finite weight.");
                    }
                    if (influence.Weight < 0f)
                    {
                        throw new DomainException($"Vertex {vertex} has a negative weight.");
                    }
                    for (int prior = 0; prior < i; prior++)
                    {
                        if (vertexInfluences[prior].BoneIndex == influence.BoneIndex)
                        {
                            throw new DomainException(
                                $"Vertex {vertex} contains duplicate bone index {influence.BoneIndex}.");
                        }
                    }
                    totalWeight += influence.Weight;
                    if (!NumericValidity.IsFinite(totalWeight))
                    {
                        throw new DomainException($"Vertex {vertex} has a non-finite total weight.");
                    }

                    switch (i)
                    {
                        case 0:
                            weight.boneIndex0 = influence.BoneIndex;
                            weight.weight0 = influence.Weight;
                            break;
                        case 1:
                            weight.boneIndex1 = influence.BoneIndex;
                            weight.weight1 = influence.Weight;
                            break;
                        case 2:
                            weight.boneIndex2 = influence.BoneIndex;
                            weight.weight2 = influence.Weight;
                            break;
                        case 3:
                            weight.boneIndex3 = influence.BoneIndex;
                            weight.weight3 = influence.Weight;
                            break;
                    }
                }

                if (!(totalWeight > 0f))
                {
                    throw new DomainException($"Vertex {vertex} has no net bone weight.");
                }

                boneWeights[vertex] = weight;
            }
            return boneWeights;
        }
    }
}
