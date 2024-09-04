using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Objects;

namespace Utils.Arrays
{
	/// <summary>
	/// Compares two arrays (or any IReadOnlyCollection&lt;T&gt;) for equality using custom or default equality logic.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array.</typeparam>
	public class ArrayEqualityComparer<T> : IEqualityComparer<IReadOnlyCollection<T>>
	{
		private readonly Func<T, T, bool> areEquals;
		private readonly Func<T, int> getHashCode;
		private readonly Type typeOfT = typeof(T);

		/// <summary>
		/// Initializes a new instance of the <see cref="ArrayEqualityComparer{T}"/> class using the specified equality comparers.
		/// </summary>
		/// <param name="equalityComparers">Optional custom comparers for comparing elements.</param>
		public ArrayEqualityComparer(params object[] equalityComparers)
		{
			var externalEqualityComparer = equalityComparers.OfType<IEqualityComparer<T>>().FirstOrDefault();
			if (externalEqualityComparer is not null)
			{
				areEquals = externalEqualityComparer.Equals;
				getHashCode = externalEqualityComparer.GetHashCode;
			}
			else if (typeof(IEquatable<T>).IsAssignableFrom(typeOfT))
			{
				areEquals = (e1, e2) => ((IEquatable<T>)e1).Equals(e2);
				getHashCode = e1 => e1.GetHashCode();
			}
			else if (typeof(IComparable<T>).IsAssignableFrom(typeOfT))
			{
				areEquals = (e1, e2) => ((IComparable<T>)e1).CompareTo(e2) == 0;
				getHashCode = e1 => e1.GetHashCode();
			}
			else if (typeOfT.IsArray)
			{
				var typeOfElement = typeOfT.GetElementType();
				Type equalityComparerGenericType = typeof(MultiDimensionalArrayEqualityComparer<>);
				Type equalityComparerType = equalityComparerGenericType.MakeGenericType(typeOfElement);
				object subComparer = Activator.CreateInstance(equalityComparerType);
				areEquals = (Func<T, T, bool>)equalityComparerType.GetMethod(nameof(Equals), new[] { typeOfT, typeOfT }).CreateDelegate(typeof(Func<T, T, bool>), subComparer);
				getHashCode = (Func<T, int>)equalityComparerType.GetMethod(nameof(GetHashCode), new[] { typeOfT }).CreateDelegate(typeof(Func<T, int>), subComparer);
			}
			else
			{
				areEquals = (e1, e2) => e1.Equals(e2);
				getHashCode = e => e.GetHashCode();
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ArrayEqualityComparer{T}"/> class using a specified equality comparer.
		/// </summary>
		/// <param name="equalityComparer">The equality comparer to use for elements.</param>
		public ArrayEqualityComparer(IEqualityComparer<T> equalityComparer)
		{
			areEquals = equalityComparer.Equals;
			getHashCode = equalityComparer.GetHashCode;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ArrayEqualityComparer{T}"/> class using a custom comparison function and hash code function.
		/// </summary>
		/// <param name="areEquals">The function to compare two elements for equality.</param>
		/// <param name="getHashCode">The function to generate a hash code for an element.</param>
		public ArrayEqualityComparer(Func<T, T, bool> areEquals, Func<T, int> getHashCode = null)
		{
			this.areEquals = areEquals ?? throw new ArgumentNullException(nameof(areEquals));
			this.getHashCode = getHashCode ?? (e => e.GetHashCode());
		}

		/// <summary>
		/// Determines whether two arrays are equal by comparing their elements.
		/// </summary>
		/// <param name="x">The first array to compare.</param>
		/// <param name="y">The second array to compare.</param>
		/// <returns><see langword="true"/> if the arrays are equal; otherwise, <see langword="false"/>.</returns>
		public bool Equals(IReadOnlyCollection<T> x, IReadOnlyCollection<T> y)
		{
			if (x is null && y is null) return true;
			if (x is null || y is null) return false;
			if (x.Count != y.Count) return false;

			var enumx = x.GetEnumerator();
			var enumy = y.GetEnumerator();

			while (enumx.MoveNext() && enumy.MoveNext())
			{
				if (!areEquals(enumx.Current, enumy.Current)) return false;
			}
			return true;
		}

		/// <summary>
		/// Returns a hash code for the specified array by combining the hash codes of its elements.
		/// </summary>
		/// <param name="obj">The array for which to generate a hash code.</param>
		/// <returns>A hash code for the array.</returns>
		public int GetHashCode(IReadOnlyCollection<T> obj) => ObjectUtils.ComputeHash(getHashCode, obj);
	}

	/// <summary>
	/// Provides preconfigured array equality comparers for common types.
	/// </summary>
	public static class ArrayEqualityComparers
	{
		public static IEqualityComparer<IReadOnlyCollection<bool>> Boolean { get; } = new ArrayEqualityComparer<bool>();
		public static IEqualityComparer<IReadOnlyCollection<byte>> Byte { get; } = new ArrayEqualityComparer<byte>();
		public static IEqualityComparer<IReadOnlyCollection<sbyte>> SByte { get; } = new ArrayEqualityComparer<sbyte>();
		public static IEqualityComparer<IReadOnlyCollection<short>> Int16 { get; } = new ArrayEqualityComparer<short>();
		public static IEqualityComparer<IReadOnlyCollection<int>> Int32 { get; } = new ArrayEqualityComparer<int>();
		public static IEqualityComparer<IReadOnlyCollection<long>> Int64 { get; } = new ArrayEqualityComparer<long>();
		public static IEqualityComparer<IReadOnlyCollection<ushort>> UInt16 { get; } = new ArrayEqualityComparer<ushort>();
		public static IEqualityComparer<IReadOnlyCollection<uint>> UInt32 { get; } = new ArrayEqualityComparer<uint>();
		public static IEqualityComparer<IReadOnlyCollection<ulong>> UInt64 { get; } = new ArrayEqualityComparer<ulong>();
		public static IEqualityComparer<IReadOnlyCollection<float>> Single { get; } = new ArrayEqualityComparer<float>();
		public static IEqualityComparer<IReadOnlyCollection<double>> Double { get; } = new ArrayEqualityComparer<double>();
		public static IEqualityComparer<IReadOnlyCollection<decimal>> Decimal { get; } = new ArrayEqualityComparer<decimal>();
		public static IEqualityComparer<IReadOnlyCollection<DateTime>> DateTime { get; } = new ArrayEqualityComparer<DateTime>();
		public static IEqualityComparer<IReadOnlyCollection<Guid>> Guid { get; } = new ArrayEqualityComparer<Guid>();
		public static IEqualityComparer<IReadOnlyCollection<Type>> Type { get; } = new ArrayEqualityComparer<Type>();
		public static IEqualityComparer<IReadOnlyCollection<string>> String { get; } = new ArrayEqualityComparer<string>();
	}
}
