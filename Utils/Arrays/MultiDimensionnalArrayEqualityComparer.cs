using System;
using System.Collections.Generic;
using Utils.Objects;

namespace Utils.Arrays
{
	/// <summary>
	/// A comparer for multi-dimensional arrays, allowing comparison of arrays of type <typeparamref name="T"/>.
	/// It compares arrays element by element, ensuring equality across all dimensions.
	/// </summary>
	/// <typeparam name="T">The type of elements in the arrays to compare.</typeparam>
	public class MultiDimensionalArrayEqualityComparer<T> : IEqualityComparer<Array>
	{
		private readonly Func<T, T, bool> areEquals;
		private readonly Func<T, int> getHashCode;
		private readonly Type typeOfT = typeof(T);

		/// <summary>
		/// Initializes a new instance of the <see cref="MultiDimensionalArrayEqualityComparer{T}"/> class.
		/// The equality comparison is based on the type of T.
		/// </summary>
		public MultiDimensionalArrayEqualityComparer()
		{
			if (typeof(IEquatable<T>).IsAssignableFrom(typeOfT))
			{
				areEquals = (e1, e2) => ((IEquatable<T>)e1).Equals(e2);
			}
			else if (typeof(IComparable<T>).IsAssignableFrom(typeOfT))
			{
				areEquals = (e1, e2) => ((IComparable<T>)e1).CompareTo(e2) == 0;
			}
			else if (typeOfT.IsArray)
			{
				// If T is itself an array, create a sub-comparer for array elements
				var typeOfElement = typeOfT.GetElementType();
				Type equalityComparerGenericType = typeof(MultiDimensionalArrayEqualityComparer<>);
				Type equalityComparerType = equalityComparerGenericType.MakeGenericType(typeOfElement);
				object subComparer = Activator.CreateInstance(equalityComparerType);
				areEquals = (Func<T, T, bool>)equalityComparerType.GetMethod(nameof(Equals), [typeOfT, typeOfT]).CreateDelegate(typeof(Func<T, T, bool>), subComparer);
				getHashCode = (Func<T, int>)equalityComparerType.GetMethod(nameof(GetHashCode), [typeOfT]).CreateDelegate(typeof(Func<T, int>), subComparer);
				return;
			}
			else
			{
				areEquals = (e1, e2) => e1.Equals(e2);
			}

			getHashCode = e => e.GetHashCode();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MultiDimensionalArrayEqualityComparer{T}"/> class with a specific equality comparer.
		/// </summary>
		/// <param name="equalityComparer">An equality comparer for elements of type T.</param>
		public MultiDimensionalArrayEqualityComparer(IEqualityComparer<T> equalityComparer)
		{
			areEquals = equalityComparer.Equals;
			getHashCode = equalityComparer.GetHashCode;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MultiDimensionalArrayEqualityComparer{T}"/> class with a specific comparer and hash function.
		/// </summary>
		/// <param name="comparer">A comparer for elements of type T.</param>
		/// <param name="getHashCode">A hash code function for elements of type T.</param>
		public MultiDimensionalArrayEqualityComparer(IComparer<T> comparer, Func<T, int> getHashCode = null)
		{
			areEquals = (e1, e2) => comparer.Compare(e1, e2) == 0;
			this.getHashCode = getHashCode ?? (e => e.GetHashCode());
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MultiDimensionalArrayEqualityComparer{T}"/> class with custom comparison and hash code functions.
		/// </summary>
		/// <param name="areEquals">A function to determine equality between two elements of type T.</param>
		/// <param name="getHashCode">A function to compute the hash code for elements of type T.</param>
		public MultiDimensionalArrayEqualityComparer(Func<T, T, bool> areEquals, Func<T, int> getHashCode = null)
		{
			this.areEquals = areEquals;
			this.getHashCode = getHashCode ?? (e => e.GetHashCode());
		}

		/// <summary>
		/// Determines whether two multi-dimensional arrays are equal.
		/// </summary>
		/// <param name="x">The first array to compare.</param>
		/// <param name="y">The second array to compare.</param>
		/// <returns>True if the arrays are equal; otherwise, false.</returns>
		public bool Equals(Array x, Array y)
		{
			// Ensure the elements in the arrays are of the correct type
			if (!typeOfT.IsAssignableFrom(x.GetType().GetElementType()))
				throw new ArgumentException($"The array x is not of a type compatible with {typeOfT.Name}.", nameof(x));

			if (!typeOfT.IsAssignableFrom(y.GetType().GetElementType()))
				throw new ArgumentException($"The array y is not of a type compatible with {typeOfT.Name}.", nameof(y));

			// Check if both arrays have the same dimensions
			if (x.Rank != y.Rank) return false;

			// Verify if the bounds of each dimension are the same
			for (int r = 0; r < x.Rank; r++)
			{
				if (x.GetLowerBound(r) != y.GetLowerBound(r) || x.GetUpperBound(r) != y.GetUpperBound(r))
					return false;
			}

			// Recursively check element equality
			return AreValuesEquals(0, new int[x.Rank]);

			bool AreValuesEquals(int r, int[] positions)
			{
				if (r == positions.Length)
				{
					return areEquals((T)x.GetValue(positions), (T)y.GetValue(positions));
				}

				for (int i = x.GetLowerBound(r); i <= x.GetUpperBound(r); i++)
				{
					positions[r] = i;
					if (!AreValuesEquals(r + 1, positions)) return false;
				}
				return true;
			}
		}

		/// <summary>
		/// Returns the hash code for a multi-dimensional array.
		/// </summary>
		/// <param name="obj">The array for which to compute the hash code.</param>
		/// <returns>The computed hash code.</returns>
		public int GetHashCode(Array obj)
		{
			return obj.ComputeHash(getHashCode);
		}
	}
}
