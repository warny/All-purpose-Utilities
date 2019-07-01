using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Lists
{
	class IndexedList<K, V> : ICollection<V>, IReadOnlyDictionary<K, V>
	{
		private Dictionary<K, V> dictionary = new Dictionary<K, V>();
		private Func<V, K> getKey;

		public IndexedList( Func<V, K> getKey) {
			this.getKey = getKey;
		}

		public V this[K key] => dictionary[key];
		public int Count => dictionary.Count;
		public bool IsReadOnly => false;
		public IEnumerable<K> Keys => dictionary.Keys;
		public IEnumerable<V> Values => dictionary.Values;
		public void Add( V item ) => dictionary.Add(getKey(item), item);
		public bool Remove( V item ) => dictionary.Remove(getKey(item));
		public void Clear() => dictionary.Clear();
		public bool Contains( V item )=> dictionary.ContainsValue(item);
		public bool ContainsKey( K key ) => dictionary.ContainsKey(key);
		public void CopyTo( V[] array, int arrayIndex )	=>dictionary.Values.CopyTo(array, arrayIndex);
		public bool TryGetValue( K key, out V value ) => dictionary.TryGetValue(key, out value);
		IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator() => dictionary.GetEnumerator();
		IEnumerator<V> IEnumerable<V>.GetEnumerator() => dictionary.Values.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => dictionary.Values.GetEnumerator();
	}
}
