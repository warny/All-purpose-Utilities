using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Utils.Objects;

public static class ObjectUtils
{
	/// <summary>
	/// Return true if the given object of type T? is null or is default value for type T 
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="nullableObj"></param>
	/// <returns></returns>
	public static bool IsNullOrDefault<T>(this T? nullableObj) where T : struct
	{
		if (!nullableObj.HasValue) return true;
		return nullableObj.Equals(default(T));
	}

	/// <summary>
	/// Execute the specified function for the current object
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="Result">The type of the esult.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="func">The function to execute for the current object.</param>
	/// <returns></returns>
	public static Result Do<T, Result>(this T value, Func<T, Result> func)
	{
		return func(value);
	}

	/// <summary>
	/// Execute the specified function for the current object
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="Result">The type of the esult.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="ifNotNull">The function to execute for the current object.</param>
	/// <param name="ifNull">The function to execute isf the object is null</param>
	/// <returns></returns>
	public static Result Do<T, Result>(this T value, Func<T, Result> ifNotNull, Func<Result> ifNull)
	{
		if (value == null)
		{
			return ifNull();
		}
		return ifNotNull(value);
	}

	/// <summary>
	/// Execute the specified function for the current object
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="Result">The type of the esult.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="ifNotNull">The function to execute for the current object.</param>
	/// <param name="ifNull">The value to return if the object is null</param>
	/// <returns></returns>
	public static Result Do<T, Result>(this T value, Func<T, Result> ifNotNull, Result ifNull)
	{
		if (value == null)
		{
			return ifNull;
		}
		return ifNotNull(value);
	}

	/// <summary>
	/// Calcul le hash d'un tableau multidimensionnel
	/// </summary>
	/// <param name="array"></param>
	/// <returns></returns>
	public static int ComputeHash(this Array array)
	{
		array.ArgMustNotBeNull();
		unchecked
		{
			int hash = 23;
			InnerComputeHash(0, new int[array.Rank], ref hash);
			return hash;
		}

		void InnerComputeHash(int r, int[] positions, ref int hash)
		{
			unchecked
			{
				if (r == positions.Length)
				{
					hash *= 31;
					hash += array.GetValue(positions).GetHashCode();
					return;
				}

				for (int i = array.GetLowerBound(r); i <= array.GetUpperBound(r); i++)
				{
					positions[r] = i;
					InnerComputeHash(r + 1, positions, ref hash);
				}
			}
		}
	}

	/// <summary>
	/// Calcul le hash d'un tableau multidimensionnel
	/// </summary>
	/// <param name="array"></param>
	/// <param name="getHashCode">Fonction de calcul de hash</param>
	/// <returns></returns>
	public static int ComputeHash<T>(this Array array, Func<T, int> getHashCode)
	{
		array.ArgMustNotBeNull();
		getHashCode.ArgMustNotBeNull();

		unchecked
		{
			int hash = 23;
			InnerComputeHash(0, new int[array.Rank], ref hash);
			return hash;
		}

		void InnerComputeHash(int r, int[] positions, ref int hash)
		{
			unchecked
			{
				if (r == positions.Length)
				{
					hash *= 31;
					hash += getHashCode((T)array.GetValue(positions));
					return;
				}

				for (int i = array.GetLowerBound(r); i <= array.GetUpperBound(r); i++)
				{
					positions[r] = i;
					InnerComputeHash(r + 1, positions, ref hash);
				}
			}
		}
	}

	/// <summary>
	/// Compute a hash from the hashes of the given objects
	/// </summary>
	/// <param name="objects"></param>
	/// <returns></returns>
	public static int ComputeHash(params object[] objects) => ComputeHash((IEnumerable<object>)objects);

	/// <summary>
	/// Compute a hash from the hashes of the given objects
	/// </summary>
	/// <param name="objects"></param>
	/// <returns></returns>
	public static int ComputeHash(this IEnumerable<object> objects)
	{
		unchecked
		{
			return objects.Aggregate(23, (acc, value) => value.GetHashCode() + acc * 31);
		}
	}

	/// <summary>
	/// Compute a hash from the hashes of the given objects
	/// </summary>
	/// <param name="objects"></param>
	/// <returns></returns>
	public static int ComputeHash<T>(Func<T, int> getHashCode, IEnumerable<T> objects)
	{
		unchecked
		{
			return objects.Aggregate(23, (acc, value) => getHashCode(value) + acc * 31);
		}
	}

	/// <summary>
	/// Compute a hash from the hashes of the given objects
	/// </summary>
	/// <param name="objects"></param>
	/// <returns></returns>
	public static int ComputeHash<T>(Func<T, int> getHashCode, params T[] objects) => ComputeHash(getHashCode, (IEnumerable<T>)objects);

	/// <summary>
	/// 
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="obj1"></param>
	/// <param name="obj2"></param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Swap<T>(ref T obj1, ref T obj2)
	{
		T temp;
		temp = obj1;
		obj1 = obj2;
		obj2 = temp;
	}

	public static T[] RandomArray<T>(this Random r, int minSize, int maxSize, Func<int, T> value)
	{
		T[] result = new T[r.Next(minSize, maxSize)];
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = value(i);
		}
		return result;
	}

	public static byte[] NextBytes(this Random r, int size)
	{
		byte[] result = new byte[size];
		r.NextBytes(result);
		return result;
	}

	public static byte[] NextBytes(this Random r, int minSize, int maxSize)
	{
		byte[] result = new byte[r.Next(minSize, maxSize)];
		r.NextBytes(result);
		return result;
	}
}
