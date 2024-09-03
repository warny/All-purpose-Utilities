using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Utils.Objects;

namespace Utils.Arrays
{
	/// <summary>
	/// A comparer for <see cref="KeyValuePair{K, V}"/> objects that allows for custom comparison logic for keys and values.
	/// </summary>
	/// <typeparam name="K">The type of the key in the key-value pair.</typeparam>
	/// <typeparam name="V">The type of the value in the key-value pair.</typeparam>
	public class KeyValuePairComparer<K, V> : IEqualityComparer<KeyValuePair<K, V>>
	{
		private readonly QuickEqualityComparer<K> keyComparer;
		private readonly QuickEqualityComparer<V> valueComparer;

		/// <summary>
		/// Initializes a new instance of the <see cref="KeyValuePairComparer{K, V}"/> class with default equality comparers for both keys and values.
		/// </summary>
		public KeyValuePairComparer()
		{
			keyComparer = new QuickEqualityComparer<K>();
			valueComparer = new QuickEqualityComparer<V>();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="KeyValuePairComparer{K, V}"/> class with specified equality comparers for keys and values.
		/// </summary>
		/// <param name="keyComparer">The equality comparer to use for the keys. If null, a default comparer will be used.</param>
		/// <param name="valueComparer">The equality comparer to use for the values. If null, a default comparer will be used.</param>
		public KeyValuePairComparer(QuickEqualityComparer<K> keyComparer, QuickEqualityComparer<V> valueComparer)
		{
			this.keyComparer = keyComparer ?? new QuickEqualityComparer<K>();
			this.valueComparer = valueComparer ?? new QuickEqualityComparer<V>();
		}

		/// <summary>
		/// Determines whether the specified <see cref="KeyValuePair{K, V}"/> objects are equal.
		/// </summary>
		/// <param name="x">The first key-value pair to compare.</param>
		/// <param name="y">The second key-value pair to compare.</param>
		/// <returns><see langword="true"/> if the specified key-value pairs are equal; otherwise, <see langword="false"/>.</returns>
		public bool Equals(KeyValuePair<K, V> x, KeyValuePair<K, V> y)
			=> keyComparer.Equals(x.Key, y.Key) && valueComparer.Equals(x.Value, y.Value);

		/// <summary>
		/// Returns a hash code for the specified <see cref="KeyValuePair{K, V}"/>.
		/// </summary>
		/// <param name="obj">The key-value pair for which a hash code is to be generated.</param>
		/// <returns>A hash code for the specified key-value pair.</returns>
		public int GetHashCode([DisallowNull] KeyValuePair<K, V> obj)
			=> ObjectUtils.ComputeHash(
				keyComparer.GetHashCode(obj.Key),
				valueComparer.GetHashCode(obj.Value)
			);
	}
}
