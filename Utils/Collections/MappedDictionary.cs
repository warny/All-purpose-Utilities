using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Collections
{
	/// <summary>
	/// A dictionary that uses a custom mapping for its operations. It allows customization of 
	/// how the dictionary handles additions, removals, and value retrievals.
	/// </summary>
	/// <typeparam name="K">The type of keys in the dictionary.</typeparam>
	/// <typeparam name="V">The type of values in the dictionary.</typeparam>
	public class MappedDictionary<K, V> : ReadOnlyMappedDictionary<K, V>, IDictionary<K, V>
	{
		private readonly IDictionaryMap<K, V> map;

		/// <summary>
		/// Initializes a new instance of the <see cref="MappedDictionary{K, V}"/> class using a custom dictionary map.
		/// </summary>
		/// <param name="map">The custom dictionary map that defines the behavior of the dictionary.</param>
		public MappedDictionary(IDictionaryMap<K, V> map) : base(map)
		{
			this.map = map ?? throw new ArgumentNullException(nameof(map));
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MappedDictionary{K, V}"/> class with custom handlers for dictionary operations.
		/// </summary>
		/// <param name="containsKeyValue">Function to determine if a key-value pair exists in the dictionary.</param>
		/// <param name="addValue">Action to add a key-value pair to the dictionary.</param>
		/// <param name="getValue">Function to get the value associated with a key.</param>
		/// <param name="setValue">Action to set the value for a key in the dictionary.</param>
		/// <param name="removeValue">Function to remove a key from the dictionary.</param>
		/// <param name="getValues">Function to get all key-value pairs in the dictionary.</param>
		/// <param name="getCount">Function to get the count of key-value pairs in the dictionary.</param>
		/// <param name="clear">Action to clear the dictionary.</param>
		public MappedDictionary(
			Func<KeyValuePair<K, V>, bool> containsKeyValue,
			Action<K, V> addValue,
			Func<K, V> getValue,
			Action<K, V> setValue,
			Func<K, bool> removeValue,
			Func<IEnumerable<KeyValuePair<K, V>>> getValues,
			Func<int> getCount,
			Action clear) :
			base(new DictionaryMap<K, V>(containsKeyValue, addValue, getValue, setValue, removeValue, getValues, getCount, clear))
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MappedDictionary{K, V}"/> class with a base dictionary and custom handlers.
		/// </summary>
		/// <param name="baseDictionary">The base dictionary to wrap.</param>
		/// <param name="addValue">Action to add a key-value pair to the dictionary.</param>
		/// <param name="setValue">Action to set the value for a key in the dictionary.</param>
		/// <param name="removeValue">Function to remove a key from the dictionary.</param>
		/// <param name="clear">Action to clear the dictionary.</param>
		public MappedDictionary(
			IDictionary<K, V> baseDictionary,
			Action<K, V> addValue,
			Action<K, V> setValue,
			Func<K, bool> removeValue,
			Action clear) :
			base(new DictionaryMap<K, V>(baseDictionary, addValue, setValue, removeValue, clear))
		{
		}

                /// <summary>
                /// Gets a value indicating whether the dictionary is read-only (always <see langword="false"/>).
                /// </summary>
                public bool IsReadOnly => false;

		// Explicit interface implementation for IDictionary.Keys
		ICollection<K> IDictionary<K, V>.Keys => map.GetValues().Select(kv=>kv.Key).ToList();

		// Explicit interface implementation for IDictionary.Values
		ICollection<V> IDictionary<K, V>.Values => map.GetValues().Select(kv => kv.Value).ToList();

		/// <summary>
		/// Gets or sets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key whose value to get or set.</param>
		/// <returns>The value associated with the specified key.</returns>
		public override V this[K key]
		{
			get => base[key];
			set => map.Set(key, value);
		}

		/// <summary>
		/// Adds the specified key and value to the dictionary.
		/// </summary>
		public void Add(K key, V value) => map.Add(key, value);

		/// <summary>
		/// Adds the specified key-value pair to the dictionary.
		/// </summary>
		public void Add(KeyValuePair<K, V> item) => map.Add(item.Key, item.Value);

		/// <summary>
		/// Removes all keys and values from the dictionary.
		/// </summary>
		public void Clear() => map.Clear();

		/// <summary>
		/// Determines whether the dictionary contains the specified key-value pair.
		/// </summary>
		public bool Contains(KeyValuePair<K, V> item) => map.Contains(item);

		/// <summary>
		/// Copies the elements of the dictionary to an array, starting at a particular array index.
		/// </summary>
		public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
		{
			foreach (var kv in this)
			{
				array[arrayIndex++] = kv;
			}
		}

		/// <summary>
		/// Removes the specified key-value pair from the dictionary.
		/// </summary>
		public bool Remove(KeyValuePair<K, V> item) => map.Remove(item.Key);

		/// <summary>
		/// Removes the value with the specified key from the dictionary.
		/// </summary>
		public bool Remove(K key) => map.Remove(key);
	}

	/// <summary>
	/// Interface representing a modifiable map that supports dictionary operations.
	/// </summary>
        public interface IDictionaryMap<K, V> : IReadOnlyDictionaryMap<K, V>
        {
                /// <summary>
                /// Determines whether the dictionary contains the specified key-value pair.
                /// </summary>
                /// <param name="kv">The key-value pair to locate.</param>
                /// <returns><see langword="true"/> if the pair is present; otherwise, <see langword="false"/>.</returns>
                bool Contains(KeyValuePair<K, V> kv);

                /// <summary>
                /// Adds a new key-value pair to the dictionary.
                /// </summary>
                /// <param name="key">The key to add.</param>
                /// <param name="value">The value associated with the key.</param>
                void Add(K key, V value);

                /// <summary>
                /// Sets the value associated with the specified key.
                /// </summary>
                /// <param name="key">The key to update.</param>
                /// <param name="value">The value to assign.</param>
                void Set(K key, V value);

                /// <summary>
                /// Removes the entry that matches the specified key.
                /// </summary>
                /// <param name="key">The key of the entry to remove.</param>
                /// <returns><see langword="true"/> if the key was removed; otherwise, <see langword="false"/>.</returns>
                bool Remove(K key);

                /// <summary>
                /// Removes all entries from the dictionary.
                /// </summary>
                void Clear();
        }

	/// <summary>
	/// Implementation of <see cref="IDictionaryMap{K, V}"/> that wraps custom handlers for dictionary operations.
	/// </summary>
	public class DictionaryMap<K, V> : ReadOnlyDictionaryMap<K, V>, IDictionaryMap<K, V>
	{
		private readonly Func<KeyValuePair<K, V>, bool> containsKeyValue;
		private readonly Action<K, V> addValue;
		private readonly Action<K, V> setValue;
		private readonly Func<K, bool> removeValue;
		private readonly Action clear;
		private readonly Func<IEnumerable<K>> getKeys;
		private readonly Func<IEnumerable<V>> getValues;

		/// <summary>
		/// Initializes a new instance of the <see cref="DictionaryMap{K, V}"/> class with custom handlers.
		/// </summary>
		/// <param name="containsKeyValue">Function to determine if a key-value pair exists in the dictionary.</param>
		/// <param name="addValue">Action to add a key-value pair to the dictionary.</param>
		/// <param name="getValue">Function to get the value associated with a key.</param>
		/// <param name="setValue">Action to set the value for a key in the dictionary.</param>
		/// <param name="removeValue">Function to remove a key from the dictionary.</param>
		/// <param name="getItems">Function to get all key-value pairs in the dictionary.</param>
		/// <param name="getCount">Function to get the count of key-value pairs in the dictionary.</param>
		/// <param name="clear">Action to clear the dictionary.</param>
		/// <param name="getKeys">Function to get all keys in the dictionary.</param>
		/// <param name="getValues">Function to get all values in the dictionary.</param>
		public DictionaryMap(
			Func<KeyValuePair<K, V>, bool> containsKeyValue,
			Action<K, V> addValue,
			Func<K, V> getValue,
			Action<K, V> setValue,
			Func<K, bool> removeValue,
			Func<IEnumerable<KeyValuePair<K, V>>> getItems,
			Func<int> getCount,
			Action clear,
			Func<IEnumerable<K>> getKeys = null,
			Func<IEnumerable<V>> getValues = null) :
			base(getValue, getItems, getCount)
		{
			this.containsKeyValue = containsKeyValue ?? throw new ArgumentNullException(nameof(containsKeyValue));
			this.addValue = addValue ?? throw new ArgumentNullException(nameof(addValue));
			this.setValue = setValue ?? throw new ArgumentNullException(nameof(setValue));
			this.removeValue = removeValue ?? throw new ArgumentNullException(nameof(removeValue));
			this.clear = clear ?? throw new ArgumentNullException(nameof(clear));
			this.getKeys = getKeys ?? (() => getItems().Select(kv => kv.Key));
			this.getValues = getValues ?? (() => getItems().Select(kv => kv.Value));
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DictionaryMap{K, V}"/> class that wraps an existing dictionary with custom handlers.
		/// </summary>
		/// <param name="baseDictionary">The base dictionary to wrap.</param>
		/// <param name="addValue">Action to add a key-value pair to the dictionary.</param>
		/// <param name="setValue">Action to set the value for a key in the dictionary.</param>
		/// <param name="removeValue">Function to remove a key from the dictionary.</param>
		/// <param name="clear">Action to clear the dictionary.</param>
		public DictionaryMap(
			IDictionary<K, V> baseDictionary,
			Action<K, V> addValue,
			Action<K, V> setValue,
			Func<K, bool> removeValue,
			Action clear) :
			base(
				v => baseDictionary[v],
				() => baseDictionary,
				() => baseDictionary.Count)
		{
			this.containsKeyValue = kv => baseDictionary.TryGetValue(kv.Key, out var val) && EqualityComparer<V>.Default.Equals(val, kv.Value);
			this.addValue = addValue ?? throw new ArgumentNullException(nameof(addValue));
			this.setValue = setValue ?? throw new ArgumentNullException(nameof(setValue));
			this.removeValue = removeValue ?? throw new ArgumentNullException(nameof(removeValue));
			this.clear = clear ?? throw new ArgumentNullException(nameof(clear));
			this.getKeys = () => baseDictionary.Keys;
			this.getValues = () => baseDictionary.Values;
		}

		/// <summary>
		/// Determines whether the dictionary contains the specified key-value pair.
		/// </summary>
		public bool Contains(KeyValuePair<K, V> kv) => containsKeyValue(kv);

		/// <summary>
		/// Adds the specified key and value to the dictionary.
		/// </summary>
		public void Add(K key, V value) => addValue(key, value);

		/// <summary>
		/// Sets the value associated with the specified key in the dictionary.
		/// </summary>
		public void Set(K key, V value) => setValue(key, value);

		/// <summary>
		/// Removes all keys and values from the dictionary.
		/// </summary>
		public void Clear() => clear();

		/// <summary>
		/// Removes the value with the specified key from the dictionary.
		/// </summary>
		public bool Remove(K key) => removeValue(key);
	}
}
