using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Utils.Collections;
using Utils.Objects;

namespace Utils.Arrays;

/// <summary>
/// Provides helper methods for manipulating arrays, including slicing and trimming utilities.
/// </summary>
public static class ArrayUtils
{
	/// <summary>
	/// Retrieves a slice (sub-part) of this array.
	/// The slice starts at the specified <paramref name="start"/> index and contains <paramref name="length"/> elements.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="s">The source array from which to extract the slice.</param>
	/// <param name="start">
	/// The starting index of the slice. Can be negative, in which case it counts from the end of the array.
	/// For example, -1 would refer to the last element.
	/// </param>
	/// <param name="length">The number of elements to extract.</param>
	/// <returns>
	/// A new array containing the requested slice.
	/// If <paramref name="start"/> is out of range, this may return <see cref="Array.Empty{T}()"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="s"/> is null.</exception>
	public static T[] Mid<T>(this T[] s, int start, int length)
	{
		ArgumentNullException.ThrowIfNull(s);

		// For negative indexes, shift by the array length.
		if (start < 0)
			start = s.Length + start;

		// If start is still beyond the beginning, return an empty array.
		if (start <= -length)
			return Array.Empty<T>();
		if (start < 0)
			return s.Copy(0, length + start);

		if (start >= s.Length)
			return Array.Empty<T>();

		// If the slice extends beyond the array length, copy from start to the end.
		if (start + length > s.Length)
			return s.Copy(start);

		return s.Copy(start, length);
	}

	/// <summary>
	/// Retrieves a slice (sub-part) of this array starting at the specified <paramref name="start"/> index to the end.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="s">The source array from which to extract the slice.</param>
	/// <param name="start">
	/// The starting index. Can be negative, in which case it counts from the end of the array.
	/// For example, -1 would refer to the last element.
	/// </param>
	/// <returns>
	/// A new array containing elements from <paramref name="start"/> to the end.
	/// Returns an empty array if <paramref name="start"/> is out of range.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="s"/> is null.</exception>
	public static T[] Mid<T>(this T[] s, int start)
	{
		ArgumentNullException.ThrowIfNull(s);

		if (start < 0)
			start = s.Length + start;
		if (start < 0)
			return s;
		if (start >= s.Length)
			return Array.Empty<T>();

		return s.Copy(start);
	}

	/// <summary>
	/// Returns a new array without the values at the start or end that match <paramref name="values"/>.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="obj">The source array.</param>
	/// <param name="values">Values to remove from both ends.</param>
	/// <returns>A new array with those values removed from the start and end.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="obj"/> or <paramref name="values"/> is null.</exception>
	public static T[] Trim<T>(this T[] obj, params T[] values)
	{
		ArgumentNullException.ThrowIfNull(obj);
		ArgumentNullException.ThrowIfNull(values);

		return obj.Trim(value => values.Contains(value));
	}

	/// <summary>
	/// Returns a new array without the elements at the start or end that match the given <paramref name="trimTester"/> predicate.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="obj">The source array.</param>
	/// <param name="trimTester">
	/// A function that returns <see langword="true"/> for elements that should be trimmed from the start and end.
	/// </param>
	/// <returns>A new array with the matching elements removed from both ends.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="obj"/> or <paramref name="trimTester"/> is null.</exception>
	public static T[] Trim<T>(this T[] obj, Func<T, bool> trimTester)
	{
		ArgumentNullException.ThrowIfNull(obj);
		ArgumentNullException.ThrowIfNull(trimTester);

		int start = 0;
		int end = obj.Length - 1;

		// Move start forward while elements match.
		while (start <= end && trimTester(obj[start]))
			start++;

		// Move end backward while elements match.
		while (end >= start && trimTester(obj[end]))
			end--;

		if (start > end)
			return Array.Empty<T>();

		int count = end - start + 1;
		T[] result = new T[count];
		Array.Copy(obj, start, result, 0, count);
		return result;
	}

	/// <summary>
	/// Returns a new array without the values at the start (those in <paramref name="values"/>).
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="obj">The source array.</param>
	/// <param name="values">Values to remove from the start.</param>
	/// <returns>A new array with those values removed from the start only.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="obj"/> or <paramref name="values"/> is null.</exception>
	public static T[] TrimStart<T>(this T[] obj, params T[] values)
	{
		ArgumentNullException.ThrowIfNull(obj);
		ArgumentNullException.ThrowIfNull(values);

		return obj.TrimStart(value => values.Contains(value));
	}

	/// <summary>
	/// Returns a new array without the elements at the start that match the given <paramref name="trimTester"/> predicate.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="obj">The source array.</param>
	/// <param name="trimTester">
	/// A function that returns <see langword="true"/> for elements that should be trimmed from the start.
	/// </param>
	/// <returns>A new array with matching elements removed from the start only.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="obj"/> or <paramref name="trimTester"/> is null.</exception>
	public static T[] TrimStart<T>(this T[] obj, Func<T, bool> trimTester)
	{
		ArgumentNullException.ThrowIfNull(obj);
		ArgumentNullException.ThrowIfNull(trimTester);

		int start = 0;
		int end = obj.Length;

		while (start < end && trimTester(obj[start]))
			start++;

		if (start >= end)
			return Array.Empty<T>();

		int count = end - start;
		T[] result = new T[count];
		Array.Copy(obj, start, result, 0, count);
		return result;
	}

	/// <summary>
	/// Returns a new array without the values at the end (those in <paramref name="values"/>).
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="obj">The source array.</param>
	/// <param name="values">Values to remove from the end.</param>
	/// <returns>A new array with those values removed from the end only.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="obj"/> or <paramref name="values"/> is null.</exception>
	public static T[] TrimEnd<T>(this T[] obj, params T[] values)
	{
		ArgumentNullException.ThrowIfNull(obj);
		ArgumentNullException.ThrowIfNull(values);

		return obj.TrimEnd(value => values.Contains(value));
	}

	/// <summary>
	/// Returns a new array without the elements at the end that match the given <paramref name="trimTester"/> predicate.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="obj">The source array.</param>
	/// <param name="trimTester">
	/// A function that returns <see langword="true"/> for elements that should be trimmed from the end.
	/// </param>
	/// <returns>A new array with matching elements removed from the end only.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="obj"/> or <paramref name="trimTester"/> is null.</exception>
	public static T[] TrimEnd<T>(this T[] obj, Func<T, bool> trimTester)
	{
		ArgumentNullException.ThrowIfNull(obj);
		ArgumentNullException.ThrowIfNull(trimTester);

		int start = 0;
		int end = obj.Length - 1;

		while (end >= start && trimTester(obj[end]))
			end--;

		if (start > end)
			return Array.Empty<T>();

		int count = end - start + 1;
		T[] result = new T[count];
		Array.Copy(obj, start, result, 0, count);
		return result;
	}

	/// <summary>
	/// Checks whether the array <paramref name="array"/> starts with the sequence <paramref name="prefix"/>.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="array">The source array.</param>
	/// <param name="prefix">The sequence to check at the start.</param>
	/// <returns>True if <paramref name="array"/> starts with <paramref name="prefix"/>, false otherwise.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="array"/> or <paramref name="prefix"/> is null.
	/// </exception>
	public static bool StartWith<T>(this T[] array, params T[] prefix)
	{
		ArgumentNullException.ThrowIfNull(array);
		ArgumentNullException.ThrowIfNull(prefix);

		if (prefix.Length > array.Length)
			return false;

		var comparer = EqualityComparer<T>.Default;
		for (int i = 0; i < prefix.Length; i++)
		{
			if (!comparer.Equals(array[i], prefix[i]))
				return false;
		}
		return true;
	}

	/// <summary>
	/// Checks whether the array <paramref name="array"/> ends with the sequence <paramref name="suffix"/>.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="array">The source array.</param>
	/// <param name="suffix">The sequence to check at the end.</param>
	/// <returns>True if <paramref name="array"/> ends with <paramref name="suffix"/>, false otherwise.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="array"/> or <paramref name="suffix"/> is null.
	/// </exception>
	public static bool EndWith<T>(this T[] array, params T[] suffix)
	{
		ArgumentNullException.ThrowIfNull(array);
		ArgumentNullException.ThrowIfNull(suffix);

		if (suffix.Length > array.Length)
			return false;

		var comparer = EqualityComparer<T>.Default;
		int start = array.Length - suffix.Length;
		for (int i = 0; i < suffix.Length; i++)
		{
			if (!comparer.Equals(array[i + start], suffix[i]))
				return false;
		}
		return true;
	}

	/// <summary>
	/// Copies a portion of the array <paramref name="array"/> (from <paramref name="start"/> over <paramref name="length"/> elements)
	/// into a new array.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="array">The source array.</param>
	/// <param name="start">The starting index of the slice.</param>
	/// <param name="length">The number of elements to copy.</param>
	/// <returns>A new array containing the copied elements.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="array"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="start"/> or <paramref name="length"/> is invalid.</exception>
	public static T[] Copy<T>(this T[] array, int start, int length)
	{
		ArgumentNullException.ThrowIfNull(array);
		if (start < 0 || length < 0 || start + length > array.Length)
			throw new ArgumentOutOfRangeException("The slice boundaries are outside the array.");

		T[] result = new T[length];
		Array.Copy(array, start, result, 0, length);
		return result;
	}

	/// <summary>
	/// Copies the array <paramref name="array"/> starting at <paramref name="start"/> until the end
	/// into a new array.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="array">The source array.</param>
	/// <param name="start">The starting index.</param>
	/// <returns>A new array containing elements from <paramref name="start"/> to the end.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="array"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="start"/> is invalid.</exception>
	public static T[] Copy<T>(this T[] array, int start)
	{
		ArgumentNullException.ThrowIfNull(array);
		if (start < 0 || start > array.Length)
			throw new ArgumentOutOfRangeException(nameof(start), "Start index is outside the array.");

		return array.Copy(start, array.Length - start);
	}

	/// <summary>
	/// Creates a copy of a multidimensional array (2D/3D/etc.).
	/// </summary>
	/// <param name="array">The source multidimensional array.</param>
	/// <returns>A new multidimensional array with the same dimensions and contents.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="array"/> is null.</exception>
	public static Array Copy(Array array)
	{
		ArgumentNullException.ThrowIfNull(array);

		var elementType = array.GetType().GetElementType();
		int rank = array.Rank;

		int[] lowerBounds = new int[rank];
		int[] lengths = new int[rank];
		for (int i = 0; i < rank; i++)
		{
			lowerBounds[i] = array.GetLowerBound(i);
			lengths[i] = array.GetLength(i);
		}

		var result = Array.CreateInstance(elementType, lengths, lowerBounds);
		Array.Copy(array, result, array.Length);
		return result;
	}

	/// <summary>
	/// Resizes the array to have length <paramref name="length"/>,
	/// padding on the left with <paramref name="value"/> if necessary.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="array">The source array.</param>
	/// <param name="length">The new length of the array.</param>
	/// <param name="value">The padding value to insert on the left if the array is enlarged.</param>
	/// <returns>A new array of size <paramref name="length"/>, padded on the left as needed.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="array"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="length"/> is smaller than the current array size.</exception>
	public static T[] PadLeft<T>(this T[] array, int length, T value = default)
	{
		ArgumentNullException.ThrowIfNull(array);
		if (array.Length > length)
			throw new ArgumentOutOfRangeException(nameof(length), "Target length is smaller than the original array length.");

		T[] result = new T[length];
		int start = length - array.Length;

		// Fill the left portion with the specified value.
		for (int i = 0; i < start; i++)
			result[i] = value;

		// Copy the original array.
		for (int i = 0; i < array.Length; i++)
			result[i + start] = array[i];

		return result;
	}

	/// <summary>
	/// Resizes the array to have length <paramref name="length"/>,
	/// padding on the right with <paramref name="value"/> if necessary.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="array">The source array.</param>
	/// <param name="length">The new length of the array.</param>
	/// <param name="value">The padding value to insert on the right if the array is enlarged.</param>
	/// <returns>A new array of size <paramref name="length"/>, padded on the right as needed.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="array"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="length"/> is smaller than the current array size.</exception>
	public static T[] PadRight<T>(this T[] array, int length, T value = default)
	{
		ArgumentNullException.ThrowIfNull(array);
		if (array.Length > length)
			throw new ArgumentOutOfRangeException(nameof(length), "Target length is smaller than the original array length.");

		T[] result = new T[length];
		// Copy the original array.
		for (int i = 0; i < array.Length; i++)
			result[i] = array[i];

		// Fill the remainder with the specified value.
		for (int i = array.Length; i < length; i++)
			result[i] = value;

		return result;
	}

	/// <summary>
	/// Adjusts the size of the array to <paramref name="fullLength"/>, optionally reversing the order of new elements if <paramref name="invert"/> is true.
	/// </summary>
	/// <typeparam name="T">Type of elements in the array.</typeparam>
	/// <param name="array">The source array.</param>
	/// <param name="invert">
	/// If true, the existing elements remain in the same order at the start of the new array.
	/// If false, they are placed starting from the end of the new array.
	/// </param>
	/// <param name="fullLength">The final desired length.</param>
	/// <returns>A new array of length <paramref name="fullLength"/> with the original elements in the specified arrangement.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="array"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="fullLength"/> is negative.</exception>
	public static T[] Adjust<T>(this T[] array, bool invert, int fullLength)
	{
		ArgumentNullException.ThrowIfNull(array);
		ArgumentOutOfRangeException.ThrowIfNegative(fullLength);

		int length = Math.Min(fullLength, array.Length);
		T[] result = new T[fullLength];
		Array.Clear(result, 0, fullLength);

		if (invert)
		{
			// Copy in normal order at the start
			for (int i = 0; i < length; i++)
			{
				result[i] = array[i];
			}
		}
		else
		{
			// Copy from the end
			for (int i = 0; i < length; i++)
			{
				result[fullLength - 1 - i] = array[i];
			}
		}

		return result;
	}

	/// <summary>
	/// Converts a collection of strings <paramref name="values"/> into an array of a specified <paramref name="elementType"/>.
	/// </summary>
	/// <param name="values">The source collection of strings.</param>
	/// <param name="elementType">The target element type.</param>
	/// <returns>
	/// A new array of type <paramref name="elementType"/> containing parsed values from <paramref name="values"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="values"/> or <paramref name="elementType"/> is null.</exception>
	/// <exception cref="FormatException">May be thrown if parsing fails for a given string.</exception>
	public static Array ConvertToArrayOf(this IEnumerable<string> values, Type elementType)
	{
		ArgumentNullException.ThrowIfNull(values);
		ArgumentNullException.ThrowIfNull(elementType);

		var results = new ArrayList();
		foreach (var value in values)
		{
			// Assume 'Parsers.Parse' converts string -> object based on 'elementType'.
			object parsedValue = Parsers.Parse(value, elementType);
			results.Add(parsedValue);
		}
		return results.ToArray(elementType);
	}

	/// <summary>
	/// Converts a collection of strings <paramref name="values"/> into an array of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The target element type.</typeparam>
	/// <param name="values">The source collection of strings.</param>
	/// <returns>A new array of type <typeparamref name="T"/> containing parsed values.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="values"/> is null.</exception>
	/// <exception cref="FormatException">May be thrown if parsing fails for a given string.</exception>
	public static T[] ConvertToArrayOf<T>(this IEnumerable<string> values)
		=> (T[])values.ConvertToArrayOf(typeof(T));

	/// <summary>
	/// Replaces non-overlapping occurrences of the subsequence <paramref name="toReplace"/> in <paramref name="array"/>
	/// with the subsequence <paramref name="replacement"/>.
	/// </summary>
	/// <typeparam name="T">Type of elements, must implement <see cref="IEquatable{T}"/> for equality checks.</typeparam>
	/// <param name="array">The source array in which to perform replacements.</param>
	/// <param name="toReplace">The subsequence to find and replace.</param>
	/// <param name="replacement">The subsequence to substitute in place of <paramref name="toReplace"/>.</param>
	/// <returns>A new array with the replacements applied where the pattern is matched.</returns>
	/// <remarks>
	/// <para>Overlapping matches are not detected. Only distinct, non-overlapping occurrences are replaced.</para>
	/// <para>If <paramref name="toReplace"/> is empty, no replacement is done and the original array is returned as-is.</para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="array"/>, <paramref name="toReplace"/>, or <paramref name="replacement"/> is null.
	/// </exception>
        public static T[] Replace<T>(this T[] array, T[] toReplace, T[] replacement)
                where T : IEquatable<T>
        {
                ArgumentNullException.ThrowIfNull(array);
                ArgumentNullException.ThrowIfNull(toReplace);
                ArgumentNullException.ThrowIfNull(replacement);

                // Assumes there's an EnumerableEx.Replace() extension to handle the logic.
                return EnumerableEx.Replace(array, toReplace, replacement).ToArray();
        }

        /// <summary>
        /// Determines whether the provided span begins with the specified prefix.
        /// </summary>
        /// <param name="span">The span to inspect.</param>
        /// <param name="prefix">The expected prefix.</param>
        /// <returns><see langword="true"/> when the span starts with <paramref name="prefix"/>.</returns>
        public static bool StartWith(this ReadOnlySpan<char> span, ReadOnlySpan<char> prefix)
        {
                if (prefix.Length > span.Length)
                        return false;

                for (int i = 0; i < prefix.Length; i++)
                {
                        if (span[i] != prefix[i])
                                return false;
                }

                return true;
        }
}
