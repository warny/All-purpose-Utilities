using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Utils.Objects;

/// <summary>
/// Specialized comparer for sequences of <see cref="char"/> (e.g., strings, char arrays, lists).
/// Ensures efficient equality checks between strings, char arrays, spans, and other character sequences,
/// with support for culture-specific or case-insensitive comparisons.
/// </summary>
public sealed class CharsEqualityComparer : IEqualityComparer<IEnumerable<char>>
{
	private readonly StringComparer stringComparer;

	/// <summary>
	/// A thread-safe, cached instance using <see cref="StringComparer.Ordinal"/>.
	/// </summary>
	public static CharsEqualityComparer Ordinal { get; } = new CharsEqualityComparer(StringComparer.Ordinal);

	/// <summary>
	/// A thread-safe, cached instance using <see cref="StringComparer.CurrentCulture"/> for culture-aware comparisons.
	/// </summary>
	public static CharsEqualityComparer CurrentCulture { get; } = new CharsEqualityComparer(StringComparer.CurrentCulture);

	/// <summary>
	/// A thread-safe, cached instance using <see cref="StringComparer.OrdinalIgnoreCase"/> for case-insensitive comparisons.
	/// </summary>
	public static CharsEqualityComparer OrdinalIgnoreCase { get; } = new CharsEqualityComparer(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Initializes a new instance of the <see cref="CharsEqualityComparer"/> class using a custom string comparer.
	/// </summary>
	/// <param name="stringComparer">The comparer to use for string-based comparisons.</param>
	public CharsEqualityComparer(StringComparer stringComparer)
	{
		this.stringComparer = stringComparer ?? throw new ArgumentNullException(nameof(stringComparer));
	}

	/// <inheritdoc/>
	public bool Equals(IEnumerable<char> x, IEnumerable<char> y)
	{
		if (ReferenceEquals(x, y)) return true;
		if (x is null && y is null) return true;
		if (x is null || y is null) return false;

		// Try to get spans for both
		if (GetSpan(x, out var spanX) && GetSpan(y, out var spanY))
			return spanX.SequenceEqual(spanY);

		// Special handling for strings to respect localization
		if (x is string strX && y is string strY)
			return stringComparer.Equals(strX, strY);

		return CompareEnumerables(x, y);
	}

	/// <inheritdoc/>
	public int GetHashCode(IEnumerable<char> obj)
	{
		ArgumentNullException.ThrowIfNull(obj);

		if (obj is string str)
			return stringComparer.GetHashCode(str);

		return obj.ComputeHash(c => c.GetHashCode());
	}

	/// <summary>
	/// Compares two character enumerables by iterating through them.
	/// </summary>
	private static bool CompareEnumerables(IEnumerable<char> x, IEnumerable<char> y)
	{
		using var enumX = x.GetEnumerator();
		using var enumY = y.GetEnumerator();

		while (true)
		{
			bool hasNextX = enumX.MoveNext();
			bool hasNextY = enumY.MoveNext();

			if (!hasNextX && !hasNextY) return true; // Both sequences finished
			if (!hasNextX || !hasNextY || enumX.Current != enumY.Current)
				return false; // One sequence ended, or characters don't match
		}
	}

	/// <summary>
	/// Tries to retrieve a <see cref="ReadOnlySpan{T}"/> from an enumerable of characters.
	/// Returns <c>true</c> if successful, along with the extracted span.
	/// Otherwise, returns <c>false</c>.
	/// </summary>
	private static bool GetSpan(IEnumerable<char> obj, out ReadOnlySpan<char> span)
	{
		switch (obj)
		{
			case string s:
				span = s.AsSpan();
				return true;
			case char[] array:
				span = array;
				return true;
			case List<char> list:
				span = CollectionsMarshal.AsSpan(list);
				return true;
			default:
				span = default;
				return false;
		}
	}
}
