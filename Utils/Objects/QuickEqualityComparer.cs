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
	public static readonly IEqualityComparer<TElement> Instance = CreateComparer();

	public QuickEqualityComparer(Func<TElement, TElement, bool> comparer, Func<TElement, int> hasher = null)
	{
		this.Comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
		this.Hasher = hasher ?? (e => e.GetHashCode());
	}

	public bool Equals(TElement x, TElement y) => Comparer(x, y);
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

