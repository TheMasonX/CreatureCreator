using System.Collections.Generic;
using System.Linq;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Strategy for ordering sibling parts in the parts tree. Presentation-only:
    /// it never affects creature DNA, validation, canonicalization, or
    /// serialization — swapping the active strategy only changes how the tree
    /// orders sibling nodes. The strategy pattern keeps the ordering policy
    /// swappable (e.g. a future grouped sort via OrderBy().ThenBy()).
    /// </summary>
    internal interface IPartSiblingOrderer
    {
        IEnumerable<CreaturePart> OrderSiblings(IEnumerable<CreaturePart> parts);
    }

    /// <summary>
    /// Simple alphabetical order by the author-facing label (DisplayName),
    /// falling back to the stable Id for ties. This is the default strategy.
    /// </summary>
    internal sealed class AlphabeticalPartSiblingOrderer : IPartSiblingOrderer
    {
        public IEnumerable<CreaturePart> OrderSiblings(IEnumerable<CreaturePart> parts)
        {
            return parts
                .OrderBy(p => DisplayName(p), System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p?.Id, System.StringComparer.Ordinal);
        }

        private static string DisplayName(CreaturePart part)
        {
            return part == null || string.IsNullOrWhiteSpace(part.DisplayName)
                ? string.Empty
                : part.DisplayName;
        }
    }

    /// <summary>
    /// Grouped order: part type first, then alphabetical within each type
    /// (OrderBy().ThenBy()). Demonstrates the extensibility the strategy pattern
    /// exists for; not the active default.
    /// </summary>
    internal sealed class GroupedPartSiblingOrderer : IPartSiblingOrderer
    {
        public IEnumerable<CreaturePart> OrderSiblings(IEnumerable<CreaturePart> parts)
        {
            return parts
                .OrderBy(p => p?.PartType)
                .ThenBy(p => DisplayName(p), System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p?.Id, System.StringComparer.Ordinal);
        }

        private static string DisplayName(CreaturePart part)
        {
            return part == null || string.IsNullOrWhiteSpace(part.DisplayName)
                ? string.Empty
                : part.DisplayName;
        }
    }

    /// <summary>Selection point for the tree's active sibling orderer.</summary>
    internal static class PartSiblingOrderers
    {
        public static IPartSiblingOrderer Alphabetical { get; } = new AlphabeticalPartSiblingOrderer();

        public static IPartSiblingOrderer Grouped { get; } = new GroupedPartSiblingOrderer();
    }
}
