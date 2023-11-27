using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Utils.Mathematics
{
    [DebuggerDisplay("{FloatingPointComparer}(±{Interval})")]
    public class FloatingPointComparer<T> : IComparer<T>, IEqualityComparer<T>
        where
            T : struct, IComparable<T>, IEquatable<T>, IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>
    {
		public T Interval { get; }

		public FloatingPointComparer(int precision) : this((T)(object)Math.Pow(10d, -(double)precision)) { }

        public FloatingPointComparer(T interval)
        {
            Interval = interval;
        }

        public int Compare(T x, T y) => x.Equals(y) ? 0 : x.CompareTo(y);
        public bool Equals(T x, T y) => x.Between(y - Interval, y + Interval);
        public int GetHashCode([DisallowNull] T obj) => obj.GetHashCode();
    }
}
