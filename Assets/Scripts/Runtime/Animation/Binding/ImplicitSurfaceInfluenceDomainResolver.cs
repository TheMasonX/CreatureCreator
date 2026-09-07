using System;
using System.Collections.Generic;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Sdf;
using ProceduralCreature.Skeleton;
using Unity.Collections;
using UnityEngine;

namespace ProceduralCreature.Animation.Binding
{
    /// <summary>
    /// Resolves build-time influence domains for vertices on the welded implicit
    /// surface. Part ownership remains based on the same resolved SDF correspondence
    /// used by appearance baking. Body vertices additionally get a small local
    /// neighborhood of compact Body bones instead of one global "body" domain, which
    /// prevents distant Body segments from becoming valid influences simply because
    /// their morphology radius is large.
    ///
    /// This is build-time only. It does not add any per-frame work.
    /// </summary>
    public static class ImplicitSurfaceInfluenceDomainResolver
    {
        private const int MaxBodyDomainInfluences = 4;

        public static InfluenceDomain[] Resolve(
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            IReadOnlyList<Vector3> vertices)
        {
            if (definition == null) throw new DomainException("definition must not be null.");
            if (snapshot == null) throw new DomainException("snapshot must not be null.");
            if (vertices == null) throw new DomainException("vertices must not be null.");

            List<ResolvedPartProgram> parts = SdfProgramBuilder.CompileIndividualPartsPortable(definition, snapshot);
            SdfProgram body = SdfProgramBuilder.CompilePortableBodyField(definition, snapshot);
            try
            {
                int scratchLength = Math.Max(1, body.Operations.Length);
                for (int i = 0; i < parts.Count; i++)
                    scratchLength = Math.Max(scratchLength, parts[i].Program.Operations.Length);

                var scratch = new NativeArray<float>(scratchLength, Allocator.Temp);
                try
                {
                    IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bodyRig =
                        AnatomicalBodyRigLayout.Build(snapshot);
                    var domains = new InfluenceDomain[vertices.Count];

                    for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
                    {
                        Vector3 vertex = vertices[vertexIndex];
                        if (!NumericValidity.IsFinite(vertex))
                            throw new DomainException($"Vertex {vertexIndex} is not finite.");

                        float nearest = float.PositiveInfinity;
                        ResolvedPartSnapshot nearestPart = default;
                        bool hasPart = false;
                        for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                        {
                            float distance = Mathf.Abs(SdfProgramEvaluator.Evaluate(
                                parts[partIndex].Program,
                                new Unity.Mathematics.float3(vertex.x, vertex.y, vertex.z),
                                scratch));
                            if (float.IsPositiveInfinity(distance) || distance >= nearest) continue;
                            nearest = distance;
                            nearestPart = parts[partIndex].Part;
                            hasPart = true;
                        }

                        float bodyDistance = Mathf.Abs(SdfProgramEvaluator.Evaluate(
                            body,
                            new Unity.Mathematics.float3(vertex.x, vertex.y, vertex.z),
                            scratch));

                        if (!float.IsPositiveInfinity(bodyDistance) && bodyDistance <= nearest)
                        {
                            domains[vertexIndex] = BuildBodyDomain(bodyRig, vertex);
                        }
                        else if (hasPart)
                        {
                            domains[vertexIndex] = BuildHierarchyDomain(snapshot, nearestPart, vertex);
                        }
                        else
                        {
                            throw new DomainException(
                                $"Vertex {vertexIndex} has no finite resolved body or part domain.");
                        }
                    }
                    return domains;
                }
                finally
                {
                    scratch.Dispose();
                }
            }
            finally
            {
                foreach (ResolvedPartProgram partProgram in parts) partProgram.Program.Dispose();
                body.Dispose();
            }
        }

        private static InfluenceDomain BuildBodyDomain(
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bodyRig,
            Vector3 vertex)
        {
            if (bodyRig == null || bodyRig.Count == 0)
                throw new DomainException("A Body influence domain requires a non-empty compact Body rig.");

            // Keep only the four nearest Body bones. Insertion is O(bodyBones * 4),
            // avoiding a per-vertex List/sort allocation. InfluenceDomain clones the
            // resulting IDs because it is an immutable binding contract.
            var nearestIds = new string[Math.Min(MaxBodyDomainInfluences, bodyRig.Count)];
            var nearestDistances = new float[nearestIds.Length];
            for (int i = 0; i < nearestDistances.Length; i++) nearestDistances[i] = float.PositiveInfinity;

            for (int i = 0; i < bodyRig.Count; i++)
            {
                AnatomicalBodyRigLayout.BoneSpec bone = bodyRig[i];
                float distance = bone.HasSegment
                    ? SqrDistanceToSegment(vertex, bone.Position, bone.EndPosition)
                    : (vertex - bone.Position).sqrMagnitude;

                int insertAt = -1;
                for (int slot = 0; slot < nearestDistances.Length; slot++)
                {
                    if (distance < nearestDistances[slot]
                        || (Mathf.Approximately(distance, nearestDistances[slot])
                            && string.CompareOrdinal(bone.Id, nearestIds[slot]) < 0))
                    {
                        insertAt = slot;
                        break;
                    }
                }
                if (insertAt < 0) continue;

                for (int slot = nearestDistances.Length - 1; slot > insertAt; slot--)
                {
                    nearestDistances[slot] = nearestDistances[slot - 1];
                    nearestIds[slot] = nearestIds[slot - 1];
                }
                nearestDistances[insertAt] = distance;
                nearestIds[insertAt] = bone.Id;
            }

            int validCount = 0;
            while (validCount < nearestIds.Length && nearestIds[validCount] != null) validCount++;
            if (validCount == 0)
                throw new DomainException("Unable to resolve a compact Body influence domain.");

            if (validCount != nearestIds.Length)
            {
                var compactIds = new string[validCount];
                Array.Copy(nearestIds, compactIds, validCount);
                nearestIds = compactIds;
            }

            return new InfluenceDomain(nearestIds[0], nearestIds);
        }

        private static InfluenceDomain BuildHierarchyDomain(
            ResolvedCreatureSnapshot snapshot,
            ResolvedPartSnapshot primaryPart,
            Vector3 vertex)
        {
            bool mirrored = IsMirroredInstance(primaryPart, vertex);
            string primaryDomain = ResolveDomainForInstance(primaryPart, mirrored);
            var allowedDomains = new List<string>(4) { primaryDomain };

            string parentId = primaryPart.ParentId;
            int guard = 0;
            while (!string.IsNullOrEmpty(parentId) && parentId != CreatureDefinition.BodyId)
            {
                if (++guard > snapshot.PartsById.Count)
                {
                    throw new DomainException(
                        $"Resolved parent chain for part '{primaryPart.Id}' exceeds the part count; " +
                        "the resolved hierarchy is cyclic or inconsistent.");
                }
                if (!snapshot.PartsById.TryGetValue(parentId, out ResolvedPartSnapshot parent))
                {
                    throw new DomainException(
                        $"Resolved part '{primaryPart.Id}' references missing parent '{parentId}'.");
                }

                string parentDomain = ResolveDomainForInstance(parent, mirrored);
                if (!allowedDomains.Contains(parentDomain)) allowedDomains.Add(parentDomain);
                parentId = parent.ParentId;
            }

            return new InfluenceDomain(primaryDomain, allowedDomains.ToArray());
        }

        private static bool IsMirroredInstance(ResolvedPartSnapshot part, Vector3 vertex)
        {
            if (!part.MirrorAcrossSymmetryPlane) return false;
            Vector3 originalOrigin = part.PartFrameToCreatureSpace.GetColumn(3);
            Vector3 mirroredOrigin = MirrorUtility.ReflectPointAcrossX(originalOrigin);
            float originalDistance = (vertex - originalOrigin).sqrMagnitude;
            float mirroredDistance = (vertex - mirroredOrigin).sqrMagnitude;
            return mirroredDistance < originalDistance;
        }

        private static string ResolveDomainForInstance(ResolvedPartSnapshot part, bool mirrored)
        {
            return mirrored && part.MirrorAcrossSymmetryPlane
                ? part.Id + SemanticBoneResolver.MirrorSuffix
                : part.Id;
        }

        private static float SqrDistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            if (abSqr <= 1e-12f) return (point - a).sqrMagnitude;
            float t = Vector3.Dot(point - a, ab) / abSqr;
            t = Mathf.Clamp01(t);
            Vector3 closest = a + t * ab;
            return (point - closest).sqrMagnitude;
        }
    }
}
