using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Utils.Arrays;

/// <summary>
/// Compare deux tableau de valeurs de types comparables
/// </summary>
/// <typeparam name="T">Type comparable</typeparam>
public class ArrayComparer<T> : IComparer<IReadOnlyCollection<T>>
{
	Func<T, T, int> comparer;
	private readonly Type typeOfT = typeof(T);
	public ArrayComparer(params object[] comparers)
	{
		var externalComparer = comparers.OfType<IComparer<T>>().FirstOrDefault();
		if (externalComparer is not null)
		{
			comparer = (e1, e2) => externalComparer.Compare(e1, e2);
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
			object subComparer = Activator.CreateInstance(arrayComparerType, new object[] { comparers });
			comparer = (Func<T, T, int>)arrayComparerType.GetMethod(nameof(Compare), new[] { typeOfT, typeOfT }).CreateDelegate(typeof(Func<T, T, int>), subComparer);
			return;
		}
		else if (typeof(IComparable).IsAssignableFrom(typeOfT))
		{
			comparer = (e1, e2) => ((IComparable)e1).CompareTo(e2);
		}
		else
		{
			throw new NotSupportedException($"The type {typeof(T).Name} doesn't spport comparison");
		}
	}

	public int Compare(IReadOnlyCollection<T> x, IReadOnlyCollection<T> y)
	{
		if (x is null && y is null) return 0;
		if (x is null) return 1;
		if (y is null) return -1;

		var enumx = x.GetEnumerator();
		var enumy = y.GetEnumerator();

		while (true)
		{
			bool readx = enumx.MoveNext();
			bool ready = enumy.MoveNext();
			if (!readx && !ready) return 0;
			if (!readx) return 1;
			if (!ready) return -1;
			var comparison = comparer(enumx.Current, enumy.Current);
			if (comparison != 0) return comparison;
		}
	}
}
