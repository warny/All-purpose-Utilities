using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.Objects;

namespace Utils.Mathematics;

/// <summary>
/// Provides extended mathematical functions and utilities, including custom rounding,
/// clamping, and Pascal's triangle generation.
/// </summary>
public static class MathEx
{
	/// <summary>
	/// Constant factor to convert degrees to radians.
	/// </summary>
	public const double Deg2Rad = Math.PI / 180.0;

	/// <summary>
	/// Constant factor to convert radians to degrees.
	/// </summary>
	public const double Rad2Deg = 180.0 / Math.PI;

	#region Modulus

	/// <summary>
	/// Computes a mathematical modulo that always returns a non-negative result,
	/// unlike the built-in '%' operator which can yield negative remainders.
	/// The result is always in the range [0, <paramref name="b"/>).
	/// <example>
	/// <code>
	/// -1 % 3 = -1
	/// Mod(-1, 3) = 2
	/// </code>
	/// </example>
	/// </summary>
	/// <typeparam name="T">A numeric type supporting modulus and addition operators.</typeparam>
	/// <param name="a">Dividend.</param>
	/// <param name="b">Divisor. Should be non-zero.</param>
	/// <returns>A non-negative remainder.</returns>
	public static T Mod<T>(T a, T b)
		where T : struct, IModulusOperators<T, T, T>, IAdditionOperators<T, T, T>
	{
		// Callers are responsible for ensuring b is not zero in typical usage.
		return ((a % b + b) % b);
	}

	#endregion

	#region Round

	/// <summary>
	/// Rounds <paramref name="value"/> to the nearest multiple of <paramref name="base"/>.
	/// If <paramref name="value"/> is exactly between two multiples, it is rounded up.
	/// </summary>
	/// <typeparam name="T">A numeric type supporting modulus and comparison operators.</typeparam>
	/// <param name="value">Value to be rounded.</param>
	/// <param name="base">Rounding base. Must be non-zero.</param>
	/// <returns>The value of <paramref name="value"/> rounded to the nearest multiple of <paramref name="base"/>.</returns>
	/// <exception cref="DivideByZeroException">Thrown if <paramref name="base"/> equals zero.</exception>
	public static T Round<T>(T value, T @base)
		where T : struct, INumber<T>
	{
		if (@base == T.Zero)
			throw new DivideByZeroException("Base must be non-zero for rounding.");

		T middle = @base / (T.One + T.One);
		T remainder = Mod(value, @base);

		// If the remainder is less than half of the base, round down; otherwise, round up.
		if (remainder < middle)
		{
			return value - remainder;
		}
		else
		{
			return value - remainder + @base;
		}
	}

	/// <summary>
	/// Rounds <paramref name="value"/> to the nearest power-of-ten multiple, specified by <paramref name="exponent"/>.
	/// For instance, <c>Round(1234, 2)</c> rounds to the nearest multiple of 100.
	/// </summary>
	/// <typeparam name="T">A numeric type that supports exponentiation via <see cref="IPowerFunctions{TSelf}"/>.</typeparam>
	/// <param name="value">Value to be rounded.</param>
	/// <param name="exponent">The exponent of 10 used as the rounding base. Defaults to 0 (which rounds to integer).</param>
	/// <returns>A value rounded to the nearest power-of-ten multiple.</returns>
	public static T Round<T>(T value, int exponent = 0)
		where T : struct, INumber<T>, IPowerFunctions<T>
	{
		// base = 10^exponent
		T @base = T.Pow(
			(T)Convert.ChangeType(10, typeof(T)),
			(T)Convert.ChangeType(exponent, typeof(T))
		);

		return Round(value, @base);
	}

	#endregion

	#region Floor

	/// <summary>
	/// Computes the greatest multiple of <paramref name="base"/> that is less than or equal to <paramref name="value"/>.
	/// </summary>
	/// <typeparam name="T">A numeric type supporting modulus and basic arithmetic operators.</typeparam>
	/// <param name="value">The value for which to find the floor multiple.</param>
	/// <param name="base">The base multiple. Must be non-zero.</param>
	/// <returns>The greatest multiple of <paramref name="base"/> that is &lt;= <paramref name="value"/>.</returns>
	/// <exception cref="DivideByZeroException">Thrown if <paramref name="base"/> equals zero.</exception>
	public static T Floor<T>(T value, T @base)
		where T : struct, IModulusOperators<T, T, T>, INumberBase<T>
	{
		if (@base == T.Zero)
			throw new DivideByZeroException("Base must be non-zero for floor operation.");

		T correction = Mod(value, @base);
		return value - correction;
	}

	#endregion

	#region Ceiling

	/// <summary>
	/// Computes the smallest multiple of <paramref name="base"/> that is greater than or equal to <paramref name="value"/>.
	/// </summary>
	/// <typeparam name="T">A numeric type supporting modulus and basic arithmetic operators.</typeparam>
	/// <param name="value">The value for which to find the ceiling multiple.</param>
	/// <param name="base">The base multiple. Must be non-zero.</param>
	/// <returns>The smallest multiple of <paramref name="base"/> that is &gt;= <paramref name="value"/>.</returns>
	/// <exception cref="DivideByZeroException">Thrown if <paramref name="base"/> equals zero.</exception>
	public static T Ceiling<T>(T value, T @base)
		where T : struct, IModulusOperators<T, T, T>, INumberBase<T>
	{
		if (@base == T.Zero)
			throw new DivideByZeroException("Base must be non-zero for ceiling operation.");

		T floorValue = Floor(value, @base);
		return floorValue == value ? value : floorValue + @base;
	}

	#endregion

	#region MinMax

	/// <summary>
	/// Returns the minimum value from the specified <paramref name="values"/>.
	/// </summary>
	/// <typeparam name="T">A type that implements <see cref="IComparable{T}"/>.</typeparam>
	/// <param name="values">One or more values to compare.</param>
	/// <returns>The minimum value among the input set.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="values"/> is empty.</exception>
	public static T Min<T>(params T[] values) where T : IComparable<T>
	{
		if (values is null || values.Length == 0)
			throw new ArgumentException("At least one value must be provided.", nameof(values));

		T result = values[0];
		for (int i = 1; i < values.Length; i++)
		{
			T value = values[i];
			if (value.CompareTo(result) < 0)
			{
				result = value;
			}
		}
		return result;
	}

	/// <summary>
	/// Returns the minimum value from the specified <paramref name="values"/>, using a custom <paramref name="comparer"/>.
	/// </summary>
	/// <typeparam name="T">Type of the elements being compared.</typeparam>
	/// <param name="comparer">An <see cref="IComparer{T}"/> implementation.</param>
	/// <param name="values">One or more values to compare.</param>
	/// <returns>The minimum value among the input set.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="values"/> is empty.</exception>
	public static T Min<T>(IComparer<T> comparer, params T[] values)
	{
		if (values is null || values.Length == 0)
			throw new ArgumentException("At least one value must be provided.", nameof(values));
		if (comparer is null)
			throw new ArgumentNullException(nameof(comparer));

		T result = values[0];
		for (int i = 1; i < values.Length; i++)
		{
			T value = values[i];
			if (comparer.Compare(value, result) < 0)
			{
				result = value;
			}
		}
		return result;
	}

	/// <summary>
	/// Returns the maximum value from the specified <paramref name="values"/>.
	/// </summary>
	/// <typeparam name="T">A type that implements <see cref="IComparable{T}"/>.</typeparam>
	/// <param name="values">One or more values to compare.</param>
	/// <returns>The maximum value among the input set.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="values"/> is empty.</exception>
	public static T Max<T>(params T[] values) where T : IComparable<T>
	{
		if (values is null || values.Length == 0)
			throw new ArgumentException("At least one value must be provided.", nameof(values));

		T result = values[0];
		for (int i = 1; i < values.Length; i++)
		{
			T value = values[i];
			if (value.CompareTo(result) > 0)
			{
				result = value;
			}
		}
		return result;
	}

	/// <summary>
	/// Returns the maximum value from the specified <paramref name="values"/>, using a custom <paramref name="comparer"/>.
	/// </summary>
	/// <typeparam name="T">Type of the elements being compared.</typeparam>
	/// <param name="comparer">An <see cref="IComparer{T}"/> implementation.</param>
	/// <param name="values">One or more values to compare.</param>
	/// <returns>The maximum value among the input set.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="values"/> is empty.</exception>
	public static T Max<T>(IComparer<T> comparer, params T[] values)
	{
		if (values is null || values.Length == 0)
			throw new ArgumentException("At least one value must be provided.", nameof(values));
		if (comparer is null)
			throw new ArgumentNullException(nameof(comparer));

		T result = values[0];
		for (int i = 1; i < values.Length; i++)
		{
			T value = values[i];
			if (comparer.Compare(value, result) > 0)
			{
				result = value;
			}
		}
		return result;
	}

	/// <summary>
	/// Clamps <paramref name="value"/> between <paramref name="min"/> and <paramref name="max"/>.
	/// Returns <paramref name="min"/> if <paramref name="value"/> is less than <paramref name="min"/>, 
	/// and <paramref name="max"/> if <paramref name="value"/> is greater than <paramref name="max"/>.
	/// </summary>
	/// <typeparam name="T">A type that implements <see cref="IComparable{T}"/>.</typeparam>
	/// <param name="value">Value to clamp.</param>
	/// <param name="min">Minimum limit (must be less than or equal to <paramref name="max"/>).</param>
	/// <param name="max">Maximum limit (must be greater than or equal to <paramref name="min"/>).</param>
	/// <returns>A clamped value between <paramref name="min"/> and <paramref name="max"/>.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="min"/> is greater than <paramref name="max"/>.</exception>
	public static T Clamp<T>(this T value, T min, T max) where T : IComparable<T>
	{
		if (min.CompareTo(max) > 0)
			throw new ArgumentException("Min must be less than or equal to Max.");

		if (value.CompareTo(min) < 0) return min;
		if (value.CompareTo(max) > 0) return max;
		return value;
	}

	/// <summary>
	/// Clamps <paramref name="value"/> between <paramref name="min"/> and <paramref name="max"/>, 
	/// using a custom <paramref name="comparer"/>.
	/// Returns <paramref name="min"/> if <paramref name="value"/> is less than <paramref name="min"/>, 
	/// and <paramref name="max"/> if <paramref name="value"/> is greater than <paramref name="max"/>.
	/// </summary>
	/// <typeparam name="T">Type to clamp.</typeparam>
	/// <param name="value">Value to clamp.</param>
	/// <param name="min">Minimum limit (must be less than or equal to <paramref name="max"/>).</param>
	/// <param name="max">Maximum limit (must be greater than or equal to <paramref name="min"/>).</param>
	/// <param name="comparer">Custom comparer for determining ordering.</param>
	/// <returns>A clamped value between <paramref name="min"/> and <paramref name="max"/>.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="min"/> is greater than <paramref name="max"/>.</exception>
	public static T Clamp<T>(this T value, T min, T max, IComparer<T> comparer)
	{
		if (comparer.Compare(min, max) > 0)
			throw new ArgumentException("Min must be less than or equal to Max.");

		if (comparer.Compare(value, min) < 0) return min;
		if (comparer.Compare(value, max) > 0) return max;
		return value;
	}

	#endregion

	/// <summary>
	/// A simple cache storing known lines of Pascal's triangle, keyed by the line number.
	/// </summary>
	private static readonly Dictionary<int, int[]> pascalTriangleCache = new()
	{
		{ 0, new[] { 1 } },
		{ 1, new[] { 1, 1 } },
		{ 2, new[] { 1, 2, 1 } },
		{ 3, new[] { 1, 3, 3, 1 } },
		{ 4, new[] { 1, 4, 6, 4, 1 } },
		{ 5, new[] { 1, 5, 10, 10, 5, 1 } },
		{ 6, new[] { 1, 6, 15, 20, 15, 6, 1 } }
	};

	/// <summary>
	/// Computes and returns the specified line of Pascal's triangle, zero-based.
	/// Uses a cached dictionary for performance. 
	/// If the line is not in the cache, it is calculated and then stored.
	/// </summary>
	/// <param name="lineNumber">Zero-based index of the line to compute. Must be &gt;= 0.</param>
	/// <returns>An array representing the requested line of Pascal's triangle.</returns>
	/// <remarks>
	/// The function updates the cache dynamically up to <paramref name="lineNumber"/>.
	/// Note that this static cache is not thread-safe; use locking if multiple threads will access it concurrently.
	/// </remarks>
	public static int[] ComputePascalTriangleLine(int lineNumber)
	{
		lineNumber.ArgMustBeGreaterOrEqualsThan(0);

		// Return if it is already in the cache
		if (pascalTriangleCache.TryGetValue(lineNumber, out var pascalTriangleLine))
		{
			return pascalTriangleLine;
		}

		// Compute from the highest known line up to the requested line
		int maxLine = pascalTriangleCache.Keys.Max();
		int[] lastLine = pascalTriangleCache[maxLine];

		for (int i = maxLine + 1; i <= lineNumber; i++)
		{
			var newLine = new int[i + 1];
			newLine[0] = 1;
			for (int j = 1; j < i; j++)
			{
				newLine[j] = lastLine[j - 1] + lastLine[j];
			}
			newLine[^1] = 1;

			pascalTriangleCache[i] = newLine;
			lastLine = newLine;
		}

		return lastLine;
	}
}
