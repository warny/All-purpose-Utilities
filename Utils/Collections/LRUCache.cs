using System;
using System.Collections;
using System.Collections.Generic;

namespace Utils.Collections;

/// <summary>
/// A Least Recently Used (LRU) cache implementation. This cache evicts the least recently accessed items when it reaches its capacity.
/// </summary>
/// <typeparam name="K">The type of keys in the cache.</typeparam>
/// <typeparam name="V">The type of values in the cache.</typeparam>
/// <remarks>
/// This type is thread-safe: every member, including enumeration and the <see cref="Keys"/>/
/// <see cref="Values"/> views, is synchronized through a single internal lock (a private object, not
/// this instance — locking on the cache instance itself from calling code has no effect on it). Enumerating
/// the cache (directly, or through <see cref="Keys"/> or <see cref="Values"/>) takes a point-in-time
/// snapshot under that lock rather than returning a live cursor, so it never throws because of a
/// concurrent modification and never observes a torn state — it just won't reflect mutations made
/// after the snapshot was taken. As with most thread-safe collections, individual members are atomic
/// but compound operations spanning more than one call (e.g. "add if not already present", or
/// "read-modify-write") are <b>not</b> currently supported atomically by this type; there is no public
/// way to make such a sequence atomic from outside the class.
/// </remarks>
public class LRUCache<K, V> : IDictionary<K, V>
    where K : notnull
{
    private readonly object syncRoot = new();
    private readonly int capacity;
    private readonly Dictionary<K, LinkedListNode<KeyValuePair<K, V>>> cacheMap;
    private readonly LinkedList<KeyValuePair<K, V>> lruList;
    private readonly KeyCollection _keysView;
    private readonly ValueCollection _valuesView;

    /// <summary>
    /// Initializes a new instance of the <see cref="LRUCache{K, V}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of elements that the cache can hold. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="capacity"/> is zero or negative. (#14)
    /// </exception>
    public LRUCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity,
                "Capacity must be greater than zero.");

        this.capacity = capacity;
        this.cacheMap = new Dictionary<K, LinkedListNode<KeyValuePair<K, V>>>(capacity);
        this.lruList = new LinkedList<KeyValuePair<K, V>>();
        this._keysView = new KeyCollection(this);
        this._valuesView = new ValueCollection(this);
    }

    /// <summary>
    /// Gets the keys of the cache as a live read-only view — no allocation on each access.
    /// </summary>
    public ICollection<K> Keys => _keysView;

    /// <summary>
    /// Gets the values of the cache as a live read-only view — no allocation on each access.
    /// </summary>
    public ICollection<V> Values => _valuesView;

    /// <summary>
    /// Gets the number of elements contained in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            lock (syncRoot)
            {
                return lruList.Count;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the cache is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown by the getter when <paramref name="key"/> is not present in the cache. (#15)
    /// </exception>
    public V this[K key]
    {
        get
        {
            lock (syncRoot)
            {
                if (!TryGetValueCore(key, out V value))
                    throw new KeyNotFoundException($"The key '{key}' was not found in the cache.");
                return value;
            }
        }
        set
        {
            lock (syncRoot)
            {
                RemoveCore(key);
                AddCore(key, value);
            }
        }
    }

    /// <summary>
    /// Removes the first element from the LRU list and the cache when the capacity is exceeded.
    /// Must be called while holding <see cref="syncRoot"/>.
    /// </summary>
    private void RemoveFirst()
    {
        var node = lruList.First;
        if (node != null)
        {
            lruList.RemoveFirst();
            cacheMap.Remove(node.Value.Key);
        }
    }

    /// <summary>
    /// Determines whether the cache contains the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the cache.</param>
    /// <returns><see langword="true"/> if the cache contains an element with the specified key; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey(K key)
    {
        lock (syncRoot)
        {
            return cacheMap.ContainsKey(key);
        }
    }

    /// <summary>
    /// Adds the specified key and value to the cache. If the cache exceeds its capacity, it removes the least recently used item.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    public void Add(K key, V value)
    {
        lock (syncRoot)
        {
            AddCore(key, value);
        }
    }

    /// <summary>
    /// Adds the specified key and value to the cache. Must be called while holding <see cref="syncRoot"/>.
    /// </summary>
    private void AddCore(K key, V value)
    {
        if (cacheMap.Count >= capacity)
        {
            RemoveFirst();
        }

        var cacheItem = new KeyValuePair<K, V>(key, value);
        var node = new LinkedListNode<KeyValuePair<K, V>>(cacheItem);
        lruList.AddLast(node);
        cacheMap.Add(key, node);
    }

    /// <summary>
    /// Removes the element with the specified key from the cache.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns><see langword="true"/> if the element is successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(K key)
    {
        lock (syncRoot)
        {
            return RemoveCore(key);
        }
    }

    /// <summary>
    /// Removes the element with the specified key from the cache. Must be called while holding <see cref="syncRoot"/>.
    /// </summary>
    private bool RemoveCore(K key)
    {
        if (cacheMap.TryGetValue(key, out var node))
        {
            lruList.Remove(node);
            cacheMap.Remove(key);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose value to get.</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter.</param>
    /// <returns><see langword="true"/> if the cache contains an element with the specified key; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(K key, out V value)
    {
        lock (syncRoot)
        {
            return TryGetValueCore(key, out value);
        }
    }

    /// <summary>
    /// Gets the value associated with the specified key. Must be called while holding <see cref="syncRoot"/>.
    /// </summary>
    private bool TryGetValueCore(K key, out V value)
    {
        if (cacheMap.TryGetValue(key, out var node))
        {
            value = node.Value.Value;
            // Move the accessed node to the end of the LRU list.
            lruList.Remove(node);
            lruList.AddLast(node);
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Adds the specified key-value pair to the cache.
    /// </summary>
    /// <param name="item">The key-value pair to add.</param>
    public void Add(KeyValuePair<K, V> item)
    {
        Add(item.Key, item.Value);
    }

    /// <summary>
    /// Removes all elements from the cache.
    /// </summary>
    public void Clear()
    {
        lock (syncRoot)
        {
            lruList.Clear();
            cacheMap.Clear();
        }
    }

    /// <summary>
    /// Determines whether the cache contains a specific key-value pair.
    /// </summary>
    /// <param name="item">The key-value pair to locate in the cache.</param>
    /// <returns><see langword="true"/> if the cache contains the specified key-value pair; otherwise, <see langword="false"/>.</returns>
    public bool Contains(KeyValuePair<K, V> item)
    {
        lock (syncRoot)
        {
            return lruList.Contains(item);
        }
    }

    /// <summary>
    /// Copies the elements of the cache to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the cache.</param>
    /// <param name="arrayIndex">The zero-based index in the array at which copying begins.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="array"/> is <see langword="null"/>. (#17)</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="arrayIndex"/> is negative. (#17)</exception>
    /// <exception cref="ArgumentException">Thrown when the destination array does not have enough room. (#17)</exception>
    public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Index must be non-negative.");

        lock (syncRoot)
        {
            if (array.Length - arrayIndex < lruList.Count)
                throw new ArgumentException("The destination array does not have sufficient space.", nameof(array));

            foreach (var item in lruList)
            {
                array[arrayIndex++] = new KeyValuePair<K, V>(item.Key, item.Value);
            }
        }
    }

    /// <summary>
    /// Removes the first occurrence of a specific key-value pair from the cache.
    /// Both the key and value must match for the entry to be removed. (#16)
    /// </summary>
    /// <param name="item">The key-value pair to remove.</param>
    /// <returns>
    /// <see langword="true"/> if the key-value pair was found and the value matched; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Remove(KeyValuePair<K, V> item)
    {
        lock (syncRoot)
        {
            if (cacheMap.TryGetValue(item.Key, out var node) &&
                EqualityComparer<V>.Default.Equals(node.Value.Value, item.Value))
            {
                lruList.Remove(node);
                cacheMap.Remove(item.Key);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Returns an enumerator over a point-in-time snapshot of the cache, taken under the internal lock.
    /// </summary>
    /// <returns>An enumerator for the cache.</returns>
    public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
    {
        List<KeyValuePair<K, V>> snapshot;
        lock (syncRoot)
        {
            snapshot = new List<KeyValuePair<K, V>>(lruList);
        }
        return snapshot.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class KeyCollection : ICollection<K>
    {
        private readonly LRUCache<K, V> _owner;
        internal KeyCollection(LRUCache<K, V> owner) => _owner = owner;

        public int Count
        {
            get { lock (_owner.syncRoot) { return _owner.lruList.Count; } }
        }

        public bool IsReadOnly => true;

        public bool Contains(K item)
        {
            lock (_owner.syncRoot)
            {
                foreach (var kvp in _owner.lruList)
                    if (EqualityComparer<K>.Default.Equals(kvp.Key, item)) return true;
                return false;
            }
        }

        /// <summary>
        /// Copies the keys to the specified array starting at <paramref name="arrayIndex"/>.
        /// </summary>
        /// <param name="array">Destination array.</param>
        /// <param name="arrayIndex">Zero-based start index in <paramref name="array"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="array"/> is <see langword="null"/>. (#17)</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="arrayIndex"/> is negative. (#17)</exception>
        /// <exception cref="ArgumentException">Thrown when the destination array does not have enough room. (#17)</exception>
        public void CopyTo(K[] array, int arrayIndex)
        {
            if (array is null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Index must be non-negative.");

            lock (_owner.syncRoot)
            {
                if (array.Length - arrayIndex < _owner.lruList.Count)
                    throw new ArgumentException("The destination array does not have sufficient space.", nameof(array));

                foreach (var kvp in _owner.lruList)
                    array[arrayIndex++] = kvp.Key;
            }
        }

        public IEnumerator<K> GetEnumerator()
        {
            List<K> snapshot;
            lock (_owner.syncRoot)
            {
                snapshot = new List<K>(_owner.lruList.Count);
                foreach (var kvp in _owner.lruList)
                    snapshot.Add(kvp.Key);
            }
            return snapshot.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(K item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Remove(K item) => throw new NotSupportedException();
    }

    private sealed class ValueCollection : ICollection<V>
    {
        private readonly LRUCache<K, V> _owner;
        internal ValueCollection(LRUCache<K, V> owner) => _owner = owner;

        public int Count
        {
            get { lock (_owner.syncRoot) { return _owner.lruList.Count; } }
        }

        public bool IsReadOnly => true;

        public bool Contains(V item)
        {
            lock (_owner.syncRoot)
            {
                foreach (var kvp in _owner.lruList)
                    if (EqualityComparer<V>.Default.Equals(kvp.Value, item)) return true;
                return false;
            }
        }

        /// <summary>
        /// Copies the values to the specified array starting at <paramref name="arrayIndex"/>.
        /// </summary>
        /// <param name="array">Destination array.</param>
        /// <param name="arrayIndex">Zero-based start index in <paramref name="array"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="array"/> is <see langword="null"/>. (#17)</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="arrayIndex"/> is negative. (#17)</exception>
        /// <exception cref="ArgumentException">Thrown when the destination array does not have enough room. (#17)</exception>
        public void CopyTo(V[] array, int arrayIndex)
        {
            if (array is null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Index must be non-negative.");

            lock (_owner.syncRoot)
            {
                if (array.Length - arrayIndex < _owner.lruList.Count)
                    throw new ArgumentException("The destination array does not have sufficient space.", nameof(array));

                foreach (var kvp in _owner.lruList)
                    array[arrayIndex++] = kvp.Value;
            }
        }

        public IEnumerator<V> GetEnumerator()
        {
            List<V> snapshot;
            lock (_owner.syncRoot)
            {
                snapshot = new List<V>(_owner.lruList.Count);
                foreach (var kvp in _owner.lruList)
                    snapshot.Add(kvp.Value);
            }
            return snapshot.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(V item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Remove(V item) => throw new NotSupportedException();
    }
}
