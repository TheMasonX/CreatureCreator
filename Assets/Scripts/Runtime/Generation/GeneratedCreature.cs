using System;
using System.Collections.Generic;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    /// <summary>
    /// Multi-item output of creature generation (CC-031). A creature is no longer a
    /// single Mesh: it is a deterministic, ordered collection of geometry items.
    /// The implicit combined surface (Body + Shape/Limb parts) is identified
    /// semantically via <see cref="TryGetImplicitSurface"/> (and <see cref="MainMesh"/>,
    /// retained as compatibility); mesh-asset and procedural items follow in
    /// ascending SourcePartId order.
    ///
    /// The collection is read-only once built: the only construction path is the
    /// internal <see cref="AddGeometry"/> choke point, which only
    /// <c>CreatureMeshGenerator.Assemble</c> drives. Consumers must not mutate a
    /// GeneratedCreature after creation (TSK-0125).
    /// </summary>
    public sealed class GeneratedCreature
    {
        /// <summary>SourcePartId used by the implicit combined surface item — no single part owns it.</summary>
        public const string ImplicitSurfaceSourceId = "";

        /// <summary>Suffix on a mirrored item's SourcePartId (matches SkeletonInferrer.MirrorSuffix).</summary>
        public const string MirrorSuffix = "_mirror";

        private readonly List<GeometryItem> _geometry = new List<GeometryItem>();

        /// <summary>Read-only, deterministic, ordered geometry items. New code must not depend on positional item 0.</summary>
        public IReadOnlyList<GeometryItem> Geometry => _geometry;

        public int Count => _geometry.Count;

        /// <summary>
        /// Compatibility accessor for the implicit combined surface mesh. Retained
        /// only until callers migrate to <see cref="TryGetImplicitSurface"/>; new
        /// code should prefer the semantic accessor over positional item 0.
        /// </summary>
        public Mesh MainMesh => TryGetImplicitSurface(out GeometryItem item) ? item.Mesh : null;

        /// <summary>
        /// Single internal construction path (TSK-0125). Only
        /// <c>CreatureMeshGenerator.Assemble</c> may drive construction of a
        /// generated creature. Validates that a null item never enters the
        /// collection.
        /// </summary>
        internal void AddGeometry(GeometryItem item)
        {
            if (item == null) throw new DomainException("geometry item must not be null.");
            _geometry.Add(item);
        }

        /// <summary>
        /// Returns the semantic implicit-surface item (the combined Body + Shape/
        /// Limb field). False when no implicit surface was generated.
        /// </summary>
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

        /// <summary>
        /// Finds the geometry item whose SourcePartId matches (exact ordinal). This
        /// is deliberately an O(n) scan rather than a keyed dictionary: no real
        /// consumer performs repeated part lookups on a single GeneratedCreature
        /// today, so a dictionary would add per-creature memory for no measured
        /// win. Revisit only if a consumer shows repeated lookups (TSK-0125).
        /// </summary>
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

    /// <summary>
    /// One generated mesh within a GeneratedCreature (CC-031). Immutable after
    /// construction (TSK-0125): every field is fixed by the constructor, so a
    /// malformed item (for example a null mesh) cannot be built through the single
    /// generator factory.
    /// </summary>
    public sealed class GeometryItem
    {
        public string SourcePartId { get; }

        public GeometryType GeometryType { get; }

        public Mesh Mesh { get; }

        /// <summary>Original mesh-asset source in its authored local space (null for the implicit surface).</summary>
        public Mesh SourceMesh { get; }

        /// <summary>Authored source-to-creature rest placement for mesh assets (identity for the implicit surface).</summary>
        public Matrix4x4 RestPlacement { get; }

        public IReadOnlyList<MaterialRegion> MaterialRegions { get; }

        public RigBindingMetadata RigBinding { get; }

        internal GeometryItem(
            string sourcePartId,
            GeometryType geometryType,
            Mesh mesh,
            Mesh sourceMesh,
            Matrix4x4 restPlacement,
            IReadOnlyList<MaterialRegion> materialRegions,
            RigBindingMetadata rigBinding)
        {
            if (mesh == null) throw new DomainException("geometry item mesh must not be null.");
            if (sourcePartId == null) throw new DomainException("geometry item source part id must not be null.");
            SourcePartId = sourcePartId;
            GeometryType = geometryType;
            Mesh = mesh;
            SourceMesh = sourceMesh;
            RestPlacement = restPlacement;
            MaterialRegions = materialRegions ?? Array.Empty<MaterialRegion>();
            RigBinding = rigBinding;
        }
    }

    /// <summary>
    /// A submaterial assignment on a geometry item (CC-031, TSK-0125). Model 1
    /// (ADR-009): a region addresses one explicit <see cref="SubmeshIndex"/> and a
    /// contiguous [<see cref="StartIndex"/>, StartIndex + <see cref="IndexCount"/>)
    /// range of that submesh's indices, carrying a material key. Implicit items
    /// keep an empty list (vertex-color bake path); a mesh-asset item with a
    /// submaterial override emits one region per submesh so the material coverage
    /// is deterministic and complete, with no ambiguity about which indices it
    /// refers to.
    /// </summary>
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
            if (materialKey == null) throw new DomainException("material region material key must not be null.");
            SubmeshIndex = submeshIndex;
            StartIndex = startIndex;
            IndexCount = indexCount;
            MaterialKey = materialKey;
        }
    }

    /// <summary>
    /// Semantic rig binding for a geometry item (CC-031). Surface attachment and rig
    /// attachment stay separate: this records what the item follows during
    /// animation, derived from the semantic skeleton (CC-018) — never from the
    /// render mesh. Records the source and parent part ids; resolving the exact
    /// bone id reuses SkeletonInferrer.ResolveParentBoneId in a later pass.
    /// Immutable after construction (TSK-0125).
    /// </summary>
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
