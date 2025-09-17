using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Utils.Collections;

public static class DictionaryExtensions
{
    /// <summary>
    /// Retrieves the value associated with <paramref name="key"/> or adds the
    /// provided <paramref name="value"/> in a thread-safe manner.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="dictionary">The dictionary to operate on.</param>
    /// <param name="key">Key of the element to retrieve.</param>
    /// <param name="value">Value to add if the key is not present.</param>
    /// <returns>The existing or newly added value.</returns>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        lock (dictionary)
        {
            ref var val = ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary, key, out bool exists);
            if (!exists)
            {
                val = value;
            }
            return val;
        }
    }

    /// <summary>
    /// Retrieves the value associated with <paramref name="key"/> or adds a
    /// new value created by <paramref name="func"/> in a thread-safe manner.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="dictionary">The dictionary to operate on.</param>
    /// <param name="key">Key of the element to retrieve.</param>
    /// <param name="func">Factory used to create the value when absent.</param>
    /// <returns>The existing or newly added value.</returns>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TValue> func)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(func);
        lock (dictionary)
        {
            ref var val = ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary, key, out bool exists);
            if (!exists)
            {
                val = func();
            }
            return val;
        }
    }

    /// <summary>
    /// Attempts to update an existing entry with the specified
    /// <paramref name="value"/> in a thread-safe manner.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="dictionary">The dictionary to update.</param>
    /// <param name="key">Key of the element to update.</param>
    /// <param name="value">The new value.</param>
    /// <returns><see langword="true"/> if the key was present and the value updated.</returns>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public static bool TryUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        lock (dictionary)
        {
            ref var val = ref CollectionsMarshal.GetValueRefOrNullRef(dictionary, key);
            if (Unsafe.IsNullRef(ref val))
            {
                return false;
            }
            val = value;
            return true;
        }
    }
}
