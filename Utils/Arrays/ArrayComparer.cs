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
		private readonly Type typeOfT = typeof(T);

		public ArrayEqualityComparer()
		{
			if (typeof(IEquatable<T>).IsAssignableFrom(typeOfT)) areEquals = (e1, e2) => ((IEquatable<T>)e1).Equals(e2);
			else if (typeof(IComparable<T>).IsAssignableFrom(typeOfT)) areEquals = (e1, e2) => ((IComparable<T>)e1).Equals(e2);
			else if (typeOfT.IsArray)
			{
				var typeOfElement = typeOfT.GetElementType();
				Type equalityComparerGenericType = typeof(ArrayEqualityComparer<>);
				Type equalityComparerType = equalityComparerGenericType.MakeGenericType(typeOfElement);
				object subComparer = Activator.CreateInstance(equalityComparerType);
				areEquals = (Func<T, T, bool>)equalityComparerType.GetMethod(nameof(Equals)).CreateDelegate(typeof(Func<T, T, bool>));
				getHashCode = (Func<T, int>)equalityComparerType.GetMethod(nameof(GetHashCode)).CreateDelegate(typeof(Func<T, int>));
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
