using System.Collections.Generic;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Animation.Binding
{
    /// <summary>
    /// Authors the pose-independent bind weights for a rigid mesh-asset item.
    /// A rigid item follows one semantic part bone, so every vertex receives one
    /// normalized influence in SkeletonSnapshot capture order.
    /// </summary>
    public static class RigidMeshWeightAuthoring
    {
        public static VertexInfluence[][] Author(
            SkeletonSnapshot skeleton,
            CreaturePart part,
            bool mirrored,
            IReadOnlyList<Vector3> restVertices)
        {
            if (skeleton == null) throw new DomainException("skeleton must not be null.");
            if (part == null) throw new DomainException("part must not be null.");
            if (restVertices == null) throw new DomainException("restVertices must not be null.");
            if (part.Limb != null)
            {
                throw new DomainException("Rigid mesh-asset weights require a non-limb part.");
            }

            string boneId = SemanticBoneResolver.ResolvePartRootBoneId(part, mirrored);
            if (!skeleton.TryGetIndex(boneId, out int boneIndex))
            {
                throw new DomainException(
                    $"Rigid mesh part '{part.Id}' resolved bone '{boneId}', but that bone is absent from the skeleton snapshot.");
            }

            var result = new VertexInfluence[restVertices.Count][];
            for (int vertex = 0; vertex < restVertices.Count; vertex++)
            {
                if (!NumericValidity.IsFinite(restVertices[vertex]))
                {
                    throw new DomainException($"Rest vertex {vertex} is not finite.");
                }
                result[vertex] = new[] { new VertexInfluence(boneIndex, 1f) };
            }
            return result;
        }
    }
}