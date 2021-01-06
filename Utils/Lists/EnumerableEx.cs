using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Lists
{
	public static class EnumerableEx
	{
        public static T GetValueValueOrDefault<K, T>(this IDictionary<K, T> d, K key, T defaultValue = default(T))
        {
            if (d.TryGetValue(key, out T value))
            {
                return value;
            }
            return defaultValue;
        }

        public static IEnumerable<T[]> EnumerateBy<T>(this IEnumerable<T> e, int by)
        {
            T[] result = new T[by];
            var enumerator = e.GetEnumerator();

			for (int i = 0; i < by; i++)
			{
                if (!enumerator.MoveNext())
                {
                    var lastResult = new T[i];
                    Array.Copy(result, lastResult, i);
                    yield return lastResult;
                    yield break;
                }
                result[i] = enumerator.Current;
			}

            var newResult = new T[by];
            while (true)
            {
                yield return result;
                if (!enumerator.MoveNext())
                {
                    yield break;
                }
                Array.Copy(result, 1, newResult, 0, by - 1);
                (result, newResult) = (newResult, result);
                result[by - 1] = enumerator.Current;
            } 

        }

        public static IEnumerable<T> FollowedBy<T>(this IEnumerable<T> enumerable, params T[] elements) 
        {
			foreach (var item in enumerable)
			{
                yield return item;
			}
            foreach (var item in elements)
            {
                yield return item;
            }
        }

        public static IEnumerable<T> PrecededBy<T>(this IEnumerable<T> enumerable, params T[] elements)
        {
            foreach (var item in elements)
            {
                yield return item;
            }
            foreach (var item in enumerable)
            {
                yield return item;
            }
        }
    }
}
