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
    /// surface. The nearest resolved part SDF is the existing geometry-to-part
    /// correspondence used by appearance baking; this resolver reuses that
    /// contract for binding and does not infer ownership from mesh names or
    /// Unity hierarchy order.
    /// </summary>
    public static class ImplicitSurfaceInfluenceDomainResolver
    {
        public static InfluenceDomain[] Resolve(
            CreatureDefinition definition,
            ResolvedCreatureSnapshot snapshot,
            IReadOnlyList<Vector3> vertices)
        {
            if (definition == null) throw new DomainException("definition must not be null.");
            if (snapshot == null) throw new DomainException("snapshot must not be null.");
            if (vertices == null) throw new DomainException("vertices must not be null.");

            List<ResolvedPartProgram> parts = SdfProgramBuilder.CompileIndividualPartsPortable(
                definition, snapshot);
            SdfProgram body = SdfProgramBuilder.CompilePortableBodyField(definition, snapshot);
            try
            {
                int scratchLength = Math.Max(1, body.Operations.Length);
                for (int i = 0; i < parts.Count; i++)
                {
                    scratchLength = Math.Max(scratchLength, parts[i].Program.Operations.Length);
                }

                var scratch = new NativeArray<float>(scratchLength, Allocator.Temp);
                try
                {
                    var domains = new InfluenceDomain[vertices.Count];
                    for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
                    {
                        Vector3 vertex = vertices[vertexIndex];
                        if (!NumericValidity.IsFinite(vertex))
                        {
                            throw new DomainException($"Vertex {vertexIndex} is not finite.");
                        }

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
                            domains[vertexIndex] = new InfluenceDomain(CreatureDefinition.BodyId);
                        }
                        else if (hasPart)
                        {
                            domains[vertexIndex] = new InfluenceDomain(
                                ResolvePartDomain(nearestPart, vertex));
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
                foreach (ResolvedPartProgram part in parts) part.Program.Dispose();
                body.Dispose();
            }
        }

        private static string ResolvePartDomain(ResolvedPartSnapshot part, Vector3 vertex)
        {
            if (!part.MirrorAcrossSymmetryPlane) return part.Id;

            Vector3 originalOrigin = part.PartFrameToCreatureSpace.GetColumn(3);
            Vector3 mirroredOrigin = MirrorUtility.ReflectPointAcrossX(originalOrigin);
            float originalDistance = (vertex - originalOrigin).sqrMagnitude;
            float mirroredDistance = (vertex - mirroredOrigin).sqrMagnitude;
            return mirroredDistance < originalDistance
                ? part.Id + SemanticBoneResolver.MirrorSuffix
                : part.Id;
        }
    }
}