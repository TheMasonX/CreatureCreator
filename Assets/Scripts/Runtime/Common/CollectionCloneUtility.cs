using System;
using System.Collections.Generic;

namespace ProceduralCreature.Common
{
    /// <summary>
    /// Small shared helpers for cloning mutable authoring collections without
    /// accidentally changing their null-state contract.
    /// </summary>
    internal static class CollectionCloneUtility
    {
        /// <summary>
        /// Deep-clones a nullable list while preserving a null source list and null
        /// elements. The selector is invoked only for non-null elements.
        /// </summary>
        public static List<T> DeepClone<T>(List<T> source, Func<T, T> clone)
            where T : class
        {
            if (source == null)
            {
                return null;
            }
            if (clone == null)
            {
                throw new ArgumentNullException(nameof(clone));
            }

            var result = new List<T>(source.Count);
            foreach (T item in source)
            {
                result.Add(item == null ? null : clone(item));
            }
            return result;
        }
    }
}
