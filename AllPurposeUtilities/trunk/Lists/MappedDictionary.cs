using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Lists
{
	/// <summary>
	/// Liste dont la source est définie exterieurement
	/// </summary>
	/// <typeparam name="K">Type de la clef</typeparam>
	/// <typeparam name="V">Type des éléments</typeparam>
	public class MappedDictionary<K, V> : IReadOnlyDictionary<K, V>
	{
		private Func<K, V> GetValue;
		private Func<K, bool> RemoveValue;
		private Func<IEnumerable<KeyValuePair<K, V>>> GetValues;
		private Func<int> GetCount;

		public MappedDictionary(
			Func<K, V> GetValue,
			Func<K, bool> RemoveValue,
			Func<IEnumerable<KeyValuePair<K, V>>> GetValues,
			Func<int> GetCount = null
			)
		{
			this.GetValue = GetValue;
			this.RemoveValue = RemoveValue;
			this.GetValues = GetValues;
			this.GetCount = GetCount;
		}

		public V this[K key]
		{
			get { return GetValue(key); }
		}

		public bool Remove( K key )
		{
			return RemoveValue(key);
		}

		public int Count
		{
			get
			{
				if (GetCount!=null) {
					return GetCount();
				} else {
					return GetValues().Count();
				}
			}
		}

		public IEnumerable<K> Keys => GetValues().Select(v => v.Key);
		public IEnumerable<V> Values => GetValues().Select(v => v.Value);

		public bool ContainsKey( K key )
		{
			return Keys.Any(k => k.Equals(key));
		}

		public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
		{
			return GetValues().GetEnumerator();
		}

		public bool TryGetValue( K key, out V value )
		{
			try {
				value = GetValue(key);
				return true;
			} catch {
				value = default(V);
				return false;
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetValues().GetEnumerator();
		}
	}
}
