using System;
using System.Linq;
using System.Collections.Generic;

namespace Utils.List
{
	/// <summary>
	/// Compares two sequences of values of comparable types.
	/// </summary>
	/// <typeparam name="T">The type of elements in the sequences. Must be comparable.</typeparam>
	public class EnumerableComparer<T> : IComparer<IEnumerable<T>>
	{
		// The delegate used to compare two elements of type T.
		private readonly Func<T, T, int> comparer;
		private readonly Type typeOfT = typeof(T);

		/// <summary>
		/// Initializes a new instance of the <see cref="EnumerableComparer{T}"/> class.
		/// The constructor attempts to resolve comparison logic using provided comparers or
		/// by using default implementations based on the type T.
		/// </summary>
		/// <param name="comparers">Optional custom comparers for elements of type T.</param>
		public EnumerableComparer(params object[] comparers)
		{
			var externalComparer = comparers.OfType<IComparer<T>>().FirstOrDefault();
			if (externalComparer is not null)
			{
				// Use the external IComparer provided
				comparer = (e1, e2) => externalComparer.Compare(e1, e2);
			}
			else if (typeof(IComparable<T>).IsAssignableFrom(typeOfT))
			{
				// Use IComparable<T> if T implements it
				comparer = (e1, e2) => ((IComparable<T>)e1).CompareTo(e2);
			}
			else if (typeOfT.IsArray)
			{
				// Special case for arrays: use a nested EnumerableComparer for the element type
				var typeOfElement = typeOfT.GetElementType();
				Type arrayComparerGenericType = typeof(EnumerableComparer<>);
				Type arrayComparerType = arrayComparerGenericType.MakeGenericType(typeOfElement);
				object subComparer = Activator.CreateInstance(arrayComparerType, comparers);
				comparer = (Func<T, T, int>)arrayComparerType
					.GetMethod(nameof(Compare), new[] { typeOfT, typeOfT })
					.CreateDelegate(typeof(Func<T, T, int>), subComparer);
				return;
			}
			else if (typeof(IComparable).IsAssignableFrom(typeOfT))
			{
				// Use IComparable if T implements it
				comparer = (e1, e2) => ((IComparable)e1).CompareTo(e2);
			}
			else
			{
				// Throw an exception if T cannot be compared
				throw new NotSupportedException($"The type {typeof(T).Name} doesn't support comparison.");
			}
		}

		/// <summary>
		/// Compares two sequences of type T.
		/// </summary>
		/// <param name="x">The first sequence to compare.</param>
		/// <param name="y">The second sequence to compare.</param>
		/// <returns>
		/// Less than zero if x is less than y; greater than zero if x is greater than y; zero if x equals y.
		/// </returns>
		public int Compare(IEnumerable<T> x, IEnumerable<T> y)
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
				if (!readx && !ready) return 0; // Both enumerations are finished
				if (!readx) return -1; // x is shorter than y
				if (!ready) return 1; // x is longer than y
				var comparison = comparer(enumx.Current, enumy.Current);
				if (comparison != 0) return comparison; // Return the first non-zero comparison
			}
		}
	}
}
