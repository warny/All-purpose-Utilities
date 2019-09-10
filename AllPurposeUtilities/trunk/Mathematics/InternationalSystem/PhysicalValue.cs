using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics.InternationalSystem
{
	public class PhysicalValue
	{
		public PhysicalValue(double value, int l = 0, int m = 0, int t = 0, int i = 0, int theta = 0, int n = 0, int j = 0, int rad = 0, int sterad = 0)
		{
			this.Value = value;
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

		public static PhysicalValue Length(double value) => new PhysicalValue(value, l: 1);
		public static PhysicalValue Surface(double value) => new PhysicalValue(value, l: 2);
		public static PhysicalValue Volume(double value) => new PhysicalValue(value, l: 3);
		public static PhysicalValue Speed(double value) => new PhysicalValue(value, l: 1, t: -1);
		public static PhysicalValue Acceleration(double value) => new PhysicalValue(value, l: 1, t: -2);
		public static PhysicalValue Mass(double value) => new PhysicalValue(value, m: 1);
		public static PhysicalValue Time(double value) => new PhysicalValue(value, t: 1);
		public static PhysicalValue Intensity(double value) => new PhysicalValue(value, i: 1);
		public static PhysicalValue Temperature(double value) => new PhysicalValue(value, theta: 1);
		public static PhysicalValue SubstanceAmount(double value) => new PhysicalValue(value, n: 1);
		public static PhysicalValue LightIntensity(double value) => new PhysicalValue(value, j: 1);
		public static PhysicalValue Angle(double value) => new PhysicalValue(value, rad: 1);
		public static PhysicalValue SolidAngle(double value) => new PhysicalValue(value, sterad: 1);

		/// <summary>
		/// Valeur
		/// </summary>
		public double Value { get; }
		/// <summary>
		/// Longueur en m
		/// </summary>
		public int L { get; }
		/// <summary>
		/// Masse en kg
		/// </summary>
		public int M { get; }
		/// <summary>
		/// Temps en s
		/// </summary>
		public int T { get; }
		/// <summary>
		/// Intensité electrique en A
		/// </summary>
		public int I { get; }
		/// <summary>
		/// Température en K
		/// </summary>
		public int Theta { get; }
		/// <summary>
		/// Quantité de matière en mol
		/// </summary>
		public int N { get; }
		/// <summary>
		/// Intensité lumineuse en cd
		/// </summary>
		public int J { get; }
		/// <summary>
		/// Angle plat
		/// </summary>
		public int Rad { get; }
		/// <summary>
		/// Angle solide
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
			return new PhysicalValue(left.Value + right.Value, left.L, left.M, left.T, left.I, left.Theta, left.N, left.J, left.Rad, left.Sterad);
		}

		public static PhysicalValue operator -(PhysicalValue left, PhysicalValue right)
		{
			IncompatibleUnitsException.TestCompatibility(left, right);
			return new PhysicalValue(left.Value + right.Value, left.L, left.M, left.T, left.I, left.Theta, left.N, left.J, left.Rad, left.Sterad);
		}

		public static PhysicalValue operator *(double left, PhysicalValue right)
		{
			return new PhysicalValue(
				left * right.Value,
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
				right * left.Value,
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
				left.Value * right.Value,
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
				left.Value / right.Value,
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
				left / right.Value,
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
				left.Value / right,
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

		public PhysicalValue Pow(int i)
		{
			return new PhysicalValue(
					Math.Pow(Value, i),
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
	}
}
