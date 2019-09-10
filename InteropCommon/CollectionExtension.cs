using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InteropCommon
{
    /// <summary>
    /// Extension to collection method
    /// </summary>
    public static class CollectionExtension
    {
        /// <summary>
        /// Provides a ToDictionary() like syntax for tuple type
        /// </summary>
        public static IEnumerable<Tuple<T1, T2>> ToTuple<TSource, T1, T2>(this IEnumerable<TSource> source,
            Func<TSource, T1> item1Selector, Func<TSource, T2> item2Selector)
            => source.Select(s => new Tuple<T1, T2>(item1Selector(s), item2Selector(s)));

        /// <summary>
        /// Provides a ToDictionary() like syntax for tuple type
        /// </summary>
        public static IEnumerable<Tuple<T1, T2, T3>> ToTuple<TSource, T1, T2, T3>(this IEnumerable<TSource> source,
            Func<TSource, T1> item1Selector, Func<TSource, T2> item2Selector, Func<TSource, T3> item3Selector)
            => source.Select(s => new Tuple<T1, T2, T3>(item1Selector(s), item2Selector(s), item3Selector(s)));

        /// <summary>
        /// Provides a ToDictionary() like syntax for tuple type
        /// </summary>
        public static IEnumerable<Tuple<T1, T2, T3, T4>> ToTuple<TSource, T1, T2, T3, T4>(this IEnumerable<TSource> source,
            Func<TSource, T1> item1Selector, Func<TSource, T2> item2Selector, Func<TSource, T3> item3Selector, Func<TSource, T4> item4Selector)
            => source.Select(s => new Tuple<T1, T2, T3, T4>(item1Selector(s), item2Selector(s), item3Selector(s), item4Selector(s)));
    }
}
