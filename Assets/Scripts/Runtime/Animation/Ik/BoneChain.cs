using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Animation.Ik
{
    /// <summary>
    /// Extracts an ordered (root-first) list of bone ids by walking a Skeleton's
    /// ParentBoneId chain from a leaf bone upward — this is the "adapter" half of
    /// the IK split: FabrikSolver never sees a Bone or a Skeleton, and this class
    /// never does any FABRIK math. See IkChainSolver for where the two meet.
    /// </summary>
    public static class BoneChain
    {
        public static List<string> ExtractChain(Skeleton.Skeleton skeleton, string leafBoneId)
        {
            if (skeleton == null) throw new DomainException("skeleton must not be null.");
            if (string.IsNullOrEmpty(leafBoneId)) throw new DomainException("leafBoneId must not be null or empty.");

            var chain = new List<string>();
            var visited = new HashSet<string>();
            string currentId = leafBoneId;

            while (currentId != null)
            {
                if (!visited.Add(currentId))
                {
                    throw new DomainException(
                        $"Bone '{currentId}' is part of a parent-reference cycle. " +
                        "A Skeleton inferred from a validated CreatureDefinition should never have one.");
                }

                Bone bone = skeleton.FindBone(currentId);
                if (bone == null)
                {
                    throw new DomainException($"Bone '{currentId}' was not found in the skeleton.");
                }

                chain.Add(currentId);
                currentId = bone.ParentBoneId;
            }

            chain.Reverse();
            return chain;
        }

        public static Vector3[] ExtractRestPositions(Skeleton.Skeleton skeleton, IReadOnlyList<string> chainBoneIds)
        {
            if (skeleton == null) throw new DomainException("skeleton must not be null.");
            if (chainBoneIds == null) throw new DomainException("chainBoneIds must not be null.");

            var positions = new Vector3[chainBoneIds.Count];
            for (int i = 0; i < chainBoneIds.Count; i++)
            {
                Bone bone = skeleton.FindBone(chainBoneIds[i]);
                if (bone == null)
                {
                    throw new DomainException($"Bone '{chainBoneIds[i]}' was not found in the skeleton.");
                }
                positions[i] = bone.Position;
            }
            return positions;
        }

        public static float[] ComputeLinkLengths(Vector3[] chainPositions)
        {
            if (chainPositions == null) throw new DomainException("chainPositions must not be null.");
            if (chainPositions.Length < 2)
            {
                throw new DomainException("A chain needs at least 2 positions to compute link lengths.");
            }

            var lengths = new float[chainPositions.Length - 1];
            for (int i = 0; i < lengths.Length; i++)
            {
                lengths[i] = Vector3.Distance(chainPositions[i], chainPositions[i + 1]);
            }
            return lengths;
        }
    }
}
