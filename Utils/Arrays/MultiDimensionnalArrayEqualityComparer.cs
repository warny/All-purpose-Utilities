using System;
using System.Collections.Generic;
using Utils.Objects;

namespace Utils.Arrays
{
	public class MultiDimensionnalArrayEqualityComparer<T> : IEqualityComparer<Array>
	{
		private readonly Func<T, T, bool> areEquals;
		private readonly Func<T, int> getHashCode;
		private readonly Type typeOfT = typeof(T);

		public MultiDimensionnalArrayEqualityComparer()
		{
			if (typeof(IEquatable<T>).IsAssignableFrom(typeOfT)) areEquals = (e1, e2) => ((IEquatable<T>)e1).Equals(e2);
			else if (typeof(IComparable<T>).IsAssignableFrom(typeOfT)) areEquals = (e1, e2) => ((IComparable<T>)e1).Equals(e2);
			else if (typeOfT.IsArray)
			{
				var typeOfElement = typeOfT.GetElementType();
				Type equalityComparerGenericType = typeof(MultiDimensionnalArrayEqualityComparer<>);
				Type equalityComparerType = equalityComparerGenericType.MakeGenericType(typeOfElement);
				object subComparer = Activator.CreateInstance(equalityComparerType);
				areEquals = (Func<T, T, bool>)equalityComparerType.GetMethod(nameof(Equals), [typeOfT, typeOfT]).CreateDelegate(typeof(Func<T, T, bool>), subComparer);
				getHashCode = (Func<T, int>)equalityComparerType.GetMethod(nameof(GetHashCode), [typeOfT]).CreateDelegate(typeof(Func<T, int>), subComparer);
				return;
			}
			else areEquals = (e1, e2) => e1.Equals(e2);

			getHashCode = e => e.GetHashCode();
		}

		public MultiDimensionnalArrayEqualityComparer(IEqualityComparer<T> equalityComparer)
		{
			this.areEquals = equalityComparer.Equals;
			this.getHashCode = equalityComparer.GetHashCode;
		}

		public MultiDimensionnalArrayEqualityComparer(IComparer<T> equalityComparer, Func<T, int> getHashCode = null)
		{
			this.areEquals = (e1, e2) => equalityComparer.Compare(e1, e2) == 0;
			this.getHashCode = getHashCode ?? (e => e.GetHashCode());
		}

		public MultiDimensionnalArrayEqualityComparer(Func<T, T, bool> areEquals, Func<T, int> getHashCode = null)
		{
			this.areEquals = areEquals;
			this.getHashCode = getHashCode ?? (e => e.GetHashCode());
		}

		public bool Equals(Array x, Array y)
		{
			//Teste si les éléments du tableau sont du bon type
			if (!typeOfT.IsAssignableFrom(x.GetType().GetElementType())) throw new ArgumentException($"Le tableau x n'est pas d'un type compatible avec {typeOfT.Name} ", nameof(x));
			if (!typeOfT.IsAssignableFrom(y.GetType().GetElementType())) throw new ArgumentException($"Le tableau y n'est pas d'un type compatible avec {typeOfT.Name} ", nameof(y));
			//teste si les tableaux ont le même nombre de dimensions
			if (x.Rank != y.Rank) return false;
			//teste si les dimensions du tableau sont identiques
			for (int r = 0; r < x.Rank; r++)
			{
				if (x.GetLowerBound(r) != y.GetLowerBound(r) || x.GetUpperBound(r) != y.GetUpperBound(r)) return false;
			}
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

		public int GetHashCode(Array obj) => obj.ComputeHash(getHashCode);

	}
}
