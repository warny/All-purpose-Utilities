using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Mathematics.InternationalSystem
{
	public class PhysicalValue : 
        IEquatable<PhysicalValue>,
		IComparable<PhysicalValue>,
		IAdditionOperators<PhysicalValue, PhysicalValue, PhysicalValue>,
        ISubtractionOperators<PhysicalValue, PhysicalValue, PhysicalValue>,
		IMultiplyOperators<PhysicalValue, PhysicalValue, PhysicalValue>,
        IMultiplyOperators<PhysicalValue, double, PhysicalValue>,
        IDivisionOperators<PhysicalValue, PhysicalValue, PhysicalValue>,
        IDivisionOperators<PhysicalValue, double, PhysicalValue>,
        IUnaryNegationOperators<PhysicalValue, PhysicalValue>,
		IUnaryPlusOperators<PhysicalValue, PhysicalValue>,
		IEqualityOperators<PhysicalValue, PhysicalValue, bool>,
		IComparisonOperators<PhysicalValue, PhysicalValue, bool>

    {
        public PhysicalValue(double value, int l = 0, int m = 0, int t = 0, int i = 0, int theta = 0, int n = 0, int j = 0, int rad = 0, int sterad = 0)
		{
			this.Scalar = value;
			this.L = l;
			this.M = m;
			this.T = t;
			this.I = i;
			this.Theta = theta;
			this.N = n;
			this.J = j;
			this.Rad = rad;
			this.Sterad = sterad;
		}

		public static PhysicalValue Length(double value) => new (value, l: 1);
		public static PhysicalValue Surface(double value) => new (value, l: 2);
		public static PhysicalValue Volume(double value) => new (value, l: 3);
		public static PhysicalValue Speed(double value) => new (value, l: 1, t: -1);
		public static PhysicalValue Acceleration(double value) => new (value, l: 1, t: -2);
		public static PhysicalValue Mass(double value) => new (value, m: 1);
		public static PhysicalValue Time(double value) => new (value, t: 1);
		public static PhysicalValue Intensity(double value) => new (value, i: 1);
		public static PhysicalValue Temperature(double value) => new (value, theta: 1);
		public static PhysicalValue SubstanceAmount(double value) => new (value, n: 1);
		public static PhysicalValue LightIntensity(double value) => new (value, j: 1);
		public static PhysicalValue Angle(double value) => new (value, rad: 1);
		public static PhysicalValue SolidAngle(double value) => new (value, sterad: 1);

        /// <summary>
        /// Scalar value
        /// </summary>
        public double Scalar { get; }

        /// <summary>
        /// Length in meters
        /// </summary>
        public int L { get; }

        /// <summary>
        /// Mass in kilograms
        /// </summary>
        public int M { get; }

        /// <summary>
        /// Time in seconds
        /// </summary>
        public int T { get; }

        /// <summary>
        /// Electric current intensity in amperes
        /// </summary>
        public int I { get; }

        /// <summary>
        /// Temperature in Kelvin
        /// </summary>
        public int Theta { get; }

        /// <summary>
        /// Amount of substance in moles
        /// </summary>
        public int N { get; }

        /// <summary>
        /// Luminous intensity in candela
        /// </summary>
        public int J { get; }

        /// <summary>
        /// Plane angle
        /// </summary>
        public int Rad { get; }

        /// <summary>
        /// Solid angle
        /// </summary>
        public int Sterad { get; }

        public string GetUnitString() {
			var result = new StringBuilder();
			if (L != 0) result.Append($"·m^{L}");
			if (M != 0) result.Append($"·kg^{M}");
			if (T != 0) result.Append($"·s^{T}");
			if (I != 0) result.Append($"·A^{I}");
			if (Theta != 0) result.Append($"·m^{L}");
			if (N != 0) result.Append($"·mol^{L}");
			if (J != 0) result.Append($"·cd^{L}");
			if (Rad != 0) result.Append($"·rad^{L}");
			if (Sterad != 0) result.Append($"·sterad^{L}");

			result.Replace("^1", "");
			if (result.Length > 0) result.Remove(0, 1);
			return result.ToString();
		}

		public static PhysicalValue operator +(PhysicalValue left, PhysicalValue right) {
			IncompatibleUnitsException.TestCompatibility(left, right);
			return new PhysicalValue(left.Scalar + right.Scalar, left.L, left.M, left.T, left.I, left.Theta, left.N, left.J, left.Rad, left.Sterad);
		}

		public static PhysicalValue operator -(PhysicalValue left, PhysicalValue right)
		{
			IncompatibleUnitsException.TestCompatibility(left, right);
			return new PhysicalValue(left.Scalar + right.Scalar, left.L, left.M, left.T, left.I, left.Theta, left.N, left.J, left.Rad, left.Sterad);
		}

		public static PhysicalValue operator *(double left, PhysicalValue right)
		{
			return new PhysicalValue(
				left * right.Scalar,
				right.L,
				right.M,
				right.T,
				right.I,
				right.Theta,
				right.N,
				right.J,
				right.Rad,
				right.Sterad);
		}

		public static PhysicalValue operator *(PhysicalValue left, double right)
		{
			return new PhysicalValue(
				right * left.Scalar,
				left.L,
				left.M,
				left.T,
				left.I,
				left.Theta,
				left.N,
				left.J,
				left.Rad,
				left.Sterad);
		}

		public static PhysicalValue operator *(PhysicalValue left, PhysicalValue right)
		{
			return new PhysicalValue(
				left.Scalar * right.Scalar,
				left.L + right.L,
				left.M + right.M,
				left.T + right.T,
				left.I + right.I,
				left.Theta + right.Theta,
				left.N + right.N,
				left.J + right.J,
				left.Rad + right.Rad,
				left.Sterad + right.Sterad);
		}

		public static PhysicalValue operator /(PhysicalValue left, PhysicalValue right)
		{
			return new PhysicalValue(
				left.Scalar / right.Scalar,
				left.L - right.L,
				left.M - right.M,
				left.T - right.T,
				left.I - right.I,
				left.Theta - right.Theta,
				left.N - right.N,
				left.J - right.J,
				left.Rad - right.Rad,
				left.Sterad - right.Sterad);
		}

		public static PhysicalValue operator /(double left, PhysicalValue right)
		{
			return new PhysicalValue(
				left / right.Scalar,
				-right.L,
				-right.M,
				-right.T,
				-right.I,
				-right.Theta,
				-right.N,
				-right.J,
				-right.Rad,
				-right.Sterad);
		}

		public static PhysicalValue operator /(PhysicalValue left, double right)
		{
			return new PhysicalValue(
				left.Scalar / right,
				left.L,
				left.M,
				left.T,
				left.I,
				left.Theta,
				left.N,
				left.J,
				left.Rad,
				left.Sterad);
		}

        public static PhysicalValue operator -(PhysicalValue value)
        {
            return new PhysicalValue(
				-value.Scalar,
                value.L,
                value.M,
                value.T,
                value.I,
                value.Theta,
                value.N,
                value.J,
                value.Rad,
                value.Sterad);
        }

        public static PhysicalValue operator +(PhysicalValue value)
        {
            return new PhysicalValue(
                value.Scalar,
                value.L,
                value.M,
                value.T,
                value.I,
                value.Theta,
                value.N,
                value.J,
                value.Rad,
                value.Sterad);
        }

        public static bool operator ==(PhysicalValue left, PhysicalValue right)	=> left?.Equals(right) ?? right is null;

        public static bool operator !=(PhysicalValue left, PhysicalValue right) => !(left?.Equals(right) ?? right is null);

		public static bool operator >(PhysicalValue left, PhysicalValue right) => left?.CompareTo(right) > 0;

        public static bool operator >=(PhysicalValue left, PhysicalValue right) => left?.CompareTo(right) >= 0;

        public static bool operator <(PhysicalValue left, PhysicalValue right) => left?.CompareTo(right) < 0;

        public static bool operator <=(PhysicalValue left, PhysicalValue right) => left?.CompareTo(right) <= 0;

        public PhysicalValue Pow(int i)
		{
			return new PhysicalValue(
					Math.Pow(Scalar, i),
					L * i,
					M * i,
					T * i,
					I * i,
					Theta * i,
					N * i,
					J * i,
					Rad * i,
					Sterad * i
				);
		}

        public override bool Equals(object obj) => obj is PhysicalValue v && Equals(v);

        public bool Equals(PhysicalValue other)
            => other is not null
            && this.Scalar == other.Scalar
            && this.L == other.L
            && this.M == other.M
            && this.T == other.T
            && this.I == other.I
            && this.Theta == other.Theta
            && this.N == other.N
            && this.J == other.J
            && this.Rad == other.Rad
            && this.Sterad == other.Sterad;


        public int GetHashCode([DisallowNull] PhysicalValue obj)
			=> ObjectUtils.ComputeHash(Scalar, L, M, T, I, Theta, N, J, Rad, Sterad);

        public int CompareTo(PhysicalValue other)
        {
            IncompatibleUnitsException.TestCompatibility(this, other);
			return this.Scalar.CompareTo(other.Scalar);
        }
    }
}
