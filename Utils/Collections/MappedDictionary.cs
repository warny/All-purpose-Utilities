using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Collections;

/// <summary>
/// List whose source is defined by accessors.
/// </summary>
/// <typeparam name="K">Type of the key</typeparam>
/// <typeparam name="V">Type of elements</typeparam>
public class MappedDictionary<K, V> : IReadOnlyDictionary<K, V>
{
    private readonly IMappable<K, V> mapper;

    /// <summary>
    /// Creates an accessor.
    /// </summary>
    /// <param name="GetValue">Accessor for retrieving a single value</param>
    /// <param name="RemoveValue">Removing a value from the list</param>
    /// <param name="GetValues">Retrieving all values from the list</param>
    /// <param name="GetCount">Retrieving the number of values in the list</param>
    public MappedDictionary(
        Func<K, V> GetValue,
        Func<K, bool> RemoveValue,
        Func<IEnumerable<KeyValuePair<K, V>>> GetValues,
        Func<int> GetCount = null
    )
    {
        mapper = new Mapped<K, V>(GetValue, RemoveValue, GetValues, GetCount);
    }

    /// <summary>
    /// Creates an accessor.
    /// </summary>
    /// <param name="mapper">Class defining the accessors</param>
    public MappedDictionary(IMappable<K, V> mapper)
    {
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public V this[K key] => mapper.GetValue(key);

    public bool Remove(K key) => mapper.Remove(key);

    public int Count
    {
        get
        {
            if (mapper.CanCount)
            {
                return mapper.GetCount();
            }
            else
            {
                return mapper.GetValues().Count();
            }
        }
    }

    public IEnumerable<K> Keys => mapper.GetValues().Select(v => v.Key);

    public IEnumerable<V> Values => mapper.GetValues().Select(v => v.Value);

    public bool ContainsKey(K key) => Keys.Any(k => k.Equals(key));

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => mapper.GetValues().GetEnumerator();

    public bool TryGetValue(K key, out V value)
    {
        try
        {
            value = mapper.GetValue(key);
            return true;
        }
        catch
        {
            value = default(V);
            return false;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => mapper.GetValues().GetEnumerator();
}

public interface IMappable<K, V>
{
    V GetValue(K key);
    bool Remove(K key);
    IEnumerable<KeyValuePair<K, V>> GetValues();
    int GetCount();
    bool CanCount { get; }
}

class Mapped<K, V> : IMappable<K, V>
{
    private readonly Func<K, V> getValue;
    private readonly Func<K, bool> removeValue;
    private readonly Func<IEnumerable<KeyValuePair<K, V>>> getValues;
    private readonly Func<int> getCount;

    public Mapped(
        Func<K, V> getValue,
        Func<K, bool> removeValue,
        Func<IEnumerable<KeyValuePair<K, V>>> getValues,
        Func<int> getCount
    )
    {
        this.getValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
        this.removeValue = removeValue ?? throw new ArgumentNullException(nameof(removeValue));
        this.getValues = getValues ?? throw new ArgumentNullException(nameof(getValues));
        this.getCount = getCount;
    }

    public int GetCount() => getCount();
    public V GetValue(K key) => getValue(key);
    public IEnumerable<KeyValuePair<K, V>> GetValues() => getValues();
    public bool Remove(K key) => removeValue(key);
    public bool CanCount => getCount is not null;
}
