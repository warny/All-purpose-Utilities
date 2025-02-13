using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Utils.Objects;

public static class ObjectUtils
{
	/// <summary>
	/// Returns true if the given nullable object is either null or equal to the default value of its type.
	/// </summary>
	/// <typeparam name="T">The underlying value type.</typeparam>
	/// <param name="nullableObj">The nullable object to check.</param>
	/// <returns>True if the object is null or its default value; otherwise, false.</returns>
	public static bool IsNullOrDefault<T>(this T? nullableObj) where T : struct
	{
		return !nullableObj.HasValue || nullableObj.Value.Equals(default(T));
	}

	/// <summary>
	/// Executes the specified function if the object is not null; otherwise, executes the fallback function.
	/// </summary>
	/// <typeparam name="T">The type of the object.</typeparam>
	/// <typeparam name="Result">The type of the result.</typeparam>
	/// <param name="value">The object to check.</param>
	/// <param name="ifNotNull">The function to execute if the object is not null.</param>
	/// <param name="ifNull">The function to execute if the object is null.</param>
	/// <returns>The result of the executed function.</returns>
	public static Result Do<T, Result>(this T value, Func<T, Result> ifNotNull, Func<Result> ifNull)
	{
		return value != null ? ifNotNull(value) : ifNull();
	}

	/// <summary>
	/// Executes the specified function if the object is not null; otherwise, returns a fallback value.
	/// </summary>
	/// <typeparam name="T">The type of the object.</typeparam>
	/// <typeparam name="Result">The type of the result.</typeparam>
	/// <param name="value">The object to check.</param>
	/// <param name="ifNotNull">The function to execute if the object is not null.</param>
	/// <param name="ifNull">The value to return if the object is null.</param>
	/// <returns>The result of the executed function or the fallback value.</returns>
	public static Result Do<T, Result>(this T value, Func<T, Result> ifNotNull, Result ifNull)
	{
		return value != null ? ifNotNull(value) : ifNull;
	}

	/// <summary>
	/// Asynchronously executes the specified function if the object is not null; otherwise, executes the fallback function.
	/// </summary>
	/// <typeparam name="T">The type of the object.</typeparam>
	/// <typeparam name="Result">The type of the result.</typeparam>
	/// <param name="value">The object to check.</param>
	/// <param name="ifNotNull">The function to execute if the object is not null.</param>
	/// <param name="ifNull">The function to execute if the object is null.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public static async Task<Result> DoAsync<T, Result>(this T value, Func<T, Result> ifNotNull, Func<Result> ifNull)
	{
		return await Task.Run(() => value != null ? ifNotNull(value) : ifNull());
	}

	/// <summary>
	/// Asynchronously executes the specified function if the object is not null; otherwise, returns a fallback value.
	/// </summary>
	/// <typeparam name="T">The type of the object.</typeparam>
	/// <typeparam name="Result">The type of the result.</typeparam>
	/// <param name="value">The object to check.</param>
	/// <param name="ifNotNull">The function to execute if the object is not null.</param>
	/// <param name="ifNull">The value to return if the object is null.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public static async Task<Result> DoAsync<T, Result>(this T value, Func<T, Result> ifNotNull, Result ifNull)
	{
		return await Task.Run(() => value != null ? ifNotNull(value) : ifNull);
	}

	/// <summary>
	/// Computes a hash code for a multi-dimensional array.
	/// </summary>
	/// <param name="array">The array for which to compute the hash code.</param>
	/// <returns>The computed hash code.</returns>
	public static int ComputeHash(this Array array)
	{
		array.Arg().MustNotBeNull();
		unchecked
		{
			int hash = 23;
			ComputeHashRecursive(0, new int[array.Rank], ref hash);
			return hash;
		}

		void ComputeHashRecursive(int rank, int[] indices, ref int hash)
		{
			if (rank == array.Rank)
			{
				hash = hash * 31 + array.GetValue(indices).GetHashCode();
			}
			else
			{
				for (int i = array.GetLowerBound(rank); i <= array.GetUpperBound(rank); i++)
				{
					indices[rank] = i;
					ComputeHashRecursive(rank + 1, indices, ref hash);
				}
			}
		}
	}

	/// <summary>
	/// Computes a hash code for a multi-dimensional array using a custom hash code function.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array.</typeparam>
	/// <param name="array">The array for which to compute the hash code.</param>
	/// <param name="getHashCode">The custom hash code function.</param>
	/// <returns>The computed hash code.</returns>
	public static int ComputeHash<T>(this Array array, Func<T, int> getHashCode)
	{
		array.Arg().MustNotBeNull();
		getHashCode.Arg().MustNotBeNull();

		unchecked
		{
			int hash = 23;
			ComputeHashRecursive(0, new int[array.Rank], ref hash);
			return hash;
		}

		void ComputeHashRecursive(int rank, int[] indices, ref int hash)
		{
			if (rank == array.Rank)
			{
				hash = hash * 31 + getHashCode((T)array.GetValue(indices));
			}
			else
			{
				for (int i = array.GetLowerBound(rank); i <= array.GetUpperBound(rank); i++)
				{
					indices[rank] = i;
					ComputeHashRecursive(rank + 1, indices, ref hash);
				}
			}
		}
	}

	/// <summary>
	/// Computes a hash code based on the hash codes of the given objects.
	/// </summary>
	/// <param name="objects">The objects to include in the hash computation.</param>
	/// <returns>The computed hash code.</returns>
	public static int ComputeHash(params object[] objects) => ComputeHash((IEnumerable<object>)objects);

	/// <summary>
	/// Computes a hash code based on the hash codes of the given objects.
	/// </summary>
	/// <param name="objects">The objects to include in the hash computation.</param>
	/// <returns>The computed hash code.</returns>
	public static int ComputeHash(this IEnumerable<object> objects)
	{
		unchecked
		{
			return objects.Aggregate(23, (acc, value) => acc * 31 + (value?.GetHashCode() ?? 0));
		}
	}

	/// <summary>
	/// Computes a hash code based on a custom hash function and the given objects.
	/// </summary>
	/// <typeparam name="T">The type of objects to hash.</typeparam>
	/// <param name="objects">The objects to include in the hash computation.</param>
	/// <param name="getHashCode">The custom hash function.</param>
	/// <returns>The computed hash code.</returns>
	public static int ComputeHash<T>(this IEnumerable<T> objects, Func<T, int> getHashCode)
	{
		unchecked
		{
			return objects.Aggregate(23, (acc, value) => acc * 31 + getHashCode(value));
		}
	}

	/// <summary>
	/// Computes a hash code based on a custom hash function and the given objects.
	/// </summary>
	/// <typeparam name="T">The type of objects to hash.</typeparam>
	/// <param name="objects">The objects to include in the hash computation.</param>
	/// <param name="getHashCode">The custom hash function.</param>
	/// <returns>The computed hash code.</returns>
	public static int ComputeHash<T>(this ReadOnlySpan<T> objects)
	{
		unchecked
		{
			var result = 23;
			for (int i = 0; i < objects.Length; i++)
			{
				result = result * 31 + objects[i].GetHashCode();
			}
			return result;
		}
	}

	/// <summary>
	/// Computes a hash code based on a custom hash function and the given objects.
	/// </summary>
	/// <typeparam name="T">The type of objects to hash.</typeparam>
	/// <param name="objects">The objects to include in the hash computation.</param>
	/// <param name="getHashCode">The custom hash function.</param>
	/// <returns>The computed hash code.</returns>
	public static int ComputeHash<T>(this ReadOnlySpan<T> objects, Func<T, int> getHashCode)
	{
		unchecked
		{
			var result = 23;
			for (int i = 0; i < objects.Length; i++)
			{
				result = result * 31 + getHashCode(objects[i]);
			}
			return result;
		}
	}

	/// <summary>
	/// Computes a hash code based on a custom hash function and the given objects.
	/// </summary>
	/// <typeparam name="T">The type of objects to hash.</typeparam>
	/// <param name="getHashCode">The custom hash function.</param>
	/// <param name="objects">The objects to include in the hash computation.</param>
	/// <returns>The computed hash code.</returns>
	public static int ComputeHash<T>(Func<T, int> getHashCode, params T[] objects) => ComputeHash((IEnumerable<T>)objects, getHashCode);

	/// <summary>
	/// Swaps the values of two objects.
	/// </summary>
	/// <typeparam name="T">The type of the objects.</typeparam>
	/// <param name="obj1">The first object.</param>
	/// <param name="obj2">The second object.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Swap<T>(ref T obj1, ref T obj2)
	{
		T temp = obj1;
		obj1 = obj2;
		obj2 = temp;
	}

	#region Comparisons

	/// <summary>
	/// Determines whether a value is between a lower and upper bound (inclusive).
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="lowerBound">The lower bound.</param>
	/// <param name="upperBound">The upper bound.</param>
	/// <returns>True if the value is between the bounds; otherwise, false.</returns>
	public static bool Between<T>(this T value, T lowerBound, T upperBound) where T : IComparable<T>
	{
		return value.CompareTo(lowerBound) >= 0 && value.CompareTo(upperBound) <= 0;
	}

	/// <summary>
	/// Determines whether a value is between a lower and upper bound with optional inclusivity.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="lowerBound">The lower bound.</param>
	/// <param name="upperBound">The upper bound.</param>
	/// <param name="includeLowerBound">Whether to include the lower bound in the comparison.</param>
	/// <param name="includeUpperBound">Whether to include the upper bound in the comparison.</param>
	/// <returns>True if the value is between the bounds with the specified inclusivity; otherwise, false.</returns>
	public static bool Between<T>(this T value, T lowerBound, T upperBound, bool includeLowerBound = true, bool includeUpperBound = true) where T : IComparable<T>
	{
		bool isGreaterThanLower = includeLowerBound ? value.CompareTo(lowerBound) >= 0 : value.CompareTo(lowerBound) > 0;
		bool isLessThanUpper = includeUpperBound ? value.CompareTo(upperBound) <= 0 : value.CompareTo(upperBound) < 0;
		return isGreaterThanLower && isLessThanUpper;
	}

	/// <summary>
	/// Determines whether a value is between a lower and upper bound using a custom comparer (inclusive).
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="comparer">The custom comparer to use.</param>
	/// <param name="lowerBound">The lower bound.</param>
	/// <param name="upperBound">The upper bound.</param>
	/// <returns>True if the value is between the bounds; otherwise, false.</returns>
	public static bool Between<T>(this T value, IComparer<T> comparer, T lowerBound, T upperBound)
	{
		return comparer.Compare(value, lowerBound) >= 0 && comparer.Compare(value, upperBound) <= 0;
	}

	/// <summary>
	/// Determines whether a value is between a lower and upper bound using a custom comparer with optional inclusivity.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="comparer">The custom comparer to use.</param>
	/// <param name="lowerBound">The lower bound.</param>
	/// <param name="upperBound">The upper bound.</param>
	/// <param name="includeLowerBound">Whether to include the lower bound in the comparison.</param>
	/// <param name="includeUpperBound">Whether to include the upper bound in the comparison.</param>
	/// <returns>True if the value is between the bounds with the specified inclusivity; otherwise, false.</returns>
	public static bool Between<T>(this T value, IComparer<T> comparer, T lowerBound, T upperBound, bool includeLowerBound, bool includeUpperBound)
	{
		bool isGreaterThanLower = includeLowerBound ? comparer.Compare(value, lowerBound) >= 0 : comparer.Compare(value, lowerBound) > 0;
		bool isLessThanUpper = includeUpperBound ? comparer.Compare(value, upperBound) <= 0 : comparer.Compare(value, upperBound) < 0;
		return isGreaterThanLower && isLessThanUpper;
	}

	/// <summary>
	/// Determines whether a value is contained in the specified array.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="objects">The array of values to check against.</param>
	/// <returns>True if the value is found in the array; otherwise, false.</returns>
	public static bool In<T>(this T value, params T[] objects) => objects.Contains(value);

	/// <summary>
	/// Determines whether a value is not contained in the specified array.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="objects">The array of values to check against.</param>
	/// <returns>True if the value is not found in the array; otherwise, false.</returns>
	public static bool NotIn<T>(this T value, params T[] objects) => !objects.Contains(value);

	/// <summary>
	/// Returns the index of the specified value in the enumerable, or -1 if not found.
	/// </summary>
	/// <typeparam name="T">The type of elements in the enumerable.</typeparam>
	/// <param name="enumeration">The enumerable to search.</param>
	/// <param name="toFind">The value to find.</param>
	/// <returns>The index of the value if found; otherwise, -1.</returns>
	public static int IndexOf<T>(this IEnumerable<T> enumeration, T toFind)
	{
		int index = 0;
		foreach (var element in enumeration)
		{
			if (element.Equals(toFind)) return index;
			index++;
		}
		return -1;
	}

	/// <summary>
	/// Returns the index of the specified value in the enumerable, using a custom comparer, or -1 if not found.
	/// </summary>
	/// <typeparam name="T">The type of elements in the enumerable.</typeparam>
	/// <param name="enumeration">The enumerable to search.</param>
	/// <param name="toFind">The value to find.</param>
	/// <param name="comparer">The custom comparer to use.</param>
	/// <returns>The index of the value if found; otherwise, -1.</returns>
	public static int IndexOf<T>(this IEnumerable<T> enumeration, T toFind, IEqualityComparer<T> comparer)
	{
		int index = 0;
		foreach (var element in enumeration)
		{
			if (comparer.Equals(element, toFind)) return index;
			index++;
		}
		return -1;
	}

	/// <summary>
	/// Returns the index of the first element in the enumerable that satisfies the specified condition, or -1 if not found.
	/// </summary>
	/// <typeparam name="T">The type of elements in the enumerable.</typeparam>
	/// <param name="enumeration">The enumerable to search.</param>
	/// <param name="predicate">The condition to satisfy.</param>
	/// <returns>The index of the first matching element if found; otherwise, -1.</returns>
	public static int IndexOf<T>(this IEnumerable<T> enumeration, Func<T, bool> predicate)
	{
		int index = 0;
		foreach (var element in enumeration)
		{
			if (predicate(element)) return index;
			index++;
		}
		return -1;
	}

	#endregion
}
