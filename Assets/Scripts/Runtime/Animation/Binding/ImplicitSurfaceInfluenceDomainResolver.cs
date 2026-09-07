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
                    SkeletonModelSnapshot bodyRig = BuildBodyRigSnapshot(snapshot);
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

        private static InfluenceDomain BuildBodyDomain(SkeletonModelSnapshot bodyRig, Vector3 vertex)
        {
            var candidates = new List<BodyCandidate>(bodyRig.Count);
            for (int i = 0; i < bodyRig.Count; i++)
            {
                BodyBone bone = bodyRig[i];
                float distance = bone.HasSegment
                    ? SqrDistanceToSegment(vertex, bone.Start, bone.End)
                    : (vertex - bone.Position).sqrMagnitude;
                candidates.Add(new BodyCandidate(bone.Id, distance));
            }

            candidates.Sort((left, right) =>
            {
                int byDistance = left.Distance.CompareTo(right.Distance);
                return byDistance != 0
                    ? byDistance
                    : string.CompareOrdinal(left.Id, right.Id);
            });

            int take = Math.Min(MaxBodyDomainInfluences, candidates.Count);
            var allowed = new string[take];
            for (int i = 0; i < take; i++) allowed[i] = candidates[i].Id;
            string primary = take > 0 ? allowed[0] : AnatomicalBodyRigLayout.PelvisBoneId;
            return new InfluenceDomain(primary, allowed);
        }

        private static SkeletonModelSnapshot BuildBodyRigSnapshot(ResolvedCreatureSnapshot snapshot)
        {
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> specs =
                AnatomicalBodyRigLayout.Build(snapshot);
            var result = new SkeletonModelSnapshot(specs.Count);
            for (int i = 0; i < specs.Count; i++)
            {
                AnatomicalBodyRigLayout.BoneSpec spec = specs[i];
                result.Add(new BodyBone(spec.Id, spec.Position, spec.EndPosition, spec.HasSegment));
            }
            return result;
        }

        private readonly struct BodyCandidate
        {
            public readonly string Id;
            public readonly float Distance;
            public BodyCandidate(string id, float distance) { Id = id; Distance = distance; }
        }

        private readonly struct BodyBone
        {
            public readonly string Id;
            public readonly Vector3 Position;
            public readonly Vector3 Start;
            public readonly Vector3 End;
            public readonly bool HasSegment;
            public BodyBone(string id, Vector3 position, Vector3 end, bool hasSegment)
            {
                Id = id; Position = position; Start = position; End = end; HasSegment = hasSegment;
            }
        }

        private sealed class SkeletonModelSnapshot
        {
            private readonly List<BodyBone> _bones;
            public int Count => _bones.Count;
            public BodyBone this[int index] => _bones[index];
            public SkeletonModelSnapshot(int capacity) { _bones = new List<BodyBone>(capacity); }
            public void Add(BodyBone bone) => _bones.Add(bone);
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
