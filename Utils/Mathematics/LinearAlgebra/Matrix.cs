using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics.LinearAlgebra
{
	/// <summary>
	/// Matrice
	/// </summary>
	public sealed partial class Matrix : IFormattable , IEquatable<Matrix>, IEquatable<double[,]>, IEquatable<double[][]>
	{
		internal readonly double[,] components;
		private bool? isDiagonalized;
		private bool? isTriangularised;
		private bool? isIdentity;
		private double? determinant;

		private Matrix ()
		{
		}

		public Matrix ( int dimensionX, int dimensionY )
		{
			components = new double[dimensionX, dimensionY];
		}

		private Matrix(double[,] array, bool isIdentity, bool isDiagonalized, bool isTriangularised, double? determinant)
		{
			this.components = array;
			this.isDiagonalized = isDiagonalized;
			this.isTriangularised = isTriangularised;
			this.isIdentity = isIdentity;
			this.determinant = determinant;
		}

		public Matrix ( double[,] array )
		{
			components = new double[array.GetLength(0), array.GetLength(1)];
			Array.Copy(array, this.components, array.Length);
			isDiagonalized = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
			isTriangularised = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null; 
			isIdentity = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
			determinant = null;
		}

		public Matrix ( double[][] array )
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

		/// <summary>
		/// Créé une copie de la matrice
		/// </summary>
		/// <param name="matrix"></param>
		public Matrix ( Matrix matrix )
		{
			this.components = new double[matrix.components.GetLength(0), matrix.components.GetLength(1)];
			Array.Copy(matrix.components, this.components, matrix.components.Length);
			this.isDiagonalized = matrix.isDiagonalized ;
			this.isTriangularised = matrix.isTriangularised;
			this.isIdentity = matrix.isIdentity;
			this.determinant = matrix.determinant;
		}

		/// <summary>
		/// Créé une matrice identité de la dimension indiquée
		/// </summary>
		/// <param name="dimension"></param>
		/// <returns></returns>
		public static Matrix Identity ( int dimension )
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
		/// Génère une matrice de rotation
		/// </summary>
		/// <param name="angles"></param>
		/// <returns></returns>
		public static Matrix Rotation ( params double[] angles )
		{
			return BuildRotationMatrix(angles, false);
		}

		/// <summary>
		/// Génère une matrice de rotation pour un espace normalisé
		/// </summary>
		/// <param name="angles"></param>
		/// <returns></returns>
		public static Matrix RotationInNormalSpace ( params double[] angles )
		{
			return BuildRotationMatrix(angles, true);
		}

		/// <summary>
		/// construit une matrice de rotation
		/// </summary>
		/// <param name="angles"></param>
		/// <param name="normalSpace"></param>
		/// <returns></returns>
		private static Matrix BuildRotationMatrix ( double[] angles, bool normalSpace )
		{
			double baseComputeDimension = (1 + Math.Sqrt(8 * angles.Length + 1)) / 2;
			int dimension = (int)(Math.Floor(baseComputeDimension));
			if (baseComputeDimension != dimension) {
				throw new ArgumentException("Le nombre d'angles n'est pas cohérent avec une dimension");
			}
			Matrix result = Matrix.Identity(dimension + (normalSpace ? 1 : 0));
			//on ne déclare qu'une fois la matrice de rotation pour optimiser la mémoire
			Matrix rotation = Matrix.Identity(dimension + (normalSpace ? 1 : 0));
			int angleIndex = 0;
			for (int dim1 = 0; dim1 < dimension; dim1++) {
				for (int dim2 = dim1 + 1; dim2 < dimension; dim2++) {
					double cos = Math.Cos(angles[angleIndex]);
					double sin = Math.Sin(angles[angleIndex]);

					rotation[dim1, dim1] = cos;
					rotation[dim2, dim2] = cos;
					rotation[dim1, dim2] = -sin;
					rotation[dim2, dim1] = sin;

					result *= rotation;

					rotation[dim1, dim1] = 1;
					rotation[dim2, dim2] = 1;
					rotation[dim1, dim2] = 0;
					rotation[dim2, dim1] = 0;
					angleIndex++;
				}
			}
			return result;
		}

		/// <summary>
		/// Construit une matrice de translation dans un espace normalisé
		/// </summary>
		/// <param name="values"></param>
		/// <returns></returns>
		public static Matrix TranslationInNormalSpace ( params double[] values )
		{
			Matrix result = Matrix.Identity(values.Length + 1);
			int lastColumn = result.Columns - 1;
			for (int i = 0 ; i < values.Length ; i++) {
				result[i, lastColumn] = values[i];
			}
			return result;
		}

		/// <summary>
		/// Renvoie le nombre de lignes de la matrice
		/// </summary>
		public int Rows
		{
			get { return components.GetLength(0); }
		}

		/// <summary>
		/// Renvoie le nombre de colonnes de la matrice
		/// </summary>
		public int Columns
		{
			get { return components.GetLength(1); }
		}

		/// <summary>
		/// Indique s'il s'agit d'une matrice carrée
		/// </summary>
		public bool IsSquare
		{
			get { return components.GetLength(0) == components.GetLength(1); }
		}

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
			set
			{
				this.components[row, col] = value;
				isDiagonalized = null;
				isTriangularised = null;
				determinant = null;
			}
		}

		/// <summary>
		/// Détermine si la matrice est triangulaire ou diagonale
		/// </summary>
		private void DetermineTrianglurisedAndDiagonlized ()
		{
			if (this.isTriangularised != null && this.isDiagonalized != null && this.isIdentity != null) return;
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
			format = format ?? "";
			StringBuilder sb = new StringBuilder();
			string componentsSeparator = ", ";
			string lineSeparator = Environment.NewLine;
			int decimals = 2;
			
			if (formatProvider is CultureInfo c) {
				if (c.NumberFormat.CurrencyDecimalSeparator == ",") componentsSeparator = ";";
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
			if (obj is Matrix m)
			{
				return this.Equals(m);
			}
			else if (obj is double[,] a)
			{
				return this.Equals(a);
			}
			return false;
		}

		public bool Equals ( Matrix other )
		{
			if (Object.ReferenceEquals(this, other)) return true;
			return Equals(other.components);
		}

		public bool Equals(double[,] other)
		{
			if (this.components.GetLength(0) != other.GetLength(0) || this.components.GetLength(1) != other.GetLength(1)) {
				return false;
			}
			for(int i = 0; i <	this.components.GetLength(0);i++) {
				for (int j = 0; j < this.components.GetLength(1); j++) {
					if (this.components[i, j] != other[i, j]) return false;
				}
			}
			return true;
		}

		public bool Equals(double[][] other)
		{
			if (this.components.GetLength(0) != other.GetLength(0))
			{
				return false;
			}
			for (int i = 0; i < this.components.GetLength(0); i++)
			{
				double[] otherRow = other[i];
				if (otherRow.GetLength(0) > this.components.GetLength(1)) return false;
				for (int j = 0; j < this.components.GetLength(1); j++)
				{
					if (j > otherRow.GetLength(0))
					{
						if (this.components[i, j] != 0) return false;
					}
					else if (this.components[i, j] != otherRow[j])
					{
						return false;
					}
				}
			}
			return true;
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

	}
}
