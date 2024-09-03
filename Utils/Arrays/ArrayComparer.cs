using System;
using System.Linq;
using System.Collections.Generic;

namespace Utils.Arrays;

/// <summary>
/// A comparer for comparing two read-only collections of comparable elements.
/// </summary>
/// <typeparam name="T">The type of elements in the collections.</typeparam>
public class ArrayComparer<T> : IComparer<IReadOnlyCollection<T>>
{
	private readonly Func<T, T, int> comparer;
	private readonly Type typeOfT = typeof(T);

	/// <summary>
	/// Initializes a new instance of the <see cref="ArrayComparer{T}"/> class.
	/// </summary>
	/// <param name="comparers">Optional external comparers or comparison methods.</param>
	/// <exception cref="NotSupportedException">Thrown when the type <typeparamref name="T"/> does not support comparison.</exception>
	public ArrayComparer(params object[] comparers)
	{
		var externalComparer = comparers.OfType<IComparer<T>>().FirstOrDefault();
		if (externalComparer != null)
		{
			comparer = externalComparer.Compare;
		}
		else if (typeof(IComparable<T>).IsAssignableFrom(typeOfT))
		{
			comparer = (e1, e2) => ((IComparable<T>)e1).CompareTo(e2);
		}
		else if (typeOfT.IsArray)
		{
			var typeOfElement = typeOfT.GetElementType();
			Type arrayComparerGenericType = typeof(ArrayComparer<>);
			Type arrayComparerType = arrayComparerGenericType.MakeGenericType(typeOfElement);
			object subComparer = Activator.CreateInstance(arrayComparerType, [comparers]);
			comparer = (Func<T, T, int>)arrayComparerType
				.GetMethod(nameof(Compare), [typeOfT, typeOfT])
				.CreateDelegate(typeof(Func<T, T, int>), subComparer);
		}
		else if (typeof(IComparable).IsAssignableFrom(typeOfT))
		{
			comparer = (e1, e2) => ((IComparable)e1).CompareTo(e2);
		}
		else
		{
			throw new NotSupportedException($"The type {typeof(T).Name} does not support comparison.");
		}
	}

	/// <summary>
	/// Compares two read-only collections of type <typeparamref name="T"/>.
	/// </summary>
	/// <param name="x">The first collection to compare.</param>
	/// <param name="y">The second collection to compare.</param>
	/// <returns>
	/// A signed integer that indicates the relative values of <paramref name="x"/> and <paramref name="y"/>.
	/// </returns>
	public int Compare(IReadOnlyCollection<T> x, IReadOnlyCollection<T> y)
	{
		if (x is null && y is null) return 0;
		if (x is null) return -1;
		if (y is null) return 1;

		using var enumx = x.GetEnumerator();
		using var enumy = y.GetEnumerator();

		while (true)
		{
			bool readx = enumx.MoveNext();
			bool ready = enumy.MoveNext();

			if (!readx && !ready) return 0; // Both enumerators are done, collections are equal.
			if (!readx) return -1; // x is shorter than y.
			if (!ready) return 1;  // y is shorter than x.

			var comparison = comparer(enumx.Current, enumy.Current);
			if (comparison != 0) return comparison; // Found a difference, return it.
		}
	}
}
