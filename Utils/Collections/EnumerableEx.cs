using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Collections;

public static partial class EnumerableEx
{
    /// <summary>
    /// Get a value for the selected <paramref name="key"/> or return <paramref name="defaultValue"/> if it doesn't exists
    /// </summary>
    /// <typeparam name="K"><paramref name="key"/> type</typeparam>
    /// <typeparam name="T"><paramref name="d"/> values type</typeparam>
    /// <param name="d">Dictionary</param>
    /// <param name="key">Key of value to be retrieve</param>
    /// <param name="defaultValue">Default value if not retrieve</param>
    /// <returns></returns>
    public static T GetValueValueOrDefault<K, T>(this IDictionary<K, T> d, K key, T defaultValue = default(T))
    {
        if (d.TryGetValue(key, out T value))
        {
            return value;
        }
        return defaultValue;
    }

    /// <summary>
    /// Enumerate the collection with a step of <paramref name="by"/>
    /// </summary>
    /// <typeparam name="T"><paramref name="e"/> values type</typeparam>
    /// <param name="e">Collection to be read</param>
    /// <param name="by">Step</param>
    /// <param name="skip">Number of values to skip</param>
    /// <returns></returns>
    public static IEnumerable<T[]> SlideEnumerateBy<T>(this IEnumerable<T> e, int by, int skip = 0)
    {
        T[] result = new T[by];
        var enumerator = e.GetEnumerator();

			for (int i = 0; i < skip; i++)
			{
            if (!enumerator.MoveNext()) yield break;
			}

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

    /// <summary>
    /// Indicate if <paramref name="enumerable"/> has more than 1 element
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerable"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static bool HasManyElements<T>(this IEnumerable<T> enumerable)
        => HasAtLeastElements(enumerable, 2);

    /// <summary>
    /// Indicate if <paramref name="enumerable"/> have at least <paramref name="count"/> elements
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerable"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static bool HasAtLeastElements<T>(this IEnumerable<T> enumerable, int count)
    {
        _ = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
        if (enumerable is ICollection<T> collection)
        {
            return collection.Count >= count;
        }
        var enumerator = enumerable.GetEnumerator();
			for (int i = 0; i < count; i++)
			{
            if (!enumerator.MoveNext()) return false;
        }
        return true;
    }


    /// <summary>
    /// Enumerate <paramref name="enumerable"/> then <paramref name="elements"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerable"></param>
    /// <param name="elements"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Enumerate <paramref name="elements"/> then <paramref name="enumerable"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerable"></param>
    /// <param name="elements"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Pack the list, grouping subsequent equals items and their count
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="c"></param>
    /// <returns></returns>
    public static IEnumerable<Pack<T>> Pack<T>(IEnumerable<T> c)
    {
        var e = c.GetEnumerator();
        if (!e.MoveNext()) yield break;
        var lastValue = e.Current;

        var repetition = 1;
        while (e.MoveNext())
        {
            if (!e.Current.Equals(lastValue))
            {
                yield return new Pack<T>(lastValue, repetition);
                repetition = 1;
                lastValue = e.Current;
            }
            else
            {
                repetition++;
            }
        }
        yield return new Pack<T>(lastValue, repetition);
    }

    /// <summary>
    /// Unpack an enumeration previously packed
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="c"></param>
    /// <returns></returns>
    public static IEnumerable<T> Unpack<T>(IEnumerable<Pack<T>> c)
    {
        foreach (var item in c)
        {
            for (int i = 0; i < item.Repetition; i++)
            {
                yield return item.Value;
            }
        }
    }

    /// <summary>
    /// Slice <paramref name="enumeration"/> at each index
    /// </summary>
    /// <typeparam name="T">Type of element in enumeration</typeparam>
    /// <param name="enumeration"><see cref="IEnumerable{T}"/> to slice</param>
    /// <param name="cutIndexes">Indexes at which the enumeration will be sliced</param>
    /// <returns></returns>
    public static IEnumerable<IEnumerable<T>> Slice<T>(IEnumerable<T> enumeration, params int[] cutIndexes)
    {
        List<T> result = new List<T>();
        int index = 0;
        var indexes = new Queue<int>(cutIndexes);
        var nextIndex = indexes.Dequeue();

        var enumerator = enumeration.GetEnumerator();

        bool @continue;
        for (@continue = enumerator.MoveNext(); @continue; @continue = enumerator.MoveNext())
        {
            if (index == nextIndex)
            {
                yield return result.ToArray();
                result.Clear();
                if (indexes.Count == 0)
                {
                    break;
                }
                nextIndex = indexes.Dequeue();
            }
            result.Add(enumerator.Current);
            index++;
        }

        if (@continue)
        {
            do
            {
                result.Add(enumerator.Current);
            } while (enumerator.MoveNext());
        }
        yield return result.ToArray();

    }

    /// <summary>
    /// Flatten <paramref name="enumerations"/> into one enumeration
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerations"><see cref="IEnumerable{IEnumerable{T}}"/> to flatten</param>
    /// <returns></returns>
    public static IEnumerable<T> Flatten<T>(IEnumerable<IEnumerable<T>> enumerations)
    {
        foreach (var enumeration in enumerations)
        {
            foreach (var item in enumeration)
            {
                yield return item;
            }
        }
    }
}
