using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Utils.Arrays;
using Utils.Collections;

namespace Utils.Objects;

/// <summary>
/// A helper class to encapsulate element equality and hash logic, 
/// optimizing for <see cref="IEquatable{T}"/>, <see cref="IComparable{T}"/>, and array types.
/// </summary>
public sealed class QuickEqualityComparer<TElement> : IEqualityComparer<TElement>
{
    /// <summary>
    /// Gets a cached <see cref="IEqualityComparer{T}"/> instance suited for <typeparamref name="TElement"/>.
    /// </summary>
    public static readonly IEqualityComparer<TElement> Instance = CreateComparer();

    /// <summary>
    /// Initializes a new instance of the <see cref="QuickEqualityComparer{TElement}"/> class.
    /// </summary>
    /// <param name="comparer">Delegate that determines whether two values are equal.</param>
    /// <param name="hasher">Optional delegate used to compute the hash code for a value.</param>
    public QuickEqualityComparer(Func<TElement, TElement, bool> comparer, Func<TElement, int> hasher = null)
    {
        this.Comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        this.Hasher = hasher ?? (e => e.GetHashCode());
    }

    /// <summary>
    /// Compares two values using the delegate provided during construction.
    /// </summary>
    /// <param name="x">The first value to compare.</param>
    /// <param name="y">The second value to compare.</param>
    /// <returns><see langword="true"/> when the values are considered equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(TElement x, TElement y) => Comparer(x, y);

    /// <summary>
    /// Computes the hash code for the specified value.
    /// </summary>
    /// <param name="obj">The value for which to compute a hash code.</param>
    /// <returns>A hash code produced by the configured hashing delegate.</returns>
    public int GetHashCode(TElement obj) => Hasher(obj);

    private readonly Func<TElement, TElement, bool> Comparer;
    private readonly Func<TElement, int> Hasher;

    private static IEqualityComparer<TElement> CreateComparer()
    {
        var typeOfElement = typeof(TElement);

        if (typeof(IEqualityComparer<TElement>).IsAssignableFrom(typeOfElement))
            return EqualityComparer<TElement>.Default;

        if (typeOfElement.IsArray)
            return (IEqualityComparer<TElement>)Activator.CreateInstance(
                typeof(EnumerableEqualityComparer<>).MakeGenericType(typeOfElement.GetElementType()));

        return new QuickEqualityComparer<TElement>(
            EqualityComparer<TElement>.Default.Equals,
            EqualityComparer<TElement>.Default.GetHashCode
        );
    }
}

