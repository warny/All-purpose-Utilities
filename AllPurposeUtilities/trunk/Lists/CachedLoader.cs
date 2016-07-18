using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Lists
{
	/// <summary>
	/// Classe permettant de charger une ressource s'il celle-ci n'a pas été chargée avant
	/// </summary>
	/// <typeparam name="TKey"></typeparam>
	/// <typeparam name="TValue"></typeparam>
	public class CachedLoader<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IDictionary<TKey, TValue>
	{
		public CachedLoader()
		{
			AutoCreateObject = true;
		}

		/// <summary>
		/// Fonction de chargement des ressources
		/// </summary>
		private Func<TKey, TValue> loader;

		/// <summary>
		/// conteneur des ressources chargées
		/// </summary>
		private Dictionary<TKey, TValue> holder;

		/// <summary>
		/// Constructeur du chargeur de ressource
		/// </summary>
		/// <param name="loader">Fonction de chargement des ressources</param>
		public CachedLoader( Func<TKey, TValue> loader )
		{
			AutoCreateObject = true;
			this.holder = new Dictionary<TKey, TValue>();
			this.loader = loader;
		}

		public bool AutoCreateObject { get; set; }

		/// <summary>
		/// Recherche des ressources
		/// Si AutoCreateObject a la valeur true et que la recherche échoue, on ajoute une nouvelle entrée
		/// (key, loader(key)) dans le cache et on renvoie la valeur loader(key)
		/// </summary>
		/// <param name="key">Clef de la ressource à charger</param>
		/// <returns>Ressource chargée</returns>
		public TValue this[TKey key]
		{
			get
			{
				TValue val;
				if (holder.TryGetValue(key, out val)) {
					return val;
				}
				if (AutoCreateObject) {
					return Load(key);
				} else {
					return default(TValue);
				}
			}
			set
			{
				holder[key] = value;
			}
		}

		private TValue Load( TKey key )
		{
			TValue value = loader(key);
			holder.Add(key, value);
			return value;
		}

		#region IEnumerable<KeyValuePair<TKey,TValue>> Membres

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return holder.GetEnumerator();
		}

		#endregion

		#region IEnumerable Membres

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return holder.GetEnumerator();
		}

		#endregion

		#region IDictionary<TKey,TValue> Membres

		public void Add( TKey key, TValue value )
		{
			holder.Add(key, value);
		}

		public bool ContainsKey( TKey key )
		{
			return holder.ContainsKey(key);
		}

		public ICollection<TKey> Keys
		{
			get { return holder.Keys; }
		}

		public bool Remove( TKey key )
		{
			return holder.Remove(key);
		}

		/// <summary>
		/// Teste si une ressource existe dans le cache
		/// si AutoCreateObject a la valeur true, renvoie toujours true (puisque la ressource
		/// est ajouée dans le cache si elle n'existe pas)
		/// </summary>
		/// <param name="key">clé pour la recherche</param>
		/// <param name="value">
		///		(paramètre de sortie) ressource trouvée ou default&gt;TValue&lt; si ressource non trouvée
		/// </param>
		/// <returns>true si la ressource a été trouvée ou si AutoCreateObject==true</returns>
		public bool TryGetValue( TKey key, out TValue value )
		{
			if (holder.TryGetValue(key, out value)) return true;
			if (AutoCreateObject) {
				value = Load(key);
				return true;
			} else {
				value = default(TValue);
				return false;
			}
		}

		public ICollection<TValue> Values
		{
			get { return holder.Values; }
		}

		#endregion

		#region ICollection<KeyValuePair<TKey,TValue>> Membres

		public void Add( KeyValuePair<TKey, TValue> item )
		{
			holder.Add(item.Key, item.Value);
		}

		public void Clear()
		{
			holder.Clear();
		}

		public bool Contains( KeyValuePair<TKey, TValue> item )
		{
			return holder.ContainsKey(item.Key) ? holder[item.Key].Equals(item.Value) : false;
		}

		public void CopyTo( KeyValuePair<TKey, TValue>[] array, int arrayIndex )
		{
			if (array == null) throw new ArgumentNullException("array ne doit pas être null");
			if (arrayIndex < 0) throw new System.ArgumentOutOfRangeException("arrayIndex doit être supérieur à 0");
			if (holder.Count + arrayIndex > array.Length) throw new System.ArgumentException("La liste est trop longue pour être copiée dans le tableau");

			foreach (var item in holder) {
				array[arrayIndex++] = item;
			}
		}

		public int Count
		{
			get { return holder.Count; }
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		public bool Remove( KeyValuePair<TKey, TValue> item )
		{
			return holder.Remove(item.Key);
		}

		#endregion
	}
}
