using System.Collections.Concurrent;

namespace VisaSponsorshipScoutBackgroundJob.Core.Extensions
{
    internal static class CollectionExtensions
    {
        internal static bool IsEqualTo(this List<string> source, List<string> target)
        {
            if (source == null && target == null)
            {
                return true;
            }

            if (source == null || target == null || source.Count != target.Count)
            {
                return false;
            }

            return source.SequenceEqual(target);
        }

        internal static ConcurrentDictionary<TKey, TValue> ToConcurrentDictionary<TSource, TKey, TValue>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector)
        {
            var dictionary = new ConcurrentDictionary<TKey, TValue>();
            foreach (var item in source)
            {
                dictionary.TryAdd(keySelector(item), valueSelector(item));
            }
            return dictionary;
        }

        internal static ConcurrentDictionary<TKey, TSource> ToConcurrentDictionary<TSource, TKey>(this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            return source.ToConcurrentDictionary(keySelector, item => item);
        }
    }
}