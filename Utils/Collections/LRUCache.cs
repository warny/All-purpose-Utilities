using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Utils.Collections
{
	/// <summary>
	/// Classe pour cacher les elements utilisés récemments
	/// </summary>
	/// <typeparam name="K">Clef des élements</typeparam>
	/// <typeparam name="V">Type des éléments</typeparam>
	public class LRUCache<K, V> : IDictionary <K,V>
	{
		private int capacity;
		private Dictionary<K, LinkedListNode<KeyValuePair<K, V>>> cacheMap = new Dictionary<K, LinkedListNode<KeyValuePair<K, V>>>();
		private LinkedList<KeyValuePair<K, V>> lruList = new LinkedList<KeyValuePair<K, V>>();

		public LRUCache( int capacity )
		{
			this.capacity = capacity;
		}

		public ICollection<K> Keys=> lruList.Select (i=>i.Key).ToList();

		public ICollection<V> Values=>lruList.Select (i=>i.Value).ToList();

		public int Count=>lruList.Count;

		public bool IsReadOnly=>false;

		public V this[K key]
		{
			[MethodImpl(MethodImplOptions.Synchronized)]
			get
			{
				V value;
				TryGetValue(key, out value);
				return value;
			}

			set
			{
				this.Remove(key);
				this.Add(key, value);
			}
		}

		private void RemoveFirst()
		{
			// Remove from LRUPriority
			LinkedListNode<KeyValuePair<K, V>> node = lruList.First;
			lruList.RemoveFirst();

			// Remove from cache
			cacheMap.Remove(node.Value.Key);
		}

		public bool ContainsKey( K key ) => cacheMap.ContainsKey(key);

		[MethodImpl(MethodImplOptions.Synchronized)]
		public void Add( K key, V value )
		{
			if (cacheMap.Count >= capacity) {
				RemoveFirst();
			}

			KeyValuePair<K, V> cacheItem = new KeyValuePair<K, V>(key, value);
			LinkedListNode<KeyValuePair<K, V>> node = new LinkedListNode<KeyValuePair<K, V>>(cacheItem);
			lruList.AddLast(node);
			cacheMap.Add(key, node);
		}

		public bool Remove( K key )
		{
			LinkedListNode<KeyValuePair<K, V>> node;
			if (cacheMap.TryGetValue(key, out node)) {
				lruList.Remove(node);
				cacheMap.Remove(node.Value.Key);
				return true;
			}
			return false;
		}

		public bool TryGetValue( K key, out V value )
		{
			LinkedListNode<KeyValuePair<K, V>> node;
			if (cacheMap.TryGetValue(key, out node)) {
				value = node.Value.Value;
				lruList.Remove(node);
				lruList.AddLast(node);
				return true;
			}
			value = default(V);
			return false;
		}

		public void Add( KeyValuePair<K, V> item )
		{
			this.Add(item.Key, item.Value);
		}

		public void Clear()
		{
			lock (lruList) {
				lruList.Clear();
				cacheMap.Clear();
			}
		}

		public bool Contains( KeyValuePair<K, V> item )=> lruList.Contains(item);

		public void CopyTo( KeyValuePair<K, V>[] array, int arrayIndex )
		{
			foreach (var item in lruList) {
				array[arrayIndex ++] = new KeyValuePair<K, V>(item.Key, item.Value);
			}
		}

		public bool Remove( KeyValuePair<K, V> item )=>this.Remove (item.Key);
		public IEnumerator<KeyValuePair<K, V>> GetEnumerator()=>lruList.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => lruList.GetEnumerator();
	}

}

