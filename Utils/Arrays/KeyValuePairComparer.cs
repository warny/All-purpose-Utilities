using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Utils.Objects;

namespace Utils.Arrays
{
	public class KeyValuePairComparer<K, V> : IEqualityComparer<KeyValuePair<K, V>>
	{
		QuickEqualityComparer<K> keyComparer;
		QuickEqualityComparer<V> valueComparer;

		public KeyValuePairComparer()
		{
			keyComparer = new();
			valueComparer = new();
		}

		public KeyValuePairComparer(QuickEqualityComparer<K> keyComparer, QuickEqualityComparer<V> valueComparer)
		{
			this.keyComparer = keyComparer ?? new();
			this.valueComparer = valueComparer ?? new();
		}

		public bool Equals(KeyValuePair<K, V> x, KeyValuePair<K, V> y)
			=> keyComparer.Equals(x.Key, y.Key)
			&& valueComparer.Equals(x.Value, y.Value);

		public int GetHashCode([DisallowNull] KeyValuePair<K, V> obj) 
			=> ObjectUtils.ComputeHash(
				keyComparer.GetHashCode(obj.Key), 
				valueComparer.GetHashCode(obj.Value)
			);
	}
}
