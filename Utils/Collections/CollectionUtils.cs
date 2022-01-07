using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utils.Collections
{
	public static  class CollectionUtils
	{
		/// <summary>
		/// Returns an element from a dictionary with the specific key or <paramref name="defaultValue"/> if if doesn't exists
		/// </summary>
		/// <typeparam name="TKey">Key type</typeparam>
		/// <typeparam name="TValue">Value type</typeparam>
		/// <param name="dictionary">Dictionary</param>
		/// <param name="key">Key whose value to get</param>
		/// <param name="defaultValue">Default value</param>
		/// <returns>Value of specific key or default value</returns>
		/// <exception cref="ArgumentNullException">Raise if dictionary is null</exception>
		public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
		{
			_ = dictionary ?? throw new NullReferenceException();
			if (dictionary.TryGetValue(key, out TValue result)) return result;
			return defaultValue;
		}

		/// <summary>
		/// Returns true if collection is null or has no elements
		/// </summary>
		/// <typeparam name="T"></typeparam>
		public static bool IsNullOrEmptyCollection<T>(this IEnumerable<T> coll) => coll is null || !coll.Any();
	}
}
