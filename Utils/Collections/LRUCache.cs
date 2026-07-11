using System.Collections;
using System.Collections.Generic;

namespace Utils.Collections;

/// <summary>
/// A Least Recently Used (LRU) cache implementation. This cache evicts the least recently accessed items when it reaches its capacity.
/// </summary>
/// <typeparam name="K">The type of keys in the cache.</typeparam>
/// <typeparam name="V">The type of values in the cache.</typeparam>
/// <remarks>
/// This type is <b>not thread-safe</b>. Concurrent reads and writes from multiple threads (including
/// enumeration via <see cref="GetEnumerator"/>, <see cref="Keys"/>, or <see cref="Values"/> while the
/// cache is mutated) must be synchronized externally by the caller, for example with a single
/// <see langword="lock"/> shared by all callers of a given instance.
/// </remarks>
public class LRUCache<K, V> : IDictionary<K, V>
    where K : notnull
{
    private readonly int capacity;
    private readonly Dictionary<K, LinkedListNode<KeyValuePair<K, V>>> cacheMap;
    private readonly LinkedList<KeyValuePair<K, V>> lruList;
    private readonly KeyCollection _keysView;
    private readonly ValueCollection _valuesView;

    /// <summary>
    /// Initializes a new instance of the <see cref="LRUCache{K, V}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of elements that the cache can hold.</param>
    public LRUCache(int capacity)
    {
        this.capacity = capacity;
        this.cacheMap = new Dictionary<K, LinkedListNode<KeyValuePair<K, V>>>(capacity);
        this.lruList = new LinkedList<KeyValuePair<K, V>>();
        this._keysView = new KeyCollection(lruList);
        this._valuesView = new ValueCollection(lruList);
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
    public int Count => lruList.Count;

    /// <summary>
    /// Gets a value indicating whether the cache is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    public V this[K key]
    {
        get
        {
            TryGetValue(key, out V value);
            return value;
        }
        set
        {
            Remove(key);
            Add(key, value);
        }
    }

    /// <summary>
    /// Removes the first element from the LRU list and the cache when the capacity is exceeded.
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
    public bool ContainsKey(K key) => cacheMap.ContainsKey(key);

    /// <summary>
    /// Adds the specified key and value to the cache. If the cache exceeds its capacity, it removes the least recently used item.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    public void Add(K key, V value)
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
        lruList.Clear();
        cacheMap.Clear();
    }

    /// <summary>
    /// Determines whether the cache contains a specific key-value pair.
    /// </summary>
    /// <param name="item">The key-value pair to locate in the cache.</param>
    /// <returns><see langword="true"/> if the cache contains the specified key-value pair; otherwise, <see langword="false"/>.</returns>
    public bool Contains(KeyValuePair<K, V> item) => lruList.Contains(item);

    /// <summary>
    /// Copies the elements of the cache to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the cache.</param>
    /// <param name="arrayIndex">The zero-based index in the array at which copying begins.</param>
    public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
    {
        foreach (var item in lruList)
        {
            array[arrayIndex++] = new KeyValuePair<K, V>(item.Key, item.Value);
        }
    }

    /// <summary>
    /// Removes the first occurrence of a specific key-value pair from the cache.
    /// </summary>
    /// <param name="item">The key-value pair to remove.</param>
    /// <returns><see langword="true"/> if the key-value pair was successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(KeyValuePair<K, V> item) => Remove(item.Key);

    /// <summary>
    /// Returns an enumerator that iterates through the cache.
    /// </summary>
    /// <returns>An enumerator for the cache.</returns>
    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => lruList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class KeyCollection : ICollection<K>
    {
        private readonly LinkedList<KeyValuePair<K, V>> _list;
        internal KeyCollection(LinkedList<KeyValuePair<K, V>> list) => _list = list;
        public int Count => _list.Count;
        public bool IsReadOnly => true;
        public bool Contains(K item) { foreach (var kvp in _list) if (EqualityComparer<K>.Default.Equals(kvp.Key, item)) return true; return false; }
        public void CopyTo(K[] array, int arrayIndex) { foreach (var kvp in _list) array[arrayIndex++] = kvp.Key; }
        public IEnumerator<K> GetEnumerator() { foreach (var kvp in _list) yield return kvp.Key; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(K item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Remove(K item) => throw new NotSupportedException();
    }

    private sealed class ValueCollection : ICollection<V>
    {
        private readonly LinkedList<KeyValuePair<K, V>> _list;
        internal ValueCollection(LinkedList<KeyValuePair<K, V>> list) => _list = list;
        public int Count => _list.Count;
        public bool IsReadOnly => true;
        public bool Contains(V item) { foreach (var kvp in _list) if (EqualityComparer<V>.Default.Equals(kvp.Value, item)) return true; return false; }
        public void CopyTo(V[] array, int arrayIndex) { foreach (var kvp in _list) array[arrayIndex++] = kvp.Value; }
        public IEnumerator<V> GetEnumerator() { foreach (var kvp in _list) yield return kvp.Value; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(V item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Remove(V item) => throw new NotSupportedException();
    }
}
