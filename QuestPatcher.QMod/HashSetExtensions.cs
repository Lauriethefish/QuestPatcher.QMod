using System.Collections.Generic;

namespace QuestPatcher.QMod
{
    internal static class HashSetExtensions
    {
        /// <summary>
        /// Collects the items in <paramref name="enumerable"/> into a HashSet.
        /// Duplicate items are ignored.
        /// </summary>
        /// <param name="enumerable">The list of items to collect from</param>
        /// <typeparam name="T">Type of items</typeparam>
        /// <returns>A HashSet containing the items in <paramref name="enumerable"/></returns>
        internal static HashSet<T> ToHashSet<T>(this IEnumerable<T> enumerable)
        {
            HashSet<T> result = new HashSet<T>();

            foreach(T t in enumerable)
            {
                result.Add(t);
            }

            return result;
        }
    }
}