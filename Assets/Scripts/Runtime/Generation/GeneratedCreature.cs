using System;
using System.Collections.Generic;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    /// <summary>
    /// Multi-item output of creature generation (CC-031). A creature is no longer a
    /// single Mesh: it is a deterministic, ordered collection of geometry items.
    /// The implicit combined surface (Body + Shape/Limb parts) is identified
    /// semantically via <see cref="TryGetImplicitSurface"/>; mesh-asset and
    /// procedural items follow in ascending SourcePartId order.
    ///
    /// The collection is read-only once built: the only construction path is the
    /// internal <see cref="AddGeometry"/> choke point, which only
    /// <c>CreatureMeshGenerator.Assemble</c> drives. Consumers must not mutate a
    /// GeneratedCreature after creation (TSK-0125).
    /// </summary>
    public sealed class GeneratedCreature
    {
        public const string ImplicitSurfaceSourceId = "";
        public const string MirrorSuffix = "_mirror";

        private readonly List<GeometryItem> _geometry = new List<GeometryItem>();
        public IReadOnlyList<GeometryItem> Geometry => _geometry;
        public int Count => _geometry.Count;

        internal void AddGeometry(GeometryItem item)
        {
            if (item == null) throw new DomainException("geometry item must not be null.");
            _geometry.Add(item);
        }

        public bool TryGetImplicitSurface(out GeometryItem item)
        {
            for (int i = 0; i < _geometry.Count; i++)
            {
                if (_geometry[i].GeometryType == GeometryType.Implicit)
                {
                    item = _geometry[i];
                    return true;
                }
            }
            item = null;
            return false;
        }

        public bool TryFindGeometryForPart(string partId, out GeometryItem item)
        {
            for (int i = 0; i < _geometry.Count; i++)
            {
                if (_geometry[i].SourcePartId == partId)
                {
                    item = _geometry[i];
                    return true;
                }
            }
            item = null;
            return false;
        }
    }

    public sealed class GeometryItem
    {
        public string SourcePartId { get; }
        public GeometryType GeometryType { get; }
        public Mesh Mesh { get; }
        public Mesh SourceMesh { get; }
        public Matrix4x4 RestPlacement { get; }
        public IReadOnlyList<MaterialRegion> MaterialRegions { get; }
        public RigBindingMetadata RigBinding { get; }
        public IReadOnlyList<VertexInfluence[]> VertexInfluences { get; }

        internal GeometryItem(
            string sourcePartId,
            GeometryType geometryType,
            Mesh mesh,
            Mesh sourceMesh,
            Matrix4x4 restPlacement,
            IReadOnlyList<MaterialRegion> materialRegions,
            RigBindingMetadata rigBinding,
            IReadOnlyList<VertexInfluence[]> vertexInfluences = null)
        {
            if (mesh == null) throw new DomainException("geometry item mesh must not be null.");
            if (sourcePartId == null) throw new DomainException("geometry item source part id must not be null.");

            MaterialRegions = materialRegions == null
                ? (IReadOnlyList<MaterialRegion>)Array.Empty<MaterialRegion>()
                : new List<MaterialRegion>(materialRegions).AsReadOnly();
            ValidateMaterialRegions(mesh);

            SourcePartId = sourcePartId;
            GeometryType = geometryType;
            Mesh = mesh;
            SourceMesh = sourceMesh;
            RestPlacement = restPlacement;
            RigBinding = rigBinding;
            VertexInfluences = CloneInfluences(vertexInfluences);
        }

        private static IReadOnlyList<VertexInfluence[]> CloneInfluences(
            IReadOnlyList<VertexInfluence[]> influences)
        {
            if (influences == null || influences.Count == 0)
            {
                return Array.Empty<VertexInfluence[]>();
            }

            var copy = new VertexInfluence[influences.Count][];
            for (int i = 0; i < influences.Count; i++)
            {
                if (influences[i] == null)
                {
                    throw new DomainException($"geometry item vertex influences {i} must not be null.");
                }
                copy[i] = (VertexInfluence[])influences[i].Clone();
            }
            return copy;
        }

        private void ValidateMaterialRegions(Mesh mesh)
        {
            if (MaterialRegions.Count == 0) return;

            int maxSubmeshIndex = Mathf.Max(0, mesh.subMeshCount - 1);
            for (int i = 0; i < MaterialRegions.Count; i++)
            {
                MaterialRegion region = MaterialRegions[i];
                if (region == null)
                {
                    throw new DomainException($"geometry item material region {i} must not be null.");
                }
                if (region.SubmeshIndex < 0 || region.SubmeshIndex > maxSubmeshIndex)
                {
                    throw new DomainException(
                        $"geometry item material region {i} submesh index {region.SubmeshIndex} is out of range for mesh submesh count {mesh.subMeshCount}.");
                }

                int[] submeshTriangles = mesh.GetTriangles(region.SubmeshIndex);
                int maxStart = submeshTriangles.Length;
                if (region.StartIndex < 0 || region.StartIndex > maxStart)
                {
                    throw new DomainException(
                        $"geometry item material region {i} start index {region.StartIndex} is outside the valid range [0, {maxStart}] for submesh {region.SubmeshIndex}.");
                }
                if (region.IndexCount < 0 || region.StartIndex + region.IndexCount > maxStart)
                {
                    throw new DomainException(
                        $"geometry item material region {i} range [{region.StartIndex}, {region.StartIndex + region.IndexCount}) exceeds submesh {region.SubmeshIndex} length {maxStart}.");
                }
            }
        }
    }

    public sealed class MaterialRegion
    {
        public int SubmeshIndex { get; }
        public int StartIndex { get; }
        public int IndexCount { get; }
        public string MaterialKey { get; }

        internal MaterialRegion(int submeshIndex, int startIndex, int indexCount, string materialKey)
        {
            if (indexCount < 0) throw new DomainException("material region index count must not be negative.");
            if (startIndex < 0) throw new DomainException("material region start index must not be negative.");
            if (submeshIndex < 0) throw new DomainException("material region submesh index must not be negative.");
            if (string.IsNullOrWhiteSpace(materialKey)) throw new DomainException("material region material key must not be null or whitespace.");
            SubmeshIndex = submeshIndex;
            StartIndex = startIndex;
            IndexCount = indexCount;
            MaterialKey = materialKey;
        }
    }

    public sealed class RigBindingMetadata
    {
        public string SourcePartId { get; }
        public string ParentPartId { get; }
        public bool IsMirrored { get; }

        internal RigBindingMetadata(string sourcePartId, string parentPartId, bool isMirrored)
        {
            SourcePartId = sourcePartId;
            ParentPartId = parentPartId;
            IsMirrored = isMirrored;
        }
    }
}
