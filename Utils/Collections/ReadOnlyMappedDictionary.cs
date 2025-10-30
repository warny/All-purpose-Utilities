using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Utils.Objects;

namespace Utils.Collections;

/// <summary>
/// List whose source is defined by accessors.
/// </summary>
/// <typeparam name="K">Type of the key</typeparam>
/// <typeparam name="V">Type of elements</typeparam>
public class ReadOnlyMappedDictionary<K, V> : IReadOnlyDictionary<K, V>
{
    private readonly IReadOnlyDictionaryMap<K, V> map;

    /// <summary>
    /// Creates an accessor.
    /// </summary>
    /// <param name="GetValue">Accessor for retrieving a single value</param>
    /// <param name="GetValues">Retrieving all values from the list</param>
    /// <param name="GetCount">Retrieving the number of values in the list</param>
    public ReadOnlyMappedDictionary(
        Func<K, V> GetValue,
        Func<IEnumerable<KeyValuePair<K, V>>> GetValues,
        Func<int> GetCount = null
    )
    {
        map = new ReadOnlyDictionaryMap<K, V>(GetValue, GetValues, GetCount);
    }

    /// <summary>
    /// Creates an accessor.
    /// </summary>
    /// <param name="map">Class defining the accessors</param>
    public ReadOnlyMappedDictionary(IReadOnlyDictionaryMap<K, V> map)
    {
        this.map = map ?? throw new ArgumentNullException(nameof(map));
    }

    /// <inheritdoc />
    virtual public V this[K key]
    {
        get => map.GetValue(key);
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public int Count
    {
        get
        {
            if (map.CanCount)
            {
                return map.GetCount();
            }
            else
            {
                return map.GetValues().Count();
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<K> Keys => map.GetValues().Select(v => v.Key);

    /// <inheritdoc />
    public IEnumerable<V> Values => map.GetValues().Select(v => v.Value);

    /// <inheritdoc />
    public bool ContainsKey(K key) => Keys.Any(k => k.Equals(key));

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => map.GetValues().GetEnumerator();

    /// <inheritdoc />
    public bool TryGetValue(K key, out V value)
    {
        try
        {
            value = map.GetValue(key);
            return true;
        }
        catch
        {
            value = default(V);
            return false;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => map.GetValues().GetEnumerator();
}

/// <summary>
/// Defines accessor methods required to expose dictionary data without granting mutation capabilities.
/// </summary>
public interface IReadOnlyDictionaryMap<K, V>
{
    /// <summary>
    /// Retrieves a value using the provided key.
    /// </summary>
    /// <param name="key">The key that identifies the value.</param>
    /// <returns>The value associated with <paramref name="key"/>.</returns>
    V GetValue(K key);

    /// <summary>
    /// Returns the key/value pairs contained in the dictionary.
    /// </summary>
    IEnumerable<KeyValuePair<K, V>> GetValues();

    /// <summary>
    /// Retrieves the count of the key/value pairs when available.
    /// </summary>
    int GetCount();

    /// <summary>
    /// Gets a value indicating whether <see cref="GetCount"/> can be called without enumerating the dictionary.
    /// </summary>
    bool CanCount { get; }
}

/// <summary>
/// Default implementation of <see cref="IReadOnlyDictionaryMap{K, V}"/> that relies on delegate accessors.
/// </summary>
/// <typeparam name="K">Type of the keys used by the dictionary.</typeparam>
/// <typeparam name="V">Type of the values stored in the dictionary.</typeparam>
public class ReadOnlyDictionaryMap<K, V> : IReadOnlyDictionaryMap<K, V>
{
    private readonly Func<K, V> getValue;
    private readonly Func<IEnumerable<KeyValuePair<K, V>>> getItems;
    private readonly Func<int> getCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyDictionaryMap{K, V}"/> class using delegate-based accessors.
    /// </summary>
    /// <param name="getValue">Delegate that retrieves a value for a given key.</param>
    /// <param name="getItems">Delegate that returns all key/value pairs.</param>
    /// <param name="getCount">Delegate that provides the item count when available.</param>
    public ReadOnlyDictionaryMap(
        Func<K, V> getValue,
        Func<IEnumerable<KeyValuePair<K, V>>> getItems,
        Func<int> getCount
    )
    {
        this.getValue = getValue.Arg().MustNotBeNull();
        this.getItems = getItems.Arg().MustNotBeNull();
        this.getCount = getCount;
    }

    /// <inheritdoc />
    public int GetCount() => getCount();

    /// <inheritdoc />
    public V GetValue(K key) => getValue(key);

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<K, V>> GetValues() => getItems();

    /// <inheritdoc />
    public bool CanCount => getCount is not null;
}
