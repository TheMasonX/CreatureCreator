using System.Collections.Generic;
using ProceduralCreature.Common;
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
            string sourcePartId,
            bool mirrored,
            IReadOnlyList<Vector3> restVertices)
        {
            if (skeleton == null) throw new DomainException("skeleton must not be null.");
            if (string.IsNullOrEmpty(sourcePartId)) throw new DomainException("sourcePartId must not be empty.");
            if (restVertices == null) throw new DomainException("restVertices must not be null.");

            string boneId = SemanticBoneResolver.ResolvePartRootBoneId(sourcePartId, mirrored);
            if (!skeleton.TryGetIndex(boneId, out int boneIndex))
            {
                throw new DomainException(
                    $"Rigid mesh part '{sourcePartId}' resolved bone '{boneId}', but that bone is absent from the skeleton snapshot.");
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

        /// <summary>
        /// Compatibility overload for callers that still hold authored parts.
        /// New generation code should pass the resolved semantic ID overload so the
        /// binding boundary does not depend on mutable CreaturePart state.
        /// </summary>
        public static VertexInfluence[][] Author(
            SkeletonSnapshot skeleton,
            CreaturePart part,
            bool mirrored,
            IReadOnlyList<Vector3> restVertices)
        {
            if (part == null) throw new DomainException("part must not be null.");
            if (part.Limb != null)
            {
                throw new DomainException("Rigid mesh-asset weights require a non-limb part.");
            }
            return Author(skeleton, part.Id, mirrored, restVertices);
        }
    }
}
