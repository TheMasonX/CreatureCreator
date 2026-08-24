using System.Collections.Generic;
using ProceduralCreature.Definition;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    /// <summary>
    /// Multi-item output of creature generation (CC-031). A creature is no longer a
    /// single Mesh: it is a deterministic, ordered collection of geometry items.
    /// Item 0 is always the implicit combined surface (Body + Shape/Limb parts)
    /// when one exists; mesh-asset and procedural items follow in ascending
    /// SourcePartId order.
    /// </summary>
    public sealed class GeneratedCreature
    {
        /// <summary>SourcePartId used by the implicit combined surface item — no single part owns it.</summary>
        public const string ImplicitSurfaceSourceId = "";

        /// <summary>Suffix on a mirrored item's SourcePartId (matches SkeletonInferrer.MirrorSuffix).</summary>
        public const string MirrorSuffix = "_mirror";

        public List<GeometryItem> Geometry { get; } = new List<GeometryItem>();

        public int Count => Geometry.Count;

        /// <summary>The implicit surface mesh — item 0. Null for an empty result.</summary>
        public Mesh MainMesh => Geometry.Count > 0 ? Geometry[0].Mesh : null;

        public bool TryFindGeometryForPart(string partId, out GeometryItem item)
        {
            for (int i = 0; i < Geometry.Count; i++)
            {
                if (Geometry[i].SourcePartId == partId)
                {
                    item = Geometry[i];
                    return true;
                }
            }
            item = null;
            return false;
        }
    }

    /// <summary>
    /// One generated mesh within a GeneratedCreature (CC-031).
    /// </summary>
    public sealed class GeometryItem
    {
        public string SourcePartId;
        public GeometryType GeometryType;
        public Mesh Mesh;
        public List<MaterialRegion> MaterialRegions = new List<MaterialRegion>();
        public RigBindingMetadata RigBinding = new RigBindingMetadata();
    }

    /// <summary>
    /// A submaterial assignment on a geometry item (CC-031). Pass 1 keeps the list
    /// empty — implicit items use the vertex-color bake and mesh-asset items carry
    /// their own source materials. The type exists so CC-028's material palette can
    /// populate it without changing the output model.
    /// </summary>
    public sealed class MaterialRegion
    {
        public int StartIndex;
        public int IndexCount;
        public string MaterialKey;
    }

    /// <summary>
    /// Semantic rig binding for a geometry item (CC-031). Surface attachment and rig
    /// attachment stay separate: this records what the item follows during
    /// animation, derived from the semantic skeleton (CC-018) — never from the
    /// render mesh. Pass 1 records the source and parent part ids; resolving the
    /// exact bone id reuses SkeletonInferrer.ResolveParentBoneId in a later pass.
    /// </summary>
    public sealed class RigBindingMetadata
    {
        public string SourcePartId;
        public string ParentPartId;
    }
}
