using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Objects;

namespace Utils.List
{
	/// <summary>
	/// A comparer for enumerables that allows comparison based on custom equality logic.
	/// </summary>
	/// <typeparam name="T">The type of elements in the enumerable.</typeparam>
	public class EnumerableEqualityComparer<T> : IEqualityComparer<IEnumerable<T>>
	{
		private readonly Func<T, T, bool> areEquals;
		private readonly Func<T, int> getHashCode;
		private readonly Type typeOfT = typeof(T);

		/// <summary>
		/// Initializes a new instance of the <see cref="EnumerableEqualityComparer{T}"/> class.
		/// The constructor attempts to resolve equality logic using the provided equality comparers,
		/// or by using default implementations based on the type T.
		/// </summary>
		/// <param name="equalityComparers">Optional custom equality comparers.</param>
		public EnumerableEqualityComparer(params object[] equalityComparers)
		{
			var externalEqualityComparer = equalityComparers.OfType<IEqualityComparer<T>>().FirstOrDefault();
			if (externalEqualityComparer is not null)
			{
				// Use the external IEqualityComparer provided
				areEquals = (e1, e2) => externalEqualityComparer.Equals(e1, e2);
				getHashCode = e => externalEqualityComparer.GetHashCode(e);
				return;
			}
			else if (typeof(IEquatable<T>).IsAssignableFrom(typeOfT))
			{
				// Use IEquatable<T> if T implements it
				areEquals = (e1, e2) => ((IEquatable<T>)e1).Equals(e2);
			}
			else if (typeof(IComparable<T>).IsAssignableFrom(typeOfT))
			{
				// Use IComparable<T> if T implements it
				areEquals = (e1, e2) => ((IComparable<T>)e1).CompareTo(e2) == 0;
			}
			else if (typeOfT.IsArray)
			{
				// Special case for arrays: use a nested EnumerableEqualityComparer for the element type
				var typeOfElement = typeOfT.GetElementType();
				Type equalityComparerGenericType = typeof(EnumerableEqualityComparer<>);
				Type equalityComparerType = equalityComparerGenericType.MakeGenericType(typeOfElement);
				object subComparer = Activator.CreateInstance(equalityComparerType);
				areEquals = (Func<T, T, bool>)equalityComparerType
					.GetMethod(nameof(Equals), [typeOfT, typeOfT])
					.CreateDelegate(typeof(Func<T, T, bool>), subComparer);
				getHashCode = (Func<T, int>)equalityComparerType
					.GetMethod(nameof(GetHashCode), [typeOfT])
					.CreateDelegate(typeof(Func<T, int>), subComparer);
				return;
			}
			else
			{
				// Default case: use object.Equals
				areEquals = (e1, e2) => e1.Equals(e2);
			}

			// Default hash code logic
			getHashCode = e => e.GetHashCode();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="EnumerableEqualityComparer{T}"/> class 
		/// using a provided IEqualityComparer.
		/// </summary>
		/// <param name="equalityComparer">An equality comparer for elements of type T.</param>
		public EnumerableEqualityComparer(IEqualityComparer<T> equalityComparer)
		{
			areEquals = equalityComparer.Equals;
			getHashCode = equalityComparer.GetHashCode;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="EnumerableEqualityComparer{T}"/> class 
		/// using a provided IComparer and optional hash code function.
		/// </summary>
		/// <param name="equalityComparer">A comparer for elements of type T.</param>
		/// <param name="getHashCode">A function to compute the hash code for elements, defaults to object.GetHashCode.</param>
		public EnumerableEqualityComparer(IComparer<T> equalityComparer, Func<T, int> getHashCode = null)
		{
			areEquals = (e1, e2) => equalityComparer.Compare(e1, e2) == 0;
			this.getHashCode = getHashCode ?? (e => e.GetHashCode());
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="EnumerableEqualityComparer{T}"/> class 
		/// using provided equality and hash code functions.
		/// </summary>
		/// <param name="areEquals">A function to determine if two elements of type T are equal.</param>
		/// <param name="getHashCode">A function to compute the hash code for elements, defaults to object.GetHashCode.</param>
		public EnumerableEqualityComparer(Func<T, T, bool> areEquals, Func<T, int> getHashCode = null)
		{
			this.areEquals = areEquals ?? throw new ArgumentNullException(nameof(areEquals));
			this.getHashCode = getHashCode ?? (e => e.GetHashCode());
		}

		/// <summary>
		/// Determines whether two enumerable sequences are equal.
		/// </summary>
		/// <param name="x">The first enumerable sequence to compare.</param>
		/// <param name="y">The second enumerable sequence to compare.</param>
		/// <returns>True if the sequences are equal; otherwise, false.</returns>
		public bool Equals(IEnumerable<T> x, IEnumerable<T> y)
		{
			if (x is null && y is null) return true;
			if (x is null || y is null) return false;

			using var enumx = x.GetEnumerator();
			using var enumy = y.GetEnumerator();

			while (true)
			{
				bool readx = enumx.MoveNext();
				bool ready = enumy.MoveNext();
				if (!readx && !ready) return true; // Both reached the end
				if (!readx || !ready) return false; // One reached the end before the other
				if (!areEquals(enumx.Current, enumy.Current)) return false; // Elements differ
			}
		}

		/// <summary>
		/// Returns a hash code for the specified enumerable sequence.
		/// </summary>
		/// <param name="obj">The enumerable sequence for which to get a hash code.</param>
		/// <returns>A hash code for the specified sequence.</returns>
		public int GetHashCode(IEnumerable<T> obj)
		{
			if (obj == null) throw new ArgumentNullException(nameof(obj));
			return ObjectUtils.ComputeHash(obj, getHashCode);
		}
	}
}
