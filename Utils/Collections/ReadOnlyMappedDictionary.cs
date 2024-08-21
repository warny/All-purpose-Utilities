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
    /// <param name="RemoveValue">Removing a value from the list</param>
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

	virtual public V this[K key]
    {
        get => map.GetValue(key);
        set => throw new NotSupportedException();
    }

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

    public IEnumerable<K> Keys => map.GetValues().Select(v => v.Key);

    public IEnumerable<V> Values => map.GetValues().Select(v => v.Value);

    public bool ContainsKey(K key) => Keys.Any(k => k.Equals(key));

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => map.GetValues().GetEnumerator();

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

public interface IReadOnlyDictionaryMap<K, V>
{
    V GetValue(K key);
    IEnumerable<KeyValuePair<K, V>> GetValues();
    int GetCount();
    bool CanCount { get; }
}

public class ReadOnlyDictionaryMap<K, V> : IReadOnlyDictionaryMap<K, V>
{
	private readonly Func<K, V> getValue;
    private readonly Func<IEnumerable<KeyValuePair<K, V>>> getItems;
    private readonly Func<int> getCount;

    public ReadOnlyDictionaryMap(
        Func<K, V> getValue,
        Func<IEnumerable<KeyValuePair<K, V>>> getItems,
        Func<int> getCount
    )
    {
		this.getValue = getValue.ArgMustNotBeNull();
		this.getItems = getItems.ArgMustNotBeNull();
		this.getCount = getCount;
    }

    public int GetCount() => getCount();
    public V GetValue(K key) => getValue(key);
    public IEnumerable<KeyValuePair<K, V>> GetValues() => getItems();
    public bool CanCount => getCount is not null;
}
