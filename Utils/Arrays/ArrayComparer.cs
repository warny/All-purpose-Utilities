using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace Utils.Arrays
{
	/// <summary>
	/// Compare deux tableau de valeurs de types comparables
	/// </summary>
	/// <typeparam name="T">Type comparable</typeparam>
	public class ArrayComparer<T> : IComparer<IReadOnlyList<T>>
	{
		Func<T, T, int> comparer;
		private readonly Type typeOfT = typeof(T);
		public ArrayComparer(params object[] comparers)
		{
			var externalComparer = comparers.OfType<IComparer<T>>().FirstOrDefault();
			if (externalComparer != null)
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

		public int Compare(IReadOnlyList<T> x, IReadOnlyList<T> y)
		{
			var maxIteration = Math.Min(x.Count, y.Count);
			for (int i = 0; i < maxIteration; i++)
			{
				var comparison = comparer(x[i], y[i]);
				if (comparison != 0) return comparison;
			}
			return x.Count.CompareTo(y.Count);
		}
	}
}
