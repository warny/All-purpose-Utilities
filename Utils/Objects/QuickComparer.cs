using System.Collections.Generic;
using System;
using Utils.Collections;

/// <summary>
/// Encapsulates element comparison logic, optimizing for <see cref="IComparable{T}"/>, arrays, and custom comparers.
/// </summary>
public sealed class QuickComparer<TElement> : IComparer<TElement>
{
	public QuickComparer(Func<TElement, TElement, int> comparer)
	{
		this.Comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
	}

	public int Compare(TElement x, TElement y) => Comparer(x, y);

	private readonly Func<TElement, TElement, int> Comparer;
}
