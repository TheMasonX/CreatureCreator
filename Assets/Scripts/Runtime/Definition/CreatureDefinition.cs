using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// The authoritative creature model. Every runtime mesh, skeleton, pose, and
    /// editor handle is derived from this (implementation guide "How to Use This
    /// Guide"). Contains no Unity scene objects, no generated mesh/bone data, no
    /// cached SDF arrays (§2.1).
    ///
    /// Current schema version. Bump when the DNA schema changes in a way that
    /// requires migration or explicit version rejection (§2.4 "Definition contains
    /// unsupported schema version").
    /// </summary>
    [Serializable]
    public sealed class CreatureDefinition
    {
        public const int CurrentSchemaVersion = 2;
        public const string BodyId = "body";

        public int SchemaVersion = CurrentSchemaVersion;
        public SymmetryMode SymmetryMode = SymmetryMode.None;
        public BoundsDefinition Bounds = BoundsDefinition.Default;
        public GenerationSettings Generation = GenerationSettings.Default;
        public BodySpline Body = new BodySpline();
        public Vector3 Forward = Vector3.forward;
        public List<CreaturePart> Parts = new List<CreaturePart>();

        public static CreatureDefinition CreateEmpty()
        {
            return new CreatureDefinition();
        }

        /// <summary>
        /// Deep clone. Used at mutation boundaries so in-progress edits (e.g. a
        /// viewport drag) can operate on a scratch copy before being committed
        /// (implementation guide §16: "one mutation path").
        /// </summary>
        public CreatureDefinition Clone()
        {
            return new CreatureDefinition
            {
                SchemaVersion = SchemaVersion,
                SymmetryMode = SymmetryMode,
                Bounds = Bounds,
                Generation = Generation,
                Body = Body == null ? null : Body.Clone(),
                Forward = Forward,
                Parts = (Parts ?? new List<CreaturePart>()).Select(p => p == null ? null : p.Clone()).ToList(),
            };
        }

        public CreaturePartHierarchyIndex CreateHierarchyIndex()
        {
            return new CreaturePartHierarchyIndex(this);
        }

        public CreaturePart FindPart(string id)
        {
            return CreateHierarchyIndex().TryResolve(id, out CreaturePart part) ? part : null;
        }

        /// <summary>
        /// Direct children of the given part ID (or of the implicit root when
        /// parentId is null). Computed on demand rather than cached, so there is
        /// exactly one place parent/child relationships are derived from — caching
        /// this in multiple places was explicitly called out as something to avoid
        /// (implementation guide Sprint 1.1: "without caching mutable authoritative
        /// relationships in multiple places").
        /// </summary>
        public IEnumerable<CreaturePart> GetChildren(string parentId)
        {
            return CreateHierarchyIndex().GetChildren(parentId);
        }

        /// <summary>
        /// True if following ParentId links from every part eventually reaches a
        /// part with ParentId == null (or an already-visited part, which is reported
        /// as a cycle) without looping. Used by DefinitionValidator's "Parent cycle"
        /// check.
        /// </summary>
        public bool HasParentCycle(out List<string> partIdsInCycle)
        {
            partIdsInCycle = new List<string>();

            return CreateHierarchyIndex().HasParentCycle(out partIdsInCycle);
        }

        /// <summary>
        /// Adds a part to the definition. Does NOT canonicalize or validate — callers
        /// go through DefinitionCanonicalizer/DefinitionValidator at the mutation
        /// boundary, keeping this a plain data operation (§16 single mutation path
        /// lives one layer up, in the editor command/service that calls this).
        /// </summary>
        public void AddPart(CreaturePart part)
        {
            if (part == null) throw new Common.DomainException("Cannot add a null CreaturePart.");
            Parts.Add(part);
        }

        public bool RemovePart(string id)
        {
            int index = Parts.FindIndex(p => p.Id == id);
            if (index < 0) return false;
            Parts.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Returns a NEW part that duplicates sourceId's authoring properties as a
        /// child of newParentId (CC-029). The returned part is NOT added to Parts —
        /// callers add it via AddPart so the single mutation boundary (the editor's
        /// MutateDefinition) stays the only place definitions are written.
        ///
        /// Copied: PartType, Shape, Appearance, MirrorAcrossSymmetryPlane, and
        /// DisplayName (the source's authored label; the parts tree disambiguates
        /// duplicates by Id). Fresh: Id (via CloneAsDuplicate), ParentId, Transform
        /// (identity relative to the new parent — the source's parent-local transform
        /// is meaningless under a different parent), and ParentAttachment (null — the
        /// source's anchor references its own parent's body, not the new parent's).
        /// No generated/runtime state is copied. Shape/Appearance/Transform are value
        /// structs, so the clone is independent of the source by construction.
        /// </summary>
        /// <exception cref="Common.DomainException">sourceId is not a part of this definition.</exception>
        public CreaturePart ClonePartAsChild(string sourceId, string newParentId)
        {
            CreaturePart source = FindPart(sourceId);
            if (source == null)
            {
                throw new Common.DomainException(
                    $"Cannot clone unknown part '{sourceId}' as a child.");
            }

            CreaturePart clone = source.CloneAsDuplicate();
            clone.ParentId = newParentId;
            clone.Transform = TransformData.Identity;
            clone.ParentAttachment = null;
            return clone;
        }
    }
}
