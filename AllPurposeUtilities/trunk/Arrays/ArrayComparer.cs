using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Arrays
{
	public class ArrayEqualityComparer<T> : IEqualityComparer<T[]>
	{
		private readonly Func<T, T, bool> areEquals;
		private readonly Func<T, int> getHashCode;

		public ArrayEqualityComparer()
		{
			if (typeof(IEquatable<T>).IsAssignableFrom(typeof(T))) areEquals = (e1, e2) => ((IEquatable<T>)e1).Equals(e2);
			else if (typeof(IComparable<T>).IsAssignableFrom(typeof(T))) areEquals = (e1, e2) => ((IComparable<T>)e1).Equals(e2);
			else areEquals = (e1, e2) => e1.Equals(e2);

			getHashCode = e => e.GetHashCode();
		}

		public ArrayEqualityComparer(IEqualityComparer<T> equalityComparer)
		{
			areEquals = equalityComparer.Equals;
			getHashCode = equalityComparer.GetHashCode;
		}

		public ArrayEqualityComparer(IComparer<T> equalityComparer, Func<T, int> getHashCode = null)
		{
			areEquals = (e1, e2) => equalityComparer.Compare(e1, e2)==0;
			getHashCode = getHashCode ?? (e => e.GetHashCode());
		}


		public bool Equals(T[] x, T[] y)
		{
			if (x == null && y == null) return true;
			if (x == null || y == null) return false;
			if (x.Length!=y.Length) return false;

			for (int i = 0; i < x.Length; i++) {
				if (!areEquals(x[i], y[i])) return false;
			}
			return true;
		}

		public int GetHashCode(T[] obj) => ObjectUtils.ComputeHash(getHashCode, obj);
	}
}
