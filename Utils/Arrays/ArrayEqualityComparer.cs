﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Utils.Objects;

namespace Utils.Arrays
{
	/// <summary>
	/// Classe de comparaison de tableaux
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ArrayEqualityComparer<T> : IEqualityComparer<IReadOnlyList<T>>
	{
		private readonly Func<T, T, bool> areEquals;
		private readonly Func<T, int> getHashCode;
		private readonly Type typeOfT = typeof(T);

		public ArrayEqualityComparer(params object[] equalityComparers)
		{
			var externalEqualityComparer = equalityComparers.OfType<IEqualityComparer<T>>().FirstOrDefault();
			if (externalEqualityComparer is not null)
			{
				areEquals = (e1, e2) => externalEqualityComparer.Equals(e1, e2);
				getHashCode = e => externalEqualityComparer.GetHashCode(e);
				return;
			}
			else if (typeof(IEquatable<T>).IsAssignableFrom(typeOfT))
			{
				areEquals = (e1, e2) => ((IEquatable<T>)e1).Equals(e2);
			}
			else if (typeof(IComparable<T>).IsAssignableFrom(typeOfT))
			{
				areEquals = (e1, e2) => ((IComparable<T>)e1).CompareTo(e2) == 0;
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

		public ArrayEqualityComparer(IEqualityComparer<T> equalityComparer)
		{
			this.areEquals = equalityComparer.Equals;
			this.getHashCode = equalityComparer.GetHashCode;
		}

		public ArrayEqualityComparer(IComparer<T> equalityComparer, Func<T, int> getHashCode = null)
		{
			this.areEquals = (e1, e2) => equalityComparer.Compare(e1, e2)==0;
			this.getHashCode = getHashCode ?? (e => e.GetHashCode());
		}

		public ArrayEqualityComparer(Func<T, T, bool> areEquals, Func<T, int> getHashCode = null)
		{
			this.areEquals = areEquals;
			this.getHashCode = getHashCode ?? (e => e.GetHashCode());
		}

		public bool Equals(IReadOnlyList<T> x, IReadOnlyList<T> y)
		{
			if (x is null && y is null) return true;
			if (x is null || y is null) return false;
			if (x.Count!=y.Count) return false;

			var enumx = x.GetEnumerator();
			var enumy = y.GetEnumerator();

			while (true)
			{
				bool readx = enumx.MoveNext();
				bool ready = enumy.MoveNext();
				if (!readx && !ready) return true;
				if (!readx || !ready) return false;
				if (!areEquals(enumx.Current, enumy.Current)) return false;
			}
		}

		public int GetHashCode(IReadOnlyList<T> obj) => ObjectUtils.ComputeHash(getHashCode, obj);
	}
}
