using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Utils.Arrays;

namespace Utils.Objects;

public class QuickEqualityComparer<T> : IEqualityComparer<T>
{
	private readonly Type typeOfT = typeof(T);
	private readonly Func<T, T, bool> areEquals;
	private readonly Func<T, int> getHashCode;

	public QuickEqualityComparer()
	{
		if (typeof(IEquatable<T>).IsAssignableFrom(typeOfT))
		{
			if (typeOfT.IsClass)
			{
				areEquals = (e1, e2)
					=> (e1 is null && e2 is null)
					|| (
						!(e1 is null || e2 is null)
						&& ((IEquatable<T>)e1).Equals(e2)
					);
			}
			else
			{
				areEquals = (e1, e2) => ((IEquatable<T>)e1).Equals(e2);
			}
		}
		else if (typeof(IComparable<T>).IsAssignableFrom(typeOfT))
		{
			if (typeOfT.IsClass)
			{
				areEquals = (e1, e2)
					=> (e1 is null && e2 is null)
					|| (
						!(e1 is null || e2 is null)
						&& ((IComparable<T>)e1).CompareTo(e2) == 0
					);
			}
			else
			{
				areEquals = (e1, e2) => ((IComparable<T>)e1).CompareTo(e2) == 0;
			}
		}
		else if (typeof(IComparable).IsAssignableFrom(typeOfT))
		{
			if (typeOfT.IsClass)
			{
				areEquals = (e1, e2)
					=> (e1 is null && e2 is null)
					&& !(e1 is null || e2 is null)
					&& ((IComparable)e1).CompareTo(e2) == 0;
			}
			else
			{
				areEquals = (e1, e2) => ((IComparable)e1).CompareTo(e2) == 0;
			}
		}
		else if (typeOfT.IsArray)
		{
			var typeOfElement = typeOfT.GetElementType();
			Type equalityComparerGenericType = typeof(MultiDimensionnalArrayEqualityComparer<>);
			Type equalityComparerType = equalityComparerGenericType.MakeGenericType(typeOfElement);
			object subComparer = Activator.CreateInstance(equalityComparerType);
			areEquals = (Func<T, T, bool>)equalityComparerType.GetMethod(nameof(Equals), new[] { typeOfT, typeOfT }).CreateDelegate(typeof(Func<T, T, bool>), subComparer);
			getHashCode = (Func<T, int>)equalityComparerType.GetMethod(nameof(GetHashCode), new[] { typeOfT }).CreateDelegate(typeof(Func<T, int>), subComparer);
			return;
		}
		else areEquals = (e1, e2) => e1.Equals(e2);

		getHashCode = e => e.GetHashCode();

	}

	public QuickEqualityComparer(Func<T, T, bool> areEquals, Func<T, int> getHashCode = null)
	{
		this.areEquals = areEquals ?? throw new ArgumentNullException(nameof(areEquals));
		this.getHashCode = getHashCode ?? (e => e.GetHashCode());
	}

	public bool Equals([AllowNull] T x, [AllowNull] T y) => areEquals(x, y);

	public int GetHashCode([DisallowNull] T obj) => GetHashCode(obj);
}
