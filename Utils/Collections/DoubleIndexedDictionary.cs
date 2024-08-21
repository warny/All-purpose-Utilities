using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Utils.Collections
{
	public class DoubleIndexedDictionary<T1, T2> : IReadOnlyCollection<KeyValuePair<T1, T2>>
	{
		private readonly Dictionary<T1, T2> left = [];
		private readonly Dictionary<T2, T1> right = [];

		public MappedDictionary<T1, T2> Left { get; }
		public MappedDictionary<T2, T1> Right { get; }

		public DoubleIndexedDictionary() {
			Left = new MappedDictionary<T1, T2>(left, AddLeft, SetLeft, RemoveLeft, Clear);
			Right = new MappedDictionary<T2, T1>(right, AddRight, SetRight, RemoveRight, Clear);
		}
		public DoubleIndexedDictionary(IEnumerable<(T1 Key, T2 Value)> values) : this()
		{
			foreach (var kv in values)
			{
				left.Add(kv.Key, kv.Value);
				right.Add(kv.Value, kv.Key);
			}
		}
		public DoubleIndexedDictionary(IEnumerable<(T2 Value, T1 Key)> values) : this()
		{
			foreach (var kv in values)
			{
				left.Add(kv.Key, kv.Value);
				right.Add(kv.Value, kv.Key);
			}
		}

		public DoubleIndexedDictionary(IEnumerable<KeyValuePair<T1, T2>> values) : this()
		{
			foreach (var kv in values)
			{
				left.Add(kv.Key, kv.Value);
				right.Add(kv.Value, kv.Key);
			}
		}
		public DoubleIndexedDictionary(IEnumerable<KeyValuePair<T2, T1>> values) : this()
		{
			foreach (var kv in values)
			{
				left.Add(kv.Value, kv.Key);
				right.Add(kv.Key, kv.Value);
			}
		}

		private void AddLeft(T1 key, T2 value)
		{
			left.Add(key, value);
			try
			{
				right.Add(value, key);
			}
			catch
			{
				left.Remove(key);
				throw;
			}
		}
		private void SetLeft(T1 key, T2 value) {
			if (right.ContainsKey(value)) throw new ArgumentException(nameof(value));
			if (left.TryGetValue(key, out var val)) {
				right.Remove(val);
			}
			left[key] = value;
			right[value] = key;
		}
		private bool RemoveLeft(T1 key)
		{
			if (!left.TryGetValue(key, out var value)) return false;
			left.Remove(key);
			right.Remove(value);
			return true;
		}

		private void SetRight(T2 key, T1 value) {
			if (left.ContainsKey(value)) throw new ArgumentException(nameof(value));
			if (right.TryGetValue(key, out var val))
			{
				left.Remove(val);
			}
			right[key] = value;
			left[value] = key;
		}

		private void AddRight(T2 key, T1 value) => AddLeft(value, key);
		private bool RemoveRight(T2 key)
		{
			if (!right.TryGetValue(key, out var value)) return false;
			right.Remove(key);
			left.Remove(value);
			return true;
		}

		public int Count => left.Count;

		public void Add(T1 key, T2 value) => AddLeft(key, value);
		public void Add(KeyValuePair<T1, T2> item) => AddLeft(item.Key, item.Value);

		public void Clear()
		{
			left.Clear();
			right.Clear();
		}

		public bool Remove(KeyValuePair<T1, T2> item)
		{
			if (!left.TryGetValue(item.Key, out var value)) return false;
			if (!item.Value.Equals(value)) return false;
			left.Remove(item.Key);
			right.Remove(value);
			return true;
		}

		public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator() => left.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => left.GetEnumerator();
	}
}
