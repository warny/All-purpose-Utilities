using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Objects
{
	public static class ObjectUtils
	{
		/// <summary>
		/// Return true if the given object of type T? is null or is default value for type T 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="nullableObj"></param>
		/// <returns></returns>
		public static bool IsNullOrDefault<T>( this T? nullableObj ) where T : struct
		{
			if (!nullableObj.HasValue) return true;
			return nullableObj.Equals(default(T));
		}
		 
		/// <summary>
		/// Returns true if collection is null or has no elements
		/// </summary>
		/// <typeparam name="T"></typeparam>
		public static bool IsNullOrEmptyCollection<T>( this IEnumerable<T> coll )
		{
			return coll == null || !coll.Any();
		}

		/// <summary>
		/// Compute a hash from the hashes of the given objects
		/// </summary>
		/// <param name="objects"></param>
		/// <returns></returns>
		public static int ComputeHash(params object[] objects)
		{
			unchecked
			{
				return objects.Aggregate(23, (value, acc) => acc.GetHashCode() * 31 + value);
			}
		}

		/// <summary>
		/// Compute a hash from the hashes of the given objects
		/// </summary>
		/// <param name="objects"></param>
		/// <returns></returns>
		public static int ComputeHash<T>(Func<T, int> getHashCode, params T[] objects)
		{
			unchecked
			{
				return objects.Aggregate(23, (value, acc) => getHashCode(acc) * 31 + value);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="obj1"></param>
		/// <param name="obj2"></param>
		public static void Swap<T>( ref T obj1, ref T obj2 )
		{
			T temp;
			temp = obj1;
			obj1 = obj2;
			obj2 = temp;
		}

		public static T[] RandomArray<T>(this Random r, int minSize, int maxSize, Func<int ,T> value)
		{
			T[] result = new T[r.Next(minSize, maxSize)];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = value(i);
			}
			return result;
		}
	}
}
