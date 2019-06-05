using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
		/// Indique si un objet est une valeur numérique
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool IsNumeric( object value )
		{
			return value is double || value is float || value is long || value is int  || value is short  || value is byte  || value is ulong  || value is uint  || value is ushort  || value is decimal;
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


	}
}
