using System.Collections.Generic;
using System;
using Utils.Collections;

/// <summary>
/// Encapsulates element comparison logic, optimizing for <see cref="IComparable{T}"/>, arrays, and custom comparers.
/// </summary>
public sealed class QuickComparer<TElement> : IComparer<TElement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuickComparer{TElement}"/> class.
    /// </summary>
    /// <param name="comparer">Delegate that compares two instances of <typeparamref name="TElement"/>.</param>
    public QuickComparer(Func<TElement, TElement, int> comparer)
    {
        this.Comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
    }

    /// <summary>
    /// Compares two values by invoking the delegate supplied at construction time.
    /// </summary>
    /// <param name="x">The first value to compare.</param>
    /// <param name="y">The second value to compare.</param>
    /// <returns>The comparison result produced by the injected delegate.</returns>
    public int Compare(TElement x, TElement y) => Comparer(x, y);

    private readonly Func<TElement, TElement, int> Comparer;
}
