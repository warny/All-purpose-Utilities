using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Collections
{
	/// <summary>
	/// Liste indexée
	/// </summary>
	/// <typeparam name="K">Structure de la clef</typeparam>
	/// <typeparam name="V">Structure des valeurs</typeparam>
	public class IndexedList<K, V> : ICollection<V>, IReadOnlyDictionary<K, V>
	{
		private Dictionary<K, V> dictionary = new Dictionary<K, V>();
		private Func<V, K> getKey;

		/// <summary>
		/// Liste indexée
		/// </summary>
		/// <param name="getKey">Fonction d'extraction de la clef</param>
		public IndexedList( Func<V, K> getKey) {
			this.getKey = getKey;
		}

		/// <summary>
		/// Récupération d'une valeur
		/// </summary>
		/// <param name="key">clef</param>
		/// <returns>Valeur correspondante</returns>
		public V this[K key] => dictionary[key];
		/// <summary>
		/// Nombre de valeurs stockées
		/// </summary>
		public int Count => dictionary.Count;
		/// <summary>
		/// Lecture/Ecriture
		/// </summary>
		public bool IsReadOnly => false;
		/// <summary>
		/// Ensemble des clefs
		/// </summary>
		public IEnumerable<K> Keys => dictionary.Keys;
		/// <summary>
		/// Ensemble des valeurs
		/// </summary>
		public IEnumerable<V> Values => dictionary.Values;
		/// <summary>
		/// Ajout d'une valeur
		/// </summary>
		/// <param name="item">Valeur</param>
		public void Add( V item ) => dictionary.Add(getKey(item), item);
		/// <summary>
		/// Suppression d'une valeur
		/// </summary>
		/// <param name="item">Valeur à supprimer</param>
		/// <returns><see cref="true"/> si la valeur a été supprimée sinon <see cref="false"/></returns>
		public bool Remove( V item ) => dictionary.Remove(getKey(item));
		/// <summary>
		/// Suppression d'une valeur par sa clef
		/// </summary>
		/// <param name="key">Clef à supprimer</param>
		/// <returns><see cref="true"/> si la valeur a été supprimée sinon <see cref="false"/></returns>
		public bool Remove(K key) => dictionary.Remove(key);
		/// <summary>
		/// Suppresion de tous les éléments
		/// </summary>
		public void Clear() => dictionary.Clear();
		/// <summary>
		/// Indique si la liste contient la valeur passée en paramètre
		/// </summary>
		/// <param name="item">Valeur</param>
		/// <returns><see cref="true"/> si la valeur est dans la liste sinon <see cref="false"/></returns>
		public bool Contains( V item )=> dictionary.ContainsValue(item);
		/// <summary>
		/// Indique si la liste contient la clef passée en paramètre
		/// </summary>
		/// <param name="key">Clef</param>
		/// <returns><see cref="true"/> si la clef est dans la liste sinon <see cref="false"/></returns>
		public bool ContainsKey( K key ) => dictionary.ContainsKey(key);
		/// <summary>
		/// Copie vers un tableau
		/// </summary>
		/// <param name="array">Tableau cible</param>
		/// <param name="arrayIndex">Index de départ</param>
		public void CopyTo( V[] array, int arrayIndex )	=>dictionary.Values.CopyTo(array, arrayIndex);
		/// <summary>
		/// 
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool TryGetValue( K key, out V value ) => dictionary.TryGetValue(key, out value);
		IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator() => dictionary.GetEnumerator();
		IEnumerator<V> IEnumerable<V>.GetEnumerator() => dictionary.Values.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => dictionary.Values.GetEnumerator();
	}
}
