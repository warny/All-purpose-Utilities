using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Lists
{
	public static class EnumerableEx
	{
        public static T GetValueValueOrDefault<K, T>(this IDictionary<K, T> d, K key, T defaultValue = default(T))
        {
            if (d.TryGetValue(key, out T value))
            {
                return value;
            }
            return defaultValue;
        }
    }
}
