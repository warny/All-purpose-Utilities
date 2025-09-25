using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Utils.Objects;

namespace Utils.Collections;

/// <summary>
/// A comparer for enumerables that allows comparison of sequences 
/// based on custom or default equality logic for each element.
/// 
/// If both sequences implement <see cref="IReadOnlyList{T}"/>, 
/// the comparison first checks <see cref="IReadOnlyCollection{T}.Count"/> for an early exit.
/// Uses <see cref="Span{T}"/> when applicable for performance.
/// </summary>
/// <typeparam name="T">The type of elements in the enumerable.</typeparam>
public sealed class EnumerableEqualityComparer<T> : IEqualityComparer<IEnumerable<T>>
{
	private readonly IEqualityComparer<T> elementComparer;

	/// <summary>
	/// A thread-safe, cached instance of <see cref="EnumerableEqualityComparer{T}"/>
	/// using the default comparison logic for elements.
	/// </summary>
	public static EnumerableEqualityComparer<T> Default { get; } = new EnumerableEqualityComparer<T>();

	/// <summary>
	/// Initializes a new instance of the <see cref="EnumerableEqualityComparer{T}"/> class
	/// using a provided element equality comparer.
	/// </summary>
	private EnumerableEqualityComparer() {
		Type tType = typeof(T);
		if (tType.IsArray)
		{
			var elementComparerType = typeof(EnumerableEqualityComparer<>).MakeGenericType(tType.GetElementType());
			var defaultProperty = elementComparerType.GetProperty(nameof(Default));
			this.elementComparer = (IEqualityComparer<T>)defaultProperty.GetValue(null);
		}
		else
		{
			this.elementComparer = EqualityComparer<T>.Default;
		}
	}


	/// <summary>
	/// Initializes a new instance of the <see cref="EnumerableEqualityComparer{T}"/> class
	/// using a provided element equality comparer.
	/// </summary>
	public EnumerableEqualityComparer(Func<T, T, bool> comparer, Func<T, int> hasher = null) : this(new QuickEqualityComparer<T>(comparer, hasher)) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="EnumerableEqualityComparer{T}"/> class
	/// using a provided element equality comparer.
	/// </summary>
	/// <param name="elementComparer">An equality comparer for elements of type <typeparamref name="T"/>.</param>
	public EnumerableEqualityComparer(IEqualityComparer<T> elementComparer)
	{
		ArgumentNullException.ThrowIfNull(elementComparer);
		this.elementComparer = elementComparer;
	}

	/// <inheritdoc/>
	public bool Equals(IEnumerable<T> x, IEnumerable<T> y)
	{
		if (ReferenceEquals(x, y)) return true;
		if (x is null || y is null) return false;

		// Try to get spans for both
		if (GetSpan(x, out var spanX) && GetSpan(y, out var spanY))
			return CompareSpans(spanX, spanY);

		// If both are IReadOnlyList<T>, compare Count first
		if (x is IReadOnlyList<T> roListX && y is IReadOnlyList<T> roListY)
		{
			if (roListX.Count != roListY.Count)
				return false;

			for (int i = 0; i < roListX.Count; i++)
			{
				if (!elementComparer.Equals(roListX[i], roListY[i]))
					return false;
			}
			return true;
		}

		// Fall back to enumeration-based comparison
		using var enumX = x.GetEnumerator();
		using var enumY = y.GetEnumerator();

		while (true)
		{
			bool hasNextX = enumX.MoveNext();
			bool hasNextY = enumY.MoveNext();

			if (!hasNextX && !hasNextY)
				return true;

			if (!hasNextX || !hasNextY || !elementComparer.Equals(enumX.Current, enumY.Current))
				return false;
		}
	}

	/// <inheritdoc/>
	public int GetHashCode(IEnumerable<T> obj)
	{
		ArgumentNullException.ThrowIfNull(obj);
		return obj.ComputeHash(elementComparer.GetHashCode);
	}

	/// <summary>
	/// Efficiently compares two spans using the element comparer.
	/// </summary>
	private bool CompareSpans(ReadOnlySpan<T> spanX, ReadOnlySpan<T> spanY)
	{
		if (spanX.Length != spanY.Length) return false;

		for (int i = 0; i < spanX.Length; i++)
		{
			if (!elementComparer.Equals(spanX[i], spanY[i]))
				return false;
		}
		return true;
	}

	/// <summary>
	/// Tries to retrieve a <see cref="Span{T}"/> from an enumerable.
	/// Returns <see langword="true"/> if successful, along with the extracted span.
	/// Otherwise, returns <see langword="false"/>.
	/// </summary>
	private static bool GetSpan(IEnumerable<T> obj, out ReadOnlySpan<T> span)
	{
		switch (obj)
		{
			case T[] array:
				span = array;
				return true;
			case List<T> list:
				span = CollectionsMarshal.AsSpan(list);
				return true;
			case Memory<T> memory:
				span = memory.Span;
				return true;
			case ReadOnlyMemory<T> rom:
				span = rom.Span;
				return true;
			default:
				span = default;
				return false;
		}
	}

}
