using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Arrays;
using Utils.Objects;

namespace Utils.Mathematics.LinearAlgebra
{
	/// <summary>
	/// Matrice
	/// </summary>
	public sealed partial class Matrix : IFormattable , IEquatable<Matrix>, IEquatable<double[,]>, IEquatable<double[][]>, IEquatable<Vector[]>
	{
		internal readonly double[,] components;
		private bool? isDiagonalized;
		private bool? isTriangularised;
		private bool? isIdentity;
		private double? determinant;
		private int? hashCode;

		private Matrix() { }

		public Matrix ( int dimensionX, int dimensionY )
		{
			components = new double[dimensionX, dimensionY];
		}

		private Matrix(double[,] array!!, bool isIdentity, bool isDiagonalized, bool isTriangularised, double? determinant)
		{
			this.components = array;
			this.isDiagonalized = isDiagonalized;
			this.isTriangularised = isTriangularised;
			this.isIdentity = isIdentity;
			this.determinant = determinant;
		}

		public Matrix ( double[,] array!!)
		{
			components = new double[array.GetLength(0), array.GetLength(1)];
			Array.Copy(array, this.components, array.Length);
			var isSquare = IsSquare;
			isDiagonalized = isSquare ? false : (bool?)null;
			isTriangularised = isSquare ? false : (bool?)null; 
			isIdentity = isSquare ? false : (bool?)null;
			determinant = null;
		}

		public Matrix ( double[][] array!!)
		{
			int maxYLength = array.Select(a => a.Length).Max();
			components = new double[array.Length, maxYLength];

			for (int i = 0; i < array.Length; i++) {
				for (int j = 0; j < array[i].Length; j++) {
					components[i, j] = array[i][j];
				}
			}
			isDiagonalized = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null; 
			isTriangularised = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null; 
			isIdentity = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
			determinant = null;
		}

		public Matrix(params Vector[] vectors)
		{
			if (vectors.Any(v => v.Dimension != vectors[0].Dimension)) throw new ArgumentException("Les vecteurs doivent tous avoir la même dimension", nameof(vectors));
			components = new double[vectors[0].Dimension, vectors.Length];
			for (int i = 0; i < vectors.Length; i++)
			{
				for (int j = 0; j < vectors[i].Dimension; j++)
				{
					components[j, i] = vectors[i][j];
				}
			}
			isDiagonalized = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
			isTriangularised = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
			isIdentity = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
			determinant = null;
		}

		/// <summary>
		/// Créé une copie de la matrice
		/// </summary>
		/// <param name="matrix"></param>
		public Matrix(Matrix matrix!!)
		{
			this.components = new double[matrix.components.GetLength(0), matrix.components.GetLength(1)];
			Array.Copy(matrix.components, this.components, matrix.components.Length);
			this.isDiagonalized = matrix.isDiagonalized;
			this.isTriangularised = matrix.isTriangularised;
			this.isIdentity = matrix.isIdentity;
			this.determinant = matrix.determinant;
		}

		/// <summary>
		/// Créé une matrice identité de la dimension indiquée
		/// </summary>
		/// <param name="dimension"></param>
		/// <returns></returns>
		public static Matrix Identity ( int dimension)
		{
			var array = new double[dimension, dimension];
			for (int i = 0; i < dimension; i++) {
				for (int j = 0; j < dimension; j++) {
					array[i, j] = i == j ? 1.0 : 0.0;
				}
			}
			return new Matrix(array, true, true, true, 1);
		}

		/// <summary>
		/// Créé une matrice diagonale avec les valeurs indiquées
		/// </summary>
		/// <param name="values"></param>
		/// <returns></returns>
		public static Matrix Diagonal ( params double[] values )
		{
			int dimension = values.Length;
			var array = new double[dimension, dimension];
			bool allOne = true;
			bool oneZero = false;
			double newDeterminant = 1;
			for (int i = 0; i < dimension; i++) {
				if (values[i] == 0.0) {
					oneZero = true;
					allOne = false;
				} else if (values[i] != 1.0) {
					allOne = false;
				}
				for (int j = 0; j < dimension; j++) {
					array[i, j] = i == j ? values[i] : 0.0;
				}
				newDeterminant *= values[i];
			}
			return new Matrix(array, allOne, !oneZero, !oneZero, newDeterminant);
		}

		/// <summary>
		/// Génère une matrice d'homothétie
		/// </summary>
		/// <param name="coefficients"></param>
		/// <returns></returns>
		public static Matrix Scaling(params double[] coefficients)
		{
			Matrix matrix = Matrix.Identity(coefficients.Length + 1);
			for (int i = 0; i < coefficients.Length; i++)
			{
				matrix.components[i, i] = coefficients[i];
			}
			matrix.isTriangularised = null;
			matrix.isIdentity = null;
			matrix.isDiagonalized = null;
			return matrix;
		}

		/// <summary>
		/// Génère une matrice de déformation
		/// </summary>
		/// <param name="angles"></param>
		/// <returns></returns>
		public static Matrix Skew(params double[] angles)
		{
			var dimension = (Math.Sqrt(4 * angles.Length + 1) + 1) / 2;
			if (dimension != Math.Floor(dimension)) throw new ArgumentException("La matrice de transformation n'a pas une dimension utilisable", nameof(angles));

			Matrix matrix = Matrix.Identity((int)dimension + 1);
			int i = 0;
			for (int x = 0; x < dimension; x++)
			{
				for (int y = 0; y < dimension; y++)
				{
					matrix.components[x, y >= x ? y : y + 1] = Math.Tan(angles[i]);
					i++;
				}
			}
			matrix.isTriangularised = null;
			matrix.isIdentity = null;
			matrix.isDiagonalized = null;

			return matrix;
		}

		/// <summary>
		/// Génère une matrice de rotation
		/// </summary>
		/// <param name="angles"></param>
		/// <returns></returns>
		public static Matrix Rotation(params double[] angles)
		{
			double baseComputeDimension = (1 + Math.Sqrt(8 * angles.Length + 1)) / 2;
			int dimension = (int)(Math.Floor(baseComputeDimension));
			if (baseComputeDimension != dimension)
			{
				throw new ArgumentException("Le nombre d'angles n'est pas cohérent avec une dimension", nameof(angles));
			}
			Matrix result = Matrix.Identity(dimension + 1);
			//on ne déclare qu'une fois la matrice de rotation pour optimiser la mémoire
			Matrix rotation = Matrix.Identity(dimension + 1);
			int angleIndex = 0;
			for (int dim1 = 0; dim1 < dimension; dim1++)
			{
				for (int dim2 = dim1 + 1; dim2 < dimension; dim2++)
				{
					double cos = Math.Cos(angles[angleIndex]);
					double sin = Math.Sin(angles[angleIndex]);

					rotation.components[dim1, dim1] = cos;
					rotation.components[dim2, dim2] = cos;
					rotation.components[dim1, dim2] = -sin;
					rotation.components[dim2, dim1] = sin;

					result *= rotation;

					rotation.components[dim1, dim1] = 1;
					rotation.components[dim2, dim2] = 1;
					rotation.components[dim1, dim2] = 0;
					rotation.components[dim2, dim1] = 0;
					angleIndex++;
				}
			}
			return result;
		}

		/// <summary>
		/// Génère une matrice de translation
		/// </summary>
		/// <param name="values"></param>
		/// <returns></returns>
		public static Matrix Translation ( params double[] values )
		{
			Matrix matrix = Matrix.Identity(values.Length + 1);
			int lastRow = matrix.Rows - 1;
			for (int i = 0 ; i < values.Length ; i++) {
				matrix.components[lastRow, i] = values[i];
			}
			matrix.isTriangularised = null;
			matrix.isIdentity = null;
			matrix.isDiagonalized = null;

			return matrix;
		}


		/// <summary>
		/// Génère une matrice de transformation
		/// </summary>
		/// <param name="values"></param>
		/// <returns></returns>
		public static Matrix Transform(params double[] values)
		{
			var dimension = (Math.Sqrt(4 * values.Length + 1) + 1) / 2;
			if (dimension != Math.Floor(dimension)) throw new ArgumentException("La matrice de transformation n'a pas une dimension utilisable", nameof(values));
			Matrix result = Matrix.Identity((int)dimension);

			var i = 0;
			for (int x = 0; x < result.Rows; x++)
			{
				for (int y = 0; y < result.Columns - 1; y++)
				{
					result.components[x, y] = values[i];
					i++;
				}
			}
			return result;
		}

		/// <summary>
		/// Renvoie le nombre de lignes de la matrice
		/// </summary>
		public int Rows => components.GetLength(0);

		/// <summary>
		/// Renvoie le nombre de colonnes de la matrice
		/// </summary>
		public int Columns => components.GetLength(1);

		/// <summary>
		/// Indique s'il s'agit d'une matrice carrée
		/// </summary>
		public bool IsSquare => components.GetLength(0) == components.GetLength(1);

		/// <summary>
		/// Renvoi ou défini la valeur d'un élément à la position indiqué
		/// </summary>
		/// <param name="row">Ligne</param>
		/// <param name="col">Colonne</param>
		/// <returns></returns>
		public double this[int row, int col] {
			get{
				return this.components[row, col];
			}
		}

		/// <summary>
		/// Détermine si la matrice est triangulaire ou diagonale
		/// </summary>
		private void DetermineTrianglurisedAndDiagonlized ()
		{
			if (this.isTriangularised is not null && this.isDiagonalized is not null && this.isIdentity is not null) return;
			if (!IsSquare) {
				this.isDiagonalized = false;
				this.isTriangularised = false;
				this.isIdentity = false;
				return;
			}
			int dimension =  components.GetLength(0);

			bool upside = true;
			bool downside = true;
			bool isIdentity = true;

			for (int row = 0; row < dimension; row++) {
				for (int col = 0; col < dimension; col++) {
					if (row == col) {
						if (this.components[row, col] == 0) {
							isDiagonalized = false;
							isTriangularised = false;
							return;
						} else if (this.components[row, col] != 1.0) {
							isIdentity = false;
						}
					}
					if (this.components[row, col] != 0) {
						if (row > col)
							upside = false;
						else
							downside = false;
					}
					if (!upside && !downside) break;
				}
				if (!upside && !downside) break;
			}

			this.isDiagonalized = upside || downside;
			this.isTriangularised = upside && downside;
			this.isIdentity = this.isDiagonalized.Value && isIdentity;
		}

		/// <summary>
		/// Indique si la matrice est triangulaire
		/// </summary>
		public bool IsTriangularised
		{
			get
			{
				DetermineTrianglurisedAndDiagonlized();
				return isTriangularised.Value;
			}
		}

		/// <summary>
		/// Indique si la matrice est diagonale
		/// </summary>
		public bool IsDiagonalized
		{
			get
			{
				DetermineTrianglurisedAndDiagonlized();
				return isDiagonalized.Value;
			}
		}

		/// <summary>
		/// Indique s'il s'agit d'une matrice identité
		/// </summary>
		public bool IsIdentity
		{
			get
			{
				DetermineTrianglurisedAndDiagonlized();
				return isIdentity.Value;
			}
		}

		/// <summary>
		/// Indique si la matrice permet de travailler dans un espace normal
		/// </summary>
		public bool IsNormalSpace
		{
			get
			{
				if (!IsSquare) return false;
				int lastRow = this.Rows -1;
				int lastcol = this.Columns -1;
				for (int col = 0; col < lastcol - 1; col++) {
					if (this.components[lastRow, col] != 0) return false;
				}
				return this.components[lastRow, lastcol] == 1;
			}
		}

		/// <summary>
		/// Déterminant de la matrice
		/// </summary>
		public double Determinant
		{
			get
			{
				if (determinant == null) {
					if (!IsSquare) {
						throw new InvalidOperationException("La matrice n'est pas une matrice carrée");
					}
					var columns = new ComputeColumns(components.GetLength(0));
					this.determinant = ComputeDeterminant(0, columns);
				}
				return this.determinant.Value;
			}
		}

		public Vector[] ToVectors()
		{
			Vector[] result = new Vector[Columns];
			for (int x = 0; x < Columns; x++)
			{
				double[] vComponents = new double[Rows];
				for (int y = 0; y < Rows; y++)
				{
					vComponents[y] = this[y, x];
				}
				result[x] = new Vector(vComponents);
			}
			return result;
		}

		public override string ToString ()
		{
			return ToString("", System.Globalization.CultureInfo.CurrentCulture);
		}

		public string ToString ( string format )
		{
			return ToString(format, System.Globalization.CultureInfo.CurrentCulture);
		}

		public string ToString ( string format, IFormatProvider formatProvider )
		{
			format ??= "";
			StringBuilder sb = new StringBuilder();
			string componentsSeparator = ", ";
			string lineSeparator = Environment.NewLine;
			int decimals = 2;
			
			if (formatProvider is CultureInfo c) {
				componentsSeparator = c.TextInfo.ListSeparator;
				decimals = c.NumberFormat.NumberDecimalDigits;
			} else if (formatProvider is NumberFormatInfo n) {
				if (n.CurrencyDecimalSeparator == ",") componentsSeparator = ";";
				decimals = n.NumberDecimalDigits;
			}

			switch (format.ToUpper()) {
				case "S":
					lineSeparator = " ";
					break;
				case "C":
					lineSeparator = ", ";
					break;
				case "SC":
					lineSeparator=" ; ";
					break;
			}

			sb.Append("{ ");
			for (int row = 0; row < components.GetLength(0); row++) {
				if (row > 0) sb.Append(lineSeparator);
				sb.Append("{ ");
				for (int col = 0; col < components.GetLength(1); col++) {
					if (col > 0) sb.Append(componentsSeparator);
					sb.Append(Math.Round(components[row, col], decimals));
				}
				sb.Append(" }");
			}
			sb.Append(" }");
			return sb.ToString();
		}

		public override bool Equals ( object obj )
		{
			switch (obj)
			{
				case Matrix m: return Equals(m);
				case double[,] a: return Equals(a);
				case double[][] b: return Equals(b);
				case Vector[] v: return Equals(v);
				default: return false;
			}
		}

		public bool Equals ( Matrix other)
		{
			if (other == null) return false;
			if (ReferenceEquals(this, other)) return true;
			return this.GetHashCode() == other.GetHashCode() && Equals(other.components);
		}

		public bool Equals(double[,] other)
		{
			if (other == null) return false;
			if (this.Rows != other.GetLength(0) || this.Columns != other.GetLength(1)) return false;
			for (int i = 0; i < Rows; i++)
			{
				for (int j = 0; j < Columns; j++)
				{
					if (this[i, j] != other[i, j]) return false;
				}
			}
			return true;
		}

		public bool Equals(double[][] other)
		{
			if (other == null) return false;
			if (Rows != other.GetLength(0)) return false;
			for (int i = 0; i < this.components.GetLength(0); i++)
			{
				double[] otherRow = other[i];
				if (otherRow.Length > Columns) return false;
				for (int j = 0; j < this.components.GetLength(1); j++)
				{
					if (j > otherRow.Length)
					{
						if (this[i, j] != 0) return false;
					}
					else if (this[i, j] != otherRow[j])
					{
						return false;
					}
				}
			}
			return true;
		}

		public bool Equals(params Vector[] other)
		{
			if (other == null) return false;
			if (other.Length != this.Columns) return false;

			for (int i = 0; i < Columns; i++)
			{
				var vector = other[i];
				if (vector.Dimension != this.Rows) return false;
				for (int j = 0; j < this.components.GetLength(1); j++)
				{
					if (vector[j] != this[j, i]) return false;
				}
			}
			return true;
		}

		public override int GetHashCode() => hashCode ??= ObjectUtils.ComputeHash(components);

		public double[,] ToArray() => (double[,])ArrayUtils.Copy(components);

	}
}
