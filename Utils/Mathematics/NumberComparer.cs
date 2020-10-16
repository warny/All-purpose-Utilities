using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Utils.Mathematics
{
	public class DoubleComparer : IComparer<double>, IEqualityComparer<double>
	{
		public double Interval {get;}

		public DoubleComparer(int precision) : this (Math.Pow(10, -precision)) {}

		public DoubleComparer(double interval)
		{
			this.Interval = interval;
		}

		public int Compare(double x, double y)	
			=> x.Equals(y) ? 0 : x.CompareTo(y);

		public bool Equals(double x, double y) => x.Between(y - Interval, y + Interval);

		public int GetHashCode(double obj) => obj.GetHashCode();
	}

	public class FloatComparer : IComparer<float>, IEqualityComparer<float>
	{
		public float Interval { get; }

		public FloatComparer(int precision) : this((float)Math.Pow(10, -precision)) { }

		public FloatComparer(float interval)
		{
			this.Interval = interval;
		}

		public int Compare(float x, float y)
			=> x.Between(x - Interval, x + Interval) ? 0 : x.CompareTo(y);

		public bool Equals(float x, float y) => x.Between(x - Interval, x + Interval);

		public int GetHashCode(float obj) => obj.GetHashCode();
	}

	public class DecimalComparer : IComparer<decimal>, IEqualityComparer<decimal>
	{
		public decimal Interval { get; }

		public DecimalComparer(int precision) : this((decimal)Math.Pow(10, -precision)) { }

		public DecimalComparer(decimal interval)
		{
			this.Interval = interval;
		}

		public int Compare(decimal x, decimal y)
			=> x.Between(x - Interval, x + Interval) ? 0 : x.CompareTo(y);

		public bool Equals(decimal x, decimal y) => x.Between(x - Interval, x + Interval);

		public int GetHashCode(decimal obj) => obj.GetHashCode();
	}
}
