using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Utils.Collections
{
	public class DoubleIndexedDictionary<T1, T2> : IDictionary<T1, T2>
	{
		private readonly Dictionary<T1, T2> dictionary1 = new Dictionary<T1, T2>();
		private readonly Dictionary<T2, T1> dictionary2 = new Dictionary<T2, T1>();

		public DoubleIndexedDictionary() { }
		public DoubleIndexedDictionary(IEnumerable<(T1 Key, T2 Value)> values)
		{
			foreach (var kv in values)
			{
				dictionary1.Add(kv.Key, kv.Value);
				dictionary2.Add(kv.Value, kv.Key);
			}
		}
		public DoubleIndexedDictionary(IEnumerable<(T2 Value, T1 Key)> values)
		{
			foreach (var kv in values)
			{
				dictionary1.Add(kv.Key, kv.Value);
				dictionary2.Add(kv.Value, kv.Key);
			}
		}

		public DoubleIndexedDictionary(IEnumerable<KeyValuePair<T1, T2>> values)
		{
			foreach (var kv in values)
			{
				dictionary1.Add(kv.Key, kv.Value);
				dictionary2.Add(kv.Value, kv.Key);
			}
		}
		public DoubleIndexedDictionary(IEnumerable<KeyValuePair<T2, T1>> values)
		{
			foreach (var kv in values)
			{
				dictionary1.Add(kv.Value, kv.Key);
				dictionary2.Add(kv.Key, kv.Value);
			}
		}

		public T2 this[T1 key] {
			get => dictionary1[key];
			set
			{
				if (dictionary2.ContainsKey(value)) throw new ArgumentException(nameof(value));
				if (dictionary1.TryGetValue(key, out var val)) {
					dictionary2.Remove(val);
				}
				dictionary1[key] = value;
				dictionary2[value] = key;
			}
		}
		public T1 this[T2 key] { 
			get => dictionary2[key];
			set
			{
				if (dictionary1.ContainsKey(value)) throw new ArgumentException(nameof(value));
				if (dictionary2.TryGetValue(key, out var val))
				{
					dictionary1.Remove(val);
				}
				dictionary2[key] = value;
				dictionary1[value] = key;
			}
		}

		public ICollection<T1> Keys => dictionary1.Keys;
		public ICollection<T2> Values => dictionary2.Keys;
		public int Count => dictionary1.Count;
		public bool IsReadOnly => false;

		public void Add(T1 key, T2 value)
		{
			dictionary1.Add(key, value);
			try
			{
				dictionary2.Add(value, key);
			}
			catch
			{
				dictionary1.Remove(key);
				throw;
			}
		}

		public void Add(KeyValuePair<T1, T2> item) => Add(item.Key, item.Value);
		public void Add(T2 key, T1 value) => Add(value, key);
		public void Add(KeyValuePair<T2, T1> item) => Add(item.Value, item.Key);

		public void Clear()
		{
			dictionary1.Clear();
			dictionary2.Clear();
		}

		public bool Contains(KeyValuePair<T1, T2> item) => dictionary1.TryGetValue(item.Key, out T2 value) && value.Equals(item.Value);
		public bool Contains(KeyValuePair<T2, T1> item) => dictionary2.TryGetValue(item.Key, out T1 value) && value.Equals(item.Value);
		public bool ContainsKey(T1 key) => dictionary1.ContainsKey(key);
		public bool ContainsKey(T2 key) => dictionary2.ContainsKey(key);

		public void CopyTo(KeyValuePair<T1, T2>[] array, int arrayIndex) => ((IDictionary<T1, T2>)dictionary1).CopyTo(array, arrayIndex);

		public void CopyTo(KeyValuePair<T2, T1>[] array, int arrayIndex) => ((IDictionary<T2, T1>)dictionary2).CopyTo(array, arrayIndex);

		public bool Remove(T1 key)
		{
			if (!TryGetValue(key, out var value)) return false;
			dictionary1.Remove(key);
			dictionary2.Remove(value);
			return true;
		}

		public bool Remove(KeyValuePair<T1, T2> item)
		{
			if (!TryGetValue(item.Key, out var value)) return false;
			if (!item.Value.Equals(value)) return false;
			dictionary1.Remove(item.Key);
			dictionary2.Remove(value);
			return true;
		}

		public bool Remove(T2 key)
		{
			if (!TryGetValue(key, out var value)) return false;
			dictionary2.Remove(key);
			dictionary1.Remove(value);
			return true;
		}

		public bool TryGetValue(T1 key, out T2 value) => dictionary1.TryGetValue(key, out value);
		public bool TryGetValue(T2 key, out T1 value) => dictionary2.TryGetValue(key, out value);

		public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator() => dictionary1.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => dictionary1.GetEnumerator();
	}
}
