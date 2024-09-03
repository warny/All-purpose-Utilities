using System;
using System.Collections;
using System.Collections.Generic;

namespace Utils.Collections;

/// <summary>
/// Indexed list.
/// </summary>
/// <typeparam name="K">Key structure</typeparam>
/// <typeparam name="V">Value structure</typeparam>
public class IndexedList<K, V> : ICollection<V>, IReadOnlyDictionary<K, V>
{
    private readonly Dictionary<K, V> dictionary = new Dictionary<K, V>();
    private readonly Func<V, K> getKey;

    /// <summary>
    /// Indexed list.
    /// </summary>
    /// <param name="getKey">Key extraction function</param>
    public IndexedList(Func<V, K> getKey)
    {
        this.getKey = getKey;
    }

    /// <summary>
    /// Indexed list.
    /// </summary>
    /// <param name="getKey">Key extraction function</param>
    public IndexedList(Func<V, K> getKey, IEnumerable<V> values) : this(getKey)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }

    /// <summary>
    /// Retrieves a value.
    /// </summary>
    /// <param name="key">Key</param>
    /// <returns>Corresponding value</returns>
    public V this[K key] => dictionary[key];

    /// <summary>
    /// Number of stored values.
    /// </summary>
    public int Count => dictionary.Count;

    /// <summary>
    /// Read/write.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Set of keys.
    /// </summary>
    public IEnumerable<K> Keys => dictionary.Keys;

    /// <summary>
    /// Set of values.
    /// </summary>
    public IEnumerable<V> Values => dictionary.Values;

    /// <summary>
    /// Adds a value.
    /// </summary>
    /// <param name="item">Value</param>
    public void Add(V item) => dictionary.Add(getKey(item), item);

    /// <summary>
    /// Removes a value.
    /// </summary>
    /// <param name="item">Value to remove</param>
    /// <returns><see cref="true"/> if the value was removed; otherwise, <see cref="false"/></returns>
    public bool Remove(V item) => dictionary.Remove(getKey(item));

    /// <summary>
    /// Removes a value by its key.
    /// </summary>
    /// <param name="key">Key to remove</param>
    /// <returns><see cref="true"/> if the value was removed; otherwise, <see cref="false"/></returns>
    public bool Remove(K key) => dictionary.Remove(key);

    /// <summary>
    /// Removes all elements.
    /// </summary>
    public void Clear() => dictionary.Clear();

    /// <summary>
    /// Indicates if the list contains the parameter value.
    /// </summary>
    /// <param name="item">Value</param>
    /// <returns><see cref="true"/> if the value is in the list; otherwise, <see cref="false"/></returns>
    public bool Contains(V item) => dictionary.ContainsValue(item);

    /// <summary>
    /// Indicates if the list contains the parameter key.
    /// </summary>
    /// <param name="key">Key</param>
    /// <returns><see cref="true"/> if the key is in the list; otherwise, <see cref="false"/></returns>
    public bool ContainsKey(K key) => dictionary.ContainsKey(key);

    /// <summary>
    /// Copies to an array.
    /// </summary>
    /// <param name="array">Target array</param>
    /// <param name="arrayIndex">Starting index</param>
    public void CopyTo(V[] array, int arrayIndex) => dictionary.Values.CopyTo(array, arrayIndex);

    /// <summary>
    /// Tries to get a value.
    /// </summary>
    /// <param name="key">Key</param>
    /// <param name="value">Output value</param>
    /// <returns><see cref="true"/> if the value was retrieved; otherwise, <see cref="false"/></returns>
    public bool TryGetValue(K key, out V value) => dictionary.TryGetValue(key, out value);

    IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator() => dictionary.GetEnumerator();
    IEnumerator<V> IEnumerable<V>.GetEnumerator() => dictionary.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => dictionary.Values.GetEnumerator();
}
