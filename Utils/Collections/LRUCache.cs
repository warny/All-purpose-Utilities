using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Utils.Collections;

/// <summary>
/// A Least Recently Used (LRU) cache implementation. This cache evicts the least recently accessed items when it reaches its capacity.
/// </summary>
/// <typeparam name="K">The type of keys in the cache.</typeparam>
/// <typeparam name="V">The type of values in the cache.</typeparam>
public class LRUCache<K, V> : IDictionary<K, V>
{
    private readonly int capacity;
    private readonly Dictionary<K, LinkedListNode<KeyValuePair<K, V>>> cacheMap;
    private readonly LinkedList<KeyValuePair<K, V>> lruList;

    /// <summary>
    /// Initializes a new instance of the <see cref="LRUCache{K, V}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of elements that the cache can hold.</param>
    public LRUCache(int capacity)
    {
        this.capacity = capacity;
        this.cacheMap = new Dictionary<K, LinkedListNode<KeyValuePair<K, V>>>(capacity);
        this.lruList = new LinkedList<KeyValuePair<K, V>>();
    }

    /// <summary>
    /// Gets the keys of the cache as a collection.
    /// </summary>
    public ICollection<K> Keys => lruList.Select(i => i.Key).ToList();

    /// <summary>
    /// Gets the values of the cache as a collection.
    /// </summary>
    public ICollection<V> Values => lruList.Select(i => i.Value).ToList();

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
        [MethodImpl(MethodImplOptions.Synchronized)]
        get
        {
            TryGetValue(key, out V value);
            return value;
        }
        [MethodImpl(MethodImplOptions.Synchronized)]
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
    [MethodImpl(MethodImplOptions.Synchronized)]
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
        lock (lruList)
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
}
