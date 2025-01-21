using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Utils.Objects;

namespace Utils.Collections;

/// <summary>
/// A class that provides a caching mechanism for loading resources. If a resource is not already loaded, 
/// it will be loaded and cached upon first access.
/// </summary>
/// <typeparam name="TKey">The type of the keys used to identify resources.</typeparam>
/// <typeparam name="TValue">The type of the resources being cached.</typeparam>
public class CachedLoader<TKey, TValue> : IDictionary<TKey, TValue>
	where TKey : notnull
{
	private readonly TryLoadValueDelegate loader;
	private readonly IDictionary<TKey, TValue> holder;

	/// <summary>
	/// Initializes a new instance of the <see cref="CachedLoader{TKey, TValue}"/> class with auto-create enabled by default.
	/// </summary>
	public CachedLoader()
	{
		AutoCreateObject = true;
		holder = new Dictionary<TKey, TValue>();
		Load = CreateGetValue();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CachedLoader{TKey, TValue}"/> class with a specified loader function.
	/// </summary>
	/// <param name="loader">The function used to load resources.</param>
	public CachedLoader(TryLoadValueDelegate loader)
		: this(loader, new Dictionary<TKey, TValue>())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CachedLoader{TKey, TValue}"/> class with a specified loader function and cache holder.
	/// </summary>
	/// <param name="loader">The function used to load resources.</param>
	/// <param name="cacheHolder">The dictionary used to hold the cached resources.</param>
	public CachedLoader(TryLoadValueDelegate loader, IDictionary<TKey, TValue> cacheHolder)
	{
		AutoCreateObject = true;
		this.loader = loader.ArgMustNotBeNull();
		this.holder = cacheHolder.ArgMustNotBeNull();
		Load = CreateGetValue();
	}

	public delegate bool TryLoadValueDelegate(TKey key, out TValue value);

	private TryLoadValueDelegate CreateGetValue()
	{
		return holder switch
		{
			Dictionary<TKey, TValue> dictionary =>
				(TKey key, out TValue value) =>
				{
					ref var val = ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary, key, out bool exists);
					if (!exists && AutoCreateObject)
					{
						exists = loader(key, out val);
					}
					value = val;
					return exists;
				},
			_ =>
				(TKey key, out TValue value) =>
				{
					if (holder.TryGetValue(key, out value))
					{
						return true;
					}
					if (AutoCreateObject && loader(key, out value))
					{
						holder[key] = value;
						return true;
					}
					value = default;
					return false;
				}
		};


	}

	private readonly TryLoadValueDelegate Load;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically create and cache a resource if it is not found.
	/// </summary>
	public bool AutoCreateObject { get; set; }

	/// <summary>
	/// Gets or sets the resource associated with the specified key.
	/// If the resource is not found and <see cref="AutoCreateObject"/> is true, the resource will be loaded and cached.
	/// </summary>
	/// <param name="key">The key of the resource to get or set.</param>
	/// <returns>The resource associated with the specified key.</returns>
	public TValue this[TKey key]
	{
		get {
			if (Load(key, out var value)) return value;
			throw new KeyNotFoundException($"The key \"{key}\" was not found");
		}
		set => holder[key] = value;
	}

	/// <summary>
	/// Adds the specified key-value pair to the cache.
	/// </summary>
	/// <param name="key">The key of the element to add.</param>
	/// <param name="value">The value of the element to add.</param>
	public void Add(TKey key, TValue value) => holder.Add(key, value);

	/// <summary>
	/// Determines whether the cache contains an element with the specified key.
	/// </summary>
	/// <param name="key">The key to locate in the cache.</param>
	/// <returns><see langword="true"/> if the cache contains an element with the specified key; otherwise, <see langword="false"/>.</returns>
	public bool ContainsKey(TKey key) => holder.ContainsKey(key);

	/// <summary>
	/// Gets a collection containing the keys of the cache.
	/// </summary>
	public ICollection<TKey> Keys => holder.Keys;

	/// <summary>
	/// Removes the element with the specified key from the cache.
	/// </summary>
	/// <param name="key">The key of the element to remove.</param>
	/// <returns><see langword="true"/> if the element is successfully removed; otherwise, <see langword="false"/>.</returns>
	public bool Remove(TKey key) => holder.Remove(key);

	/// <summary>
	/// Tries to get the value associated with the specified key.
	/// If <see cref="AutoCreateObject"/> is true and the resource is not found, it will be loaded and cached.
	/// </summary>
	/// <param name="key">The key of the resource to get.</param>
	/// <param name="value">When this method returns, contains the resource associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter.</param>
	/// <returns><see langword="true"/> if the cache contains an element with the specified key; otherwise, <see langword="false"/>.</returns>
	public bool TryGetValue(TKey key, out TValue value)
	{
		return Load (key, out value);
	}

	/// <summary>
	/// Gets a collection containing the values in the cache.
	/// </summary>
	public ICollection<TValue> Values => holder.Values;

	/// <summary>
	/// Adds a key-value pair to the cache.
	/// </summary>
	/// <param name="item">The key-value pair to add.</param>
	public void Add(KeyValuePair<TKey, TValue> item) => holder.Add(item.Key, item.Value);

	/// <summary>
	/// Removes all elements from the cache.
	/// </summary>
	public void Clear() => holder.Clear();

	/// <summary>
	/// Determines whether the cache contains a specific key-value pair.
	/// </summary>
	/// <param name="item">The key-value pair to locate in the cache.</param>
	/// <returns><see langword="true"/> if the cache contains the specified key-value pair; otherwise, <see langword="false"/>.</returns>
	public bool Contains(KeyValuePair<TKey, TValue> item) => holder.TryGetValue(item.Key, out TValue value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);

	/// <summary>
	/// Copies the elements of the cache to an array, starting at a particular array index.
	/// </summary>
	/// <param name="array">The one-dimensional array that is the destination of the elements copied from the cache.</param>
	/// <param name="arrayIndex">The zero-based index in the array at which copying begins.</param>
	public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
	{
		if (array is null) throw new ArgumentNullException(nameof(array));
		if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex), "arrayIndex must be non-negative");
		if (array.Length - arrayIndex < holder.Count) throw new ArgumentException("The array is too small to contain the elements.");

		foreach (var item in holder)
		{
			array[arrayIndex++] = item;
		}
	}

	/// <summary>
	/// Gets the number of elements contained in the cache.
	/// </summary>
	public int Count => holder.Count;

	/// <summary>
	/// Gets a value indicating whether the cache is read-only.
	/// </summary>
	public bool IsReadOnly => false;

	/// <summary>
	/// Removes the first occurrence of a specific key-value pair from the cache.
	/// </summary>
	/// <param name="item">The key-value pair to remove.</param>
	/// <returns><see langword="true"/> if the key-value pair was successfully removed; otherwise, <see langword="false"/>.</returns>
	public bool Remove(KeyValuePair<TKey, TValue> item) => holder.Remove(item.Key);

	/// <summary>
	/// Returns an enumerator that iterates through the cache.
	/// </summary>
	/// <returns>An enumerator for the cache.</returns>
	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => holder.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => holder.GetEnumerator();
}
