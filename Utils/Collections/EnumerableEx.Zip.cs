﻿using System.Collections;
using Utils.Objects;

namespace Utils.Collections;

public static partial class EnumerableEx
{
    /// <summary>
    /// Returns an element from a dictionary with the specific key or <paramref name="defaultValue"/> if if doesn't exists
    /// </summary>
    /// <typeparam name="TKey">Key type</typeparam>
    /// <typeparam name="TValue">Value type</typeparam>
    /// <param name="dictionary">Dictionary</param>
    /// <param name="key">Key whose value to get</param>
    /// <param name="defaultValue">Default value</param>
    /// <returns>Value of specific key or default value</returns>
    /// <exception cref="ArgumentNullException">Raise if dictionary is null</exception>
    public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
    {
        dictionary.Arg().MustNotBeNull();
        if (dictionary.TryGetValue(key, out TValue result)) return result;
        return defaultValue;
    }

    /// <summary>
    /// Returns true if collection is null or has no elements
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static bool IsNullOrEmptyCollection<T>(this IEnumerable<T> coll) => coll is null || !coll.Any();

    /// <summary>
    /// Read several <see cref="IEnumerable"/> in parallel and returns an array of object of elments from the same indexes
    /// </summary>
    /// <param name="enumerations"><see cref="IEnumerable"/> to be read</param>
    /// <returns>Array of objects</returns>
    public static IEnumerable<object[]> Zip(params IEnumerable[] enumerations)
        => Zip(true, enumerations, (object[])Array.CreateInstance(typeof(Object), enumerations.Length));

    /// <summary>
    /// Read several <see cref="IEnumerable"/> in parallel and returns an array of object of elments from the same indexes
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="enumerations"><see cref="IEnumerable"/> to be read</param>
    /// <returns>Array of objects</returns>
    public static IEnumerable<TResult> Zip<TResult>(Func<object[], TResult> transform, params IEnumerable[] enumerations)
        => Zip(true, enumerations, (object[])Array.CreateInstance(typeof(Object), enumerations.Length)).Select(o => transform(o));

    /// <summary>
    /// Read several <see cref="IEnumerable"/> in parallel and returns an array of object of elments from the same indexes
    /// </summary>
    /// <param name="continueAfterShortestListEnds">Continue when the shortest list ends</param>
    /// <param name="enumerations"><see cref="IEnumerable"/> to be read</param>
    /// <returns>Array of objects</returns>
    public static IEnumerable<object[]> Zip(bool continueAfterShortestListEnds, params IEnumerable[] enumerations)
        => Zip(continueAfterShortestListEnds, enumerations, (object[])Array.CreateInstance(typeof(Object), enumerations.Length));

    /// <summary>
    /// Read several <see cref="IEnumerable"/> in parallel and returns an array of object of elments from the same indexes
    /// </summary>
    /// <param name="continueAfterShortestListEnds">Continue when the shortest list ends</param>
    /// <param name="transform">Transform function</param>
    /// <param name="enumerations"><see cref="IEnumerable"/> to be read</param>
    /// <returns>Array of objects</returns>
    public static IEnumerable<TResult> Zip<TResult>(bool continueAfterShortestListEnds, Func<object[], TResult> transform, params IEnumerable[] enumerations)
        => Zip(continueAfterShortestListEnds, enumerations, (object[])Array.CreateInstance(typeof(Object), enumerations.Length)).Select(o => transform(o));

    /// <summary>
    /// Read several <see cref="IEnumerable"/> in parallel and returns an array of object of elments from the same indexes
    /// </summary>
    /// <param name="enumerations"><see cref="IEnumerable"/> to be read</param>
    /// <returns>Array of objects</returns>
    public static IEnumerable<object[]> Zip(params (IEnumerable enumeration, object defaultValue)[] enumerations)
        => Zip(true, enumerations);

    /// <summary>
    /// Read several <see cref="IEnumerable"/> in parallel and returns an array of object of elments from the same indexes
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="enumerations"><see cref="IEnumerable"/> to be read</param>
    /// <returns>Array of objects</returns>
    public static IEnumerable<TResult> Zip<TResult>(Func<object[], TResult> transform, params (IEnumerable enumeration, object defaultValue)[] enumerations)
        => Zip(true, enumerations).Select(o => transform(o));

    /// <summary>
    /// Read several <see cref="IEnumerable"/> in parallel and returns an array of object of elments from the same indexes
    /// </summary>
    /// <param name="continueAfterShortestListEnds">Continue when the shortest list ends</param>
    /// <param name="enumerations"><see cref="IEnumerable"/> to be read</param>
    /// <returns>Array of objects</returns>
    public static IEnumerable<object[]> Zip(bool continueAfterShortestListEnds, params (IEnumerable enumeration, object defaultValue)[] enumerations)
        => Zip(continueAfterShortestListEnds, enumerations.Select(e => e.enumeration).ToArray(), enumerations.Select((e) => e.defaultValue).ToArray());

    /// <summary>
    /// Read several <see cref="IEnumerable"/> in parallel and returns an array of object of elments from the same indexes
    /// </summary>
    /// <param name="continueAfterShortestListEnds">Continue when the shortest list ends</param>
    /// <param name="enumerations"><see cref="IEnumerable"/> to be read</param>
    /// <param name="transform">transformation function of input object to output object</param>
    /// <returns>Array of objects</returns>
    public static IEnumerable<TResult> Zip<TResult>(bool continueAfterShortestListEnds, Func<object[], TResult> transform, params (IEnumerable enumeration, object defaultValue)[] enumerations)
        => Zip(continueAfterShortestListEnds, enumerations.Select(e => e.enumeration).ToArray(), enumerations.Select((e) => e.defaultValue)).Select(o => transform(o));

    private static IEnumerable<object[]> Zip(bool continueAfterShortestListEnds, IEnumerable[] enumerations, object[] defaultValues)
    {
        bool[] gotValue = new bool[enumerations.Length];
        IEnumerator[] enumerators = enumerations.Select(e => e.GetEnumerator()).ToArray();

        for (int i = 0; i < enumerators.Length; i++)
        {
            gotValue[i] = enumerators[i].MoveNext();
        }

        while ((continueAfterShortestListEnds && gotValue.Any(v => v)) || gotValue.All(v => v))
        {
            object[] values = new object[enumerations.Length];
            for (int i = 0; i < enumerators.Length; i++)
            {
                values[i] = gotValue[i] ? enumerators[i].Current : defaultValues[i];
            }

            yield return values;

            for (int i = 0; i < enumerators.Length; i++)
            {
                gotValue[i] = gotValue[i] && enumerators[i].MoveNext();
            }

        }
    }

    /// <summary>
    /// Returns an enumerator with each element is the combination of the combined enumeration of <paramref name="enumeration1"/> and <paramref name="enumeration2"/> enumerations
    /// </summary>
    /// <typeparam name="T1">Type for <paramref name="enumeration1"/> elements</typeparam>
    /// <typeparam name="T2">Type for <paramref name="enumeration2"/> elements</typeparam>
    /// <param name="enumeration1">Enumeration 1</param>
    /// <param name="enumeration2">Enumeration 2</param>
    /// <param name="default1">Default value after <paramref name="enumeration1"/> has end</param>
    /// <param name="default2">Default value after <paramref name="enumeration2"/> has end</param>
    /// <param name="continueAfterShortestListEnds">Continue to enumerate the longuest enumeration when the shortest has end</param>
    /// <returns></returns>
    public static IEnumerable<(T1 Item1, T2 Item2)> Zip<T1, T2>(
        IEnumerable<T1> enumeration1, IEnumerable<T2> enumeration2,
        T1 default1 = default(T1), T2 default2 = default(T2),
        bool continueAfterShortestListEnds = true
    )
        => Zip(continueAfterShortestListEnds,
            new IEnumerable[] { enumeration1, enumeration2 },
            new object[] { default1, default2 })
        .Select(o => ((T1)o[0], (T2)o[1]));

    /// <summary>
    /// Returns an enumerator with each element is the combination of the combined enumeration of <paramref name="enumeration1"/> and <paramref name="enumeration2"/> enumerations
    /// </summary>
    /// <typeparam name="T1">Type for <paramref name="enumeration1"/> elements</typeparam>
    /// <typeparam name="T2">Type for <paramref name="enumeration2"/> elements</typeparam>
    /// <typeparam name="TResult">Resulting type</typeparam>
    /// <param name="enumeration1">Enumeration 1</param>
    /// <param name="enumeration2">Enumeration 2</param>
    /// <param name="func">transformation function</param>
    /// <param name="continueAfterShortestListEnds">Continue to enumerate the longuest enumeration when the shortest has end</param>
    /// <returns></returns>
    public static IEnumerable<TResult> Zip<T1, T2, TResult>(
        IEnumerable<T1> enumeration1, IEnumerable<T2> enumeration2,
        Func<T1, T2, TResult> func, bool continueAfterShortestListEnds = true
    )
        => Zip(enumeration1, enumeration2, default, default, continueAfterShortestListEnds).Select(e => func(e.Item1, e.Item2));

    /// <summary>
    /// Returns an enumerator with each element is the combination of the combined enumeration of <paramref name="enumeration1"/> and <paramref name="enumeration2"/> enumerations
    /// </summary>
    /// <typeparam name="T1">Type for <paramref name="enumeration1"/> elements</typeparam>
    /// <typeparam name="T2">Type for <paramref name="enumeration2"/> elements</typeparam>
    /// <typeparam name="T3">Type for <paramref name="enumeration3"/> elements</typeparam>
    /// <param name="enumeration1">Enumeration 1</param>
    /// <param name="enumeration2">Enumeration 2</param>
    /// <param name="enumeration3">Enumeration 2</param>
    /// <param name="default1">Default value after <paramref name="enumeration1"/> has end</param>
    /// <param name="default2">Default value after <paramref name="enumeration2"/> has end</param>
    /// <param name="default3">Default value after <paramref name="enumeration3"/> has end</param>
    /// <param name="continueAfterShortestListEnds">Continue to enumerate the longuest enumeration when the shortest has end</param>
    /// <returns></returns>
    public static IEnumerable<(T1 Item1, T2 Item2, T3 Item3)> Zip<T1, T2, T3>(
        IEnumerable<T1> enumeration1, IEnumerable<T2> enumeration2, IEnumerable<T3> enumeration3,
        T1 default1 = default(T1), T2 default2 = default(T2), T3 default3 = default(T3),
        bool continueAfterShortestListEnds = true
    )
        => Zip(true,
            new IEnumerable[] { enumeration1, enumeration2, enumeration3 },
            new object[] { default1, default2, default3 })
        .Select(o => ((T1)o[0], (T2)o[1], (T3)o[2]));

    /// <summary>
    /// Returns an enumerator with each element is the combination of the combined enumeration of <paramref name="enumeration1"/> and <paramref name="enumeration2"/> enumerations
    /// </summary>
    /// <typeparam name="T1">Type for <paramref name="enumeration1"/> elements</typeparam>
    /// <typeparam name="T2">Type for <paramref name="enumeration2"/> elements</typeparam>
    /// <typeparam name="T3">Type for <paramref name="enumeration3"/> elements</typeparam>
    /// <typeparam name="TResult">Type for resulting elements</typeparam>
    /// <param name="enumeration1">Enumeration 1</param>
    /// <param name="enumeration2">Enumeration 2</param>
    /// <param name="enumeration3">Enumeration 2</param>
    /// <param name="func">transformation function</param>
    /// <param name="continueAfterShortestListEnds">Continue to enumerate the longuest enumeration when the shortest has end</param>
    /// <returns></returns>
    public static IEnumerable<TResult> Zip<T1, T2, T3, TResult>(
        IEnumerable<T1> enumeration1, IEnumerable<T2> enumeration2, IEnumerable<T3> enumeration3,
        Func<T1, T2, T3, TResult> func,
        bool continueAfterShortestListEnds = true
    )
        => Zip(enumeration1, enumeration2, enumeration3, continueAfterShortestListEnds: continueAfterShortestListEnds).Select(i => func(i.Item1, i.Item2, i.Item3));


}

/// <summary>
/// Represents a value and its consecutive repetition count (used in packing).
/// </summary>
/// <typeparam name="T">Type of the packed value.</typeparam>
/// <param name="Value">The value being packed.</param>
/// <param name="Repetition">How many times it repeats consecutively.</param>
public record struct Pack<T>(T Value, int Repetition);
