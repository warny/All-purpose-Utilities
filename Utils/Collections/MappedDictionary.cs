using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Collections
{
	public class MappedDictionary<K, V> : ReadOnlyMappedDictionary<K, V>, IDictionary<K, V>
	{
		private IDictionaryMap<K, V> map;

		public MappedDictionary(IDictionaryMap<K, V> map) : 
			base(map)
		{
			this.map = map;
		}

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

		public MappedDictionary(
			IDictionary<K, V> baseDictionary,
			Action<K, V> addValue,
			Action<K, V> setValue,
			Func<K, bool> removeValue,
			Action clear) : 
			base(new DictionaryMap<K, V> (baseDictionary, addValue, setValue, removeValue, clear))
		{
		}

		public bool IsReadOnly => false;
		ICollection<K> IDictionary<K, V>.Keys { get; }
		ICollection<V> IDictionary<K, V>.Values { get; }

		public override V this[K key]
		{
			get => base[key];
			set => map.Set(key, value);
		}



		public void Add(K key, V value) => map.Add(key, value);
		public void Add(KeyValuePair<K, V> item) => map.Add(item.Key, item.Value);
		public void Clear() => map.Clear();
		public bool Contains(KeyValuePair<K, V> item) => map.Contains(item);

		public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
		{
			foreach (var kv in this) { array[arrayIndex++] = kv; }
		}

		public bool Remove(KeyValuePair<K, V> item) => map.Remove(item.Key);

		public bool Remove(K key) => map.Remove(key);
	}

	public interface IDictionaryMap<K, V> : IReadOnlyDictionaryMap<K, V>
	{
		bool Contains(KeyValuePair<K, V> kv);
		void Add(K key, V value);
		void Set(K key, V value);
		bool Remove(K key);
		void Clear();

	}

	public class DictionaryMap<K, V> : ReadOnlyDictionaryMap<K, V>, IDictionaryMap<K, V>
	{
		private readonly Func<KeyValuePair<K, V>, bool> containsKeyValue;
		private readonly Action<K, V> addValue;
		private readonly Action<K, V> setValue;
		private readonly Func<K, bool> removeValue;
		private readonly Action clear;
		private readonly Func<IEnumerable<K>> getKeys;
		private readonly Func<IEnumerable<V>> getValues;

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
			this.containsKeyValue = containsKeyValue.ArgMustNotBeNull();
			this.addValue = addValue.ArgMustNotBeNull();
			this.setValue = setValue.ArgMustNotBeNull();
			this.removeValue = removeValue.ArgMustNotBeNull();
			this.getKeys = getKeys ?? (() => getItems().Select(kv=>kv.Key));
			this.getValues = getValues ?? (() => getItems().Select(kv => kv.Value));
		}

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
			this.containsKeyValue = baseDictionary.Contains;
			this.addValue = addValue ?? throw new ArgumentNullException(nameof(addValue));
			this.setValue = setValue ?? throw new ArgumentNullException(nameof(setValue));
			this.removeValue = removeValue ?? throw new ArgumentNullException(nameof(removeValue));
			this.clear = clear ?? throw new ArgumentNullException(nameof(clear));
			this.getKeys = () => baseDictionary.Keys;
			this.getValues = () => baseDictionary.Values;
		}

		public bool Contains(KeyValuePair<K, V> kv) => containsKeyValue(kv);

		public void Add(K key, V value) => addValue(key, value);
		public void Set(K key, V value) => setValue(key, value);

		public void Clear() => clear();

		public bool Remove(K key) => removeValue(key);

	}
}
