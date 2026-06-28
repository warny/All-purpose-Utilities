using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Utils.Collections;

/// <summary>
/// A dictionary whose keys are kept in sorted order using a <see cref="SkipList{T}"/>
/// as the underlying data structure. All keyed operations run in O(log n) time on average.
/// Iteration yields entries in ascending key order.
/// </summary>
/// <typeparam name="K">The type of keys. Must be non-null.</typeparam>
/// <typeparam name="V">The type of values.</typeparam>
public class SkipListDictionary<K, V> : IDictionary<K, V>
    where K : notnull
{
    private readonly SkipList<Entry> _skipList;

    /// <summary>
    /// Initializes a new instance using the default key comparer and a threshold of 10.
    /// </summary>
    public SkipListDictionary() : this(Comparer<K>.Default, 10) { }

    /// <summary>
    /// Initializes a new instance using the default key comparer and the specified threshold.
    /// </summary>
    /// <param name="threshold">
    /// The maximum number of nodes to traverse at a given skip level before creating a
    /// structure node. Must be &gt;= 2.
    /// </param>
    public SkipListDictionary(int threshold) : this(Comparer<K>.Default, threshold) { }

    /// <summary>
    /// Initializes a new instance using the specified key comparer and threshold.
    /// </summary>
    /// <param name="keyComparer">The comparer used to order keys.</param>
    /// <param name="threshold">
    /// The maximum number of nodes to traverse at a given skip level before creating a
    /// structure node. Must be &gt;= 2.
    /// </param>
    public SkipListDictionary(IComparer<K> keyComparer, int threshold = 10)
    {
        _skipList = new SkipList<Entry>(new EntryComparer(keyComparer ?? throw new ArgumentNullException(nameof(keyComparer))), threshold);
    }

    /// <inheritdoc />
    public int Count => _skipList.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public ICollection<K> Keys => new KeyCollection(this);

    /// <inheritdoc />
    public ICollection<V> Values => new ValueCollection(this);

    /// <inheritdoc />
    public V this[K key]
    {
        get
        {
            if (!TryGetValue(key, out var value))
                throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
            return value;
        }
        set
        {
            var probe = Entry.Probe(key);
            if (_skipList.TryGet(probe, out var found))
                found.Value = value;
            else
                _skipList.Add(new Entry(key, value));
        }
    }

    /// <inheritdoc />
    public void Add(K key, V value)
    {
        if (_skipList.Contains(Entry.Probe(key)))
            throw new ArgumentException("An element with the same key already exists.", nameof(key));
        _skipList.Add(new Entry(key, value));
    }

    /// <inheritdoc />
    public void Add(KeyValuePair<K, V> item) => Add(item.Key, item.Value);

    /// <inheritdoc />
    public bool ContainsKey(K key) => _skipList.Contains(Entry.Probe(key));

    /// <inheritdoc />
    public bool Contains(KeyValuePair<K, V> item)
        => TryGetValue(item.Key, out var value) && EqualityComparer<V>.Default.Equals(value, item.Value);

    /// <inheritdoc />
    public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
    {
        if (_skipList.TryGet(Entry.Probe(key), out var found))
        {
            value = found.Value;
            return true;
        }
        value = default;
        return false;
    }

    /// <inheritdoc />
    public bool Remove(K key) => _skipList.Remove(Entry.Probe(key));

    /// <inheritdoc />
    public bool Remove(KeyValuePair<K, V> item)
    {
        if (!TryGetValue(item.Key, out var value)) return false;
        if (!EqualityComparer<V>.Default.Equals(value, item.Value)) return false;
        return Remove(item.Key);
    }

    /// <inheritdoc />
    public void Clear() => _skipList.Clear();

    /// <inheritdoc />
    public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
    {
        foreach (var kvp in this)
            array[arrayIndex++] = kvp;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        => _skipList.Select(e => new KeyValuePair<K, V>(e.Key, e.Value)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private sealed class Entry
    {
        public K Key { get; }
        public V Value { get; set; }

        public Entry(K key, V value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>Creates a key-only probe used for lookups and removals.</summary>
        public static Entry Probe(K key) => new(key, default!);
    }

    private sealed class EntryComparer : IComparer<Entry>
    {
        private readonly IComparer<K> _keyComparer;

        public EntryComparer(IComparer<K> keyComparer) => _keyComparer = keyComparer;

        public int Compare(Entry? x, Entry? y) => _keyComparer.Compare(x!.Key, y!.Key);
    }

    private sealed class KeyCollection : ICollection<K>
    {
        private readonly SkipListDictionary<K, V> _owner;
        public KeyCollection(SkipListDictionary<K, V> owner) => _owner = owner;

        public int Count => _owner.Count;
        public bool IsReadOnly => true;
        public bool Contains(K item) => _owner.ContainsKey(item);

        public void CopyTo(K[] array, int arrayIndex)
        {
            foreach (var key in this) array[arrayIndex++] = key;
        }

        public IEnumerator<K> GetEnumerator()
            => _owner._skipList.Select(e => e.Key).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(K item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Remove(K item) => throw new NotSupportedException();
    }

    private sealed class ValueCollection : ICollection<V>
    {
        private readonly SkipListDictionary<K, V> _owner;
        public ValueCollection(SkipListDictionary<K, V> owner) => _owner = owner;

        public int Count => _owner.Count;
        public bool IsReadOnly => true;
        public bool Contains(V item)
            => _owner._skipList.Any(e => EqualityComparer<V>.Default.Equals(e.Value, item));

        public void CopyTo(V[] array, int arrayIndex)
        {
            foreach (var value in this) array[arrayIndex++] = value;
        }

        public IEnumerator<V> GetEnumerator()
            => _owner._skipList.Select(e => e.Value).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(V item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Remove(V item) => throw new NotSupportedException();
    }
}
