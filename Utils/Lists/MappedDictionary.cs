using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Lists
{
	/// <summary>
	/// Liste dont la source est définie par des accesseurs
	/// </summary>
	/// <typeparam name="K">Type de la clef</typeparam>
	/// <typeparam name="V">Type des éléments</typeparam>
	public class MappedDictionary<K, V> : IReadOnlyDictionary<K, V>
	{
		IMappable<K, V> mapper;
		/// <summary>
		/// Créé un accesseur
		/// </summary>
		/// <param name="GetValue">Accesseur d'une valeur seule</param>
		/// <param name="RemoveValue">Suppression d'une valeur de la liste</param>
		/// <param name="GetValues">Récupération de toutes les valeurs de la liste</param>
		/// <param name="GetCount">Récupération du nombre de valeur de la liste</param>
		public MappedDictionary(
			Func<K, V> GetValue,
			Func<K, bool> RemoveValue,
			Func<IEnumerable<KeyValuePair<K, V>>> GetValues,
			Func<int> GetCount = null
			)
		{
			mapper = new Mapped<K, V>(GetValue, RemoveValue, GetValues, GetCount);
		}

		/// <summary>
		/// Créé un accesseur
		/// </summary>
		/// <param name="mapper">Classe définissant les accesseurs</param>
		public MappedDictionary(IMappable<K, V> mapper)
		{
			this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
		}

		public V this[K key]
		{
			get { return mapper.GetValue(key); }
		}

		public bool Remove( K key )
		{
			return mapper.Remove(key);
		}

		public int Count
		{
			get
			{
				if (mapper.CanCount) {
					return mapper.GetCount();
				} else {
					return mapper.GetValues().Count();
				}
			}
		}

		public IEnumerable<K> Keys => mapper.GetValues().Select(v => v.Key);
		public IEnumerable<V> Values => mapper.GetValues().Select(v => v.Value);

		public bool ContainsKey( K key )
		{
			return Keys.Any(k => k.Equals(key));
		}

		public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
		{
			return mapper.GetValues().GetEnumerator();
		}

		public bool TryGetValue( K key, out V value )
		{
			try {
				value = mapper.GetValue(key);
				return true;
			} catch {
				value = default(V);
				return false;
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return mapper.GetValues().GetEnumerator();
		}
	}

	public interface IMappable<K, V>
	{
		V GetValue(K key);
		bool Remove(K key);
		IEnumerable<KeyValuePair<K, V>> GetValues();
		int GetCount();
		bool CanCount { get; }
	}

	class Mapped<K, V> : IMappable<K, V>
	{
		private readonly Func<K, V> getValue;
		private readonly Func<K, bool> removeValue;
		private readonly Func<IEnumerable<KeyValuePair<K, V>>> getValues;
		private readonly Func<int> getCount;

		public Mapped(
			Func<K, V> getValue, 
			Func<K, bool> removeValue, 
			Func<IEnumerable<KeyValuePair<K, V>>> getValues, 
			Func<int> getCount
		)
		{
			this.getValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
			this.removeValue = removeValue ?? throw new ArgumentNullException(nameof(removeValue));
			this.getValues = getValues ?? throw new ArgumentNullException(nameof(getValues));
			this.getCount = getCount;
		}

		public int GetCount() => getCount();
		public V GetValue(K key) => getValue(key);
		public IEnumerable<KeyValuePair<K, V>> GetValues() => getValues();
		public bool Remove(K key) => removeValue(key);
		public bool CanCount => getCount is not null;
	}

}
