using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Utils.Objects;

namespace Utils.Mathematics.LinearAlgebra
{
	/// <summary>
	/// Point
	/// <remarks>
	/// Un point de dimension n est multipliable par une matrice d'espace normal de dimension n+1
	/// </remarks>
	/// </summary>
	public sealed partial class Point: IEquatable<Point>, IEquatable<double[]>
	{
		/// <summary>
		/// composantes du vecteur
		/// </summary>
		internal readonly double[] components;

		private Point ()
		{
		}

		/// <summary>
		/// constructeur par dimensions
		/// </summary>
		/// <param name="dimensions"></param>
		public Point(int dimensions) {
			this.components = new double[dimensions + 1];
			this.components[this.components.Length - 1] = 1;
		}

		/// <summary>
		/// constructeur par valeurs
		/// </summary>
		/// <param name="components"></param>
		public Point ( params double[] components )
		{
			components.ArgMustNotBeNull();

			this.components = new double[components.Length + 1];
			Array.Copy(components, this.components,components.Length);
			this.components[this.components.Length - 1] = 1;
		}

		/// <summary>
		/// Constructeur de copie
		/// </summary>
		/// <param name="point"></param>
		public Point ( Point point )
		{
			point.ArgMustNotBeNull();
			this.components = new Double[point.components.Length];
			Array.Copy(point.components, this.components, point.components.Length); 
		}

		/// <summary>
		/// Retourne ou définie la valeur à la dimension indiquée
		/// </summary>
		/// <param name="dimension"></param>
		/// <returns></returns>
		public double this[int dimension]
		{
			get { return this.components[dimension]; }
			set
			{
				this.components[dimension] = value;
			}
		}

		/// <summary>
		/// dimension du vecteur
		/// </summary>
		public int Dimension
		{
			get { return this.components.Length - 1; }
		}

		public override bool Equals ( object obj )
		{
			if (obj is Point p) return Equals(p);
			if (obj is double[] array) return Equals(array);
			return false;
		}

		public bool Equals ( Point other )
		{
			if (other == null) return false;
			if (Object.ReferenceEquals(this, other)) return true;
			Arrays.ArrayEqualityComparers.Double.Equals(this.components, other.components);
			return true;
		}
		public bool Equals(double[] other)
		{
			if (other == null) return false;
			return Arrays.ArrayEqualityComparers.Double.Equals(this.components, other);
		}

		public override int GetHashCode ()
		{
			unchecked {
				var temp = this.components.Length.GetHashCode();
				foreach (var el in this.components) {
					temp = ((temp * 23) << 1) + el.GetHashCode();
				}
				return temp;
			}
		}

		public static double Distance ( Point point1, Point point2 )
		{
			if (point1.components.Length != point2.components.Length) {
				throw new ArgumentException("Les deux points n'ont pas la même dimension");
			}
			double temp = 0D;
			for (int i = 0; i < point1.components.Length - 1; i++ ) {
				temp += Math.Pow(point1.components[i] - point2.components[i], 2D);
			}

			return Math.Sqrt(temp);
		}

		public override string ToString ()
		{
			return string.Format("({0})", string.Join(";", this.components.Select(c => c.ToString()).ToArray(), 0, this.Dimension));
		}

		public static Point IntermediatePoint(Point point1, Point point2, double position)
		{
			if (point1.Dimension != point2.Dimension) { throw new ArgumentException("Les dimensions de point1 et point2 sont incompatibles", nameof(point2)); }

			var result = new double[point1.Dimension];
			for (int i = 0; i < result.Length; i++) {
				result[i] = (1 - position) * point1[i] + position * point2[i];
			}
			return new Point(result);
		}
	}
}
