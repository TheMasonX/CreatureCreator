using System;
using System.Collections.Generic;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Tolerant, first-wins view of part relationships. The authoritative Parts
    /// list remains unchanged; this index preserves malformed entries for
    /// diagnostics while making lookup and traversal total.
    ///
    /// The index is a DETACHED snapshot of the parts list taken at construction
    /// (CC-089 read-only aliasing). The <see cref="Parts"/> surface is a private
    /// copy, never the definition's live list, so a caller cannot mutate the
    /// definition through this "read-only" view, and later mutation of
    /// <c>definition.Parts</c> cannot change what the cached first-wins and
    /// children maps report. Validation and canonicalization always build a
    /// fresh index, so the copy cost is confined to the malformed-input boundary.
    /// </summary>
    public sealed class CreaturePartHierarchyIndex
    {
        private readonly Dictionary<string, CreaturePart> _firstById =
            new Dictionary<string, CreaturePart>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<CreaturePart>> _childrenByParent =
            new Dictionary<string, List<CreaturePart>>(StringComparer.Ordinal);
        private readonly List<string> _duplicateIds = new List<string>();
        private readonly IReadOnlyList<CreaturePart> _parts;

        public IReadOnlyList<CreaturePart> Parts => _parts;
        public IReadOnlyList<string> DuplicateIds => _duplicateIds;
        public bool HasNullEntries { get; }

        public CreaturePartHierarchyIndex(CreatureDefinition definition)
        {
            if (definition == null) throw new Common.DomainException("definition must not be null.");

            // Detached snapshot: copy the authoritative list once so the index is
            // immune to later definition mutation and exposes no live alias. The
            // read-only wrapper also blocks downcast mutation through Parts.
            List<CreaturePart> source = definition.Parts;
            CreaturePart[] snapshot = source == null
                ? Array.Empty<CreaturePart>()
                : source.ToArray();
            _parts = Array.AsReadOnly(snapshot);

            bool hasNullEntries = false;
            foreach (CreaturePart part in _parts)
            {
                if (part == null)
                {
                    hasNullEntries = true;
                    continue;
                }

                if (part.Id != null)
                {
                    if (_firstById.ContainsKey(part.Id))
                    {
                        if (!_duplicateIds.Contains(part.Id)) _duplicateIds.Add(part.Id);
                    }
                    else
                    {
                        _firstById.Add(part.Id, part);
                    }
                }

                string parentKey = part.ParentId ?? string.Empty;
                if (!_childrenByParent.TryGetValue(parentKey, out List<CreaturePart> children))
                {
                    children = new List<CreaturePart>();
                    _childrenByParent.Add(parentKey, children);
                }
                children.Add(part);
            }

            HasNullEntries = hasNullEntries;
        }

        public bool TryResolve(string id, out CreaturePart part)
        {
            if (string.IsNullOrEmpty(id))
            {
                part = null;
                return false;
            }
            return _firstById.TryGetValue(id, out part);
        }

        public IReadOnlyList<CreaturePart> GetChildren(string parentId)
        {
            if (_childrenByParent.TryGetValue(parentId ?? string.Empty, out List<CreaturePart> children))
            {
                return children;
            }
            return Array.Empty<CreaturePart>();
        }

        public bool HasParentCycle(out List<string> partIdsInCycle)
        {
            partIdsInCycle = new List<string>();
            foreach (CreaturePart part in _parts)
            {
                if (part == null || string.IsNullOrEmpty(part.Id)) continue;

                var visited = new HashSet<string>(StringComparer.Ordinal);
                string currentId = part.Id;
                bool cycleDetected = false;
                while (true)
                {
                    if (!visited.Add(currentId))
                    {
                        cycleDetected = true;
                        break;
                    }
                    if (!TryResolve(currentId, out CreaturePart current)) break;
                    if (current.ParentId == null || current.ParentId == CreatureDefinition.BodyId) break;
                    currentId = current.ParentId;
                }

                if (cycleDetected)
                {
                    if (!partIdsInCycle.Contains(part.Id)) partIdsInCycle.Add(part.Id);
                }
            }
            return partIdsInCycle.Count > 0;
        }
    }
}