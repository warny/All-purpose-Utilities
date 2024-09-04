using System;
using System.Collections;
using System.Collections.Generic;
using Utils.Collections;

namespace Utils.Collections
{
	/// <summary>
	/// Represents a dictionary that maintains two-way mappings between keys of type <typeparamref name="T1"/> 
	/// and values of type <typeparamref name="T2"/>. This allows bidirectional access by both key and value.
	/// </summary>
	/// <typeparam name="T1">The type of the first key.</typeparam>
	/// <typeparam name="T2">The type of the second key.</typeparam>
	public class DoubleIndexedDictionary<T1, T2> : IReadOnlyCollection<KeyValuePair<T1, T2>>
	{
		private readonly Dictionary<T1, T2> left = new();
		private readonly Dictionary<T2, T1> right = new();

		/// <summary>
		/// Accessor to interact with the dictionary using T1 as the key.
		/// </summary>
		public IDictionary<T1, T2> Left { get; }

		/// <summary>
		/// Accessor to interact with the dictionary using T2 as the key.
		/// </summary>
		public IDictionary<T2, T1> Right { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="DoubleIndexedDictionary{T1, T2}"/> class.
		/// </summary>
		public DoubleIndexedDictionary()
		{
			Left = new MappedDictionary<T1, T2>(left, AddLeft, SetLeft, RemoveLeft, Clear);
			Right = new MappedDictionary<T2, T1>(right, AddRight, SetRight, RemoveRight, Clear);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DoubleIndexedDictionary{T1, T2}"/> class with predefined key-value pairs.
		/// </summary>
		/// <param name="values">A collection of (T1 key, T2 value) pairs.</param>
		public DoubleIndexedDictionary(IEnumerable<(T1 Key, T2 Value)> values) : this()
		{
			foreach (var (key, value) in values)
			{
				AddLeft(key, value);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DoubleIndexedDictionary{T1, T2}"/> class with predefined key-value pairs.
		/// </summary>
		/// <param name="values">A collection of (T2 value, T1 key) pairs.</param>
		public DoubleIndexedDictionary(IEnumerable<(T2 Value, T1 Key)> values) : this()
		{
			foreach (var (value, key) in values)
			{
				AddRight(value, key);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DoubleIndexedDictionary{T1, T2}"/> class with predefined key-value pairs.
		/// </summary>
		/// <param name="values">A collection of key-value pairs.</param>
		public DoubleIndexedDictionary(IEnumerable<KeyValuePair<T1, T2>> values) : this()
		{
			foreach (var kv in values)
			{
				AddLeft(kv.Key, kv.Value);
			}
		}

		/// <summary>
		/// Adds a key-value pair to the dictionary using T1 as the key.
		/// </summary>
		private void AddLeft(T1 key, T2 value)
		{
			if (left.ContainsKey(key) || right.ContainsKey(value))
				throw new ArgumentException("An element with the same key or value already exists.");

			left.Add(key, value);
			right.Add(value, key);
		}

		/// <summary>
		/// Adds a key-value pair to the dictionary using T2 as the key.
		/// </summary>
		private void AddRight(T2 key, T1 value)
		{
			AddLeft(value, key);
		}

		/// <summary>
		/// Sets a key-value pair in the dictionary using T1 as the key.
		/// </summary>
		private void SetLeft(T1 key, T2 value)
		{
			if (right.ContainsKey(value))
				throw new ArgumentException("The value already exists with a different key.");

			if (left.TryGetValue(key, out var oldValue))
			{
				right.Remove(oldValue);
			}
			left[key] = value;
			right[value] = key;
		}

		/// <summary>
		/// Sets a key-value pair in the dictionary using T2 as the key.
		/// </summary>
		private void SetRight(T2 key, T1 value)
		{
			if (left.ContainsKey(value))
				throw new ArgumentException("The key already exists with a different value.");

			if (right.TryGetValue(key, out var oldKey))
			{
				left.Remove(oldKey);
			}
			right[key] = value;
			left[value] = key;
		}

		/// <summary>
		/// Removes a key-value pair from the dictionary using T1 as the key.
		/// </summary>
		private bool RemoveLeft(T1 key)
		{
			if (!left.TryGetValue(key, out var value)) return false;

			left.Remove(key);
			right.Remove(value);
			return true;
		}

		/// <summary>
		/// Removes a key-value pair from the dictionary using T2 as the key.
		/// </summary>
		private bool RemoveRight(T2 key)
		{
			if (!right.TryGetValue(key, out var value)) return false;

			right.Remove(key);
			left.Remove(value);
			return true;
		}

		/// <summary>
		/// Gets the number of elements contained in the dictionary.
		/// </summary>
		public int Count => left.Count;

		/// <summary>
		/// Adds a key-value pair to the dictionary.
		/// </summary>
		public void Add(T1 key, T2 value) => AddLeft(key, value);

		/// <summary>
		/// Adds a key-value pair to the dictionary.
		/// </summary>
		public void Add(KeyValuePair<T1, T2> item) => AddLeft(item.Key, item.Value);

		/// <summary>
		/// Clears all entries from the dictionary.
		/// </summary>
		public void Clear()
		{
			left.Clear();
			right.Clear();
		}

		/// <summary>
		/// Removes a specific key-value pair from the dictionary.
		/// </summary>
		public bool Remove(KeyValuePair<T1, T2> item)
		{
			if (!left.TryGetValue(item.Key, out var value)) return false;
			if (!EqualityComparer<T2>.Default.Equals(value, item.Value)) return false;

			left.Remove(item.Key);
			right.Remove(value);
			return true;
		}

		/// <summary>
		/// Returns an enumerator that iterates through the dictionary.
		/// </summary>
		public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator() => left.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
