using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Arrays;
using Utils.Objects;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Matrice
/// </summary>
public sealed partial class Matrix<T> : IFormattable, IEquatable<Matrix<T>>, IEquatable<T[,]>, IEquatable<T[][]>, IEquatable<Vector<T>[]>, ICloneable
	where T : struct, IFloatingPoint<T>, IPowerFunctions<T>
{
	internal readonly T[,] components;
	private bool? isDiagonalized;
	private bool? isTriangularised;
	private bool? isIdentity;
	private T? determinant;
	private int? hashCode;

	private Matrix() { }

	public Matrix(int dimensionX, int dimensionY)
	{
		components = new T[dimensionX, dimensionY];
	}

	private Matrix(T[,] array, bool isIdentity, bool isDiagonalized, bool isTriangularised, T? determinant)
	{
		array.ArgMustNotBeNull();
		this.components = array;
		this.isDiagonalized = isDiagonalized;
		this.isTriangularised = isTriangularised;
		this.isIdentity = isIdentity;
		this.determinant = determinant;
	}

	public Matrix(T[,] array)
	{
		array.ArgMustNotBeNull();
		components = new T[array.GetLength(0), array.GetLength(1)];
		Array.Copy(array, this.components, array.Length);
		var isSquare = IsSquare;
		isDiagonalized = isSquare ? false : (bool?)null;
		isTriangularised = isSquare ? false : (bool?)null;
		isIdentity = isSquare ? false : (bool?)null;
		determinant = null;
	}

	public Matrix(T[][] array)
	{
		array.ArgMustNotBeNull();
		int maxYLength = array.Select(a => a.Length).Max();
		components = new T[array.Length, maxYLength];

		for (int i = 0; i < array.Length; i++)
		{
			for (int j = 0; j < array[i].Length; j++)
			{
				components[i, j] = array[i][j];
			}
		}
		isDiagonalized = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
		isTriangularised = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
		isIdentity = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
		determinant = null;
	}

	public Matrix(params Vector<T>[] vectors)
	{
		vectors.ArgMustNotBeNull();
		if (vectors.Any(v => v.Dimension != vectors[0].Dimension)) throw new ArgumentException("Les vecteurs doivent tous avoir la même dimension", nameof(vectors));
		components = new T[vectors[0].Dimension, vectors.Length];
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
	public Matrix(Matrix<T> matrix)
	{
		matrix.ArgMustNotBeNull();

		this.components = new T[matrix.components.GetLength(0), matrix.components.GetLength(1)];
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
	public static Matrix<T> Identity(int dimension)
	{
		var array = new T[dimension, dimension];
		for (int i = 0; i < dimension; i++)
		{
			for (int j = 0; j < dimension; j++)
			{
				array[i, j] = i == j ? T.One : T.Zero;
			}
		}
		return new Matrix<T>(array, true, true, true, T.One);
	}

	/// <summary>
	/// Créé une matrice diagonale avec les valeurs indiquées
	/// </summary>
	/// <param name="values"></param>
	/// <returns></returns>
	public static Matrix<T> Diagonal(params T[] values)
	{
		int dimension = values.Length;
		var array = new T[dimension, dimension];
		bool allOne = true;
		bool oneZero = false;
		T newDeterminant = T.One;
		for (int i = 0; i < dimension; i++)
		{
			if (values[i] == T.Zero)
			{
				oneZero = true;
				allOne = false;
			}
			else if (values[i] != T.One)
			{
				allOne = false;
			}
			for (int j = 0; j < dimension; j++)
			{
				array[i, j] = i == j ? values[i] : T.Zero;
			}
			newDeterminant *= values[i];
		}
		return new Matrix<T>(array, allOne, !oneZero, !oneZero, newDeterminant);
	}

	/// <summary>
	/// Génère une matrice d'homothétie
	/// </summary>
	/// <param name="coefficients"></param>
	/// <returns></returns>
	public static Matrix<T> Scaling(params T[] coefficients)
	{
		Matrix<T> matrix = Identity(coefficients.Length + 1);
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
	public static Matrix<T> Skew(params double[] angles)
	{
		var dimension = (Math.Sqrt(4 * angles.Length + 1) + 1) / 2;
		if (dimension != Math.Floor(dimension)) throw new ArgumentException("La matrice de transformation n'a pas une dimension utilisable", nameof(angles));

		Matrix<T> matrix = Identity((int)dimension + 1);
		int i = 0;
		for (int x = 0; x < dimension; x++)
		{
			for (int y = 0; y < dimension; y++)
			{
				matrix.components[x, y >= x ? y : y + 1] = (T)(object)Math.Tan(angles[i]);
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
	public static Matrix<T> Rotation(params double[] angles)
	{
		double baseComputeDimension = (1 + Math.Sqrt(8 * angles.Length + 1)) / 2;
		int dimension = (int)(Math.Floor(baseComputeDimension));
		if (baseComputeDimension != dimension)
		{
			throw new ArgumentException("Le nombre d'angles n'est pas cohérent avec une dimension", nameof(angles));
		}
		Matrix<T> result = Identity(dimension + 1);
		//on ne déclare qu'une fois la matrice de rotation pour optimiser la mémoire
		Matrix<T> rotation = Identity(dimension + 1);
		int angleIndex = 0;
		for (int dim1 = 0; dim1 < dimension; dim1++)
		{
			for (int dim2 = dim1 + 1; dim2 < dimension; dim2++)
			{
				T cos = (T)(object)Math.Cos(angles[angleIndex]);
				T sin = (T)(object)Math.Sin(angles[angleIndex]);

				rotation.components[dim1, dim1] = cos;
				rotation.components[dim2, dim2] = cos;
				rotation.components[dim1, dim2] = -sin;
				rotation.components[dim2, dim1] = sin;

				result *= rotation;

				rotation.components[dim1, dim1] = T.One;
				rotation.components[dim2, dim2] = T.One;
				rotation.components[dim1, dim2] = T.Zero;
				rotation.components[dim2, dim1] = T.Zero;
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
	public static Matrix<T> Translation(params T[] values)
	{
		Matrix<T> matrix = Identity(values.Length + 1);
		int lastRow = matrix.Rows - 1;
		for (int i = 0; i < values.Length; i++)
		{
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
	public static Matrix<T> Transform(params T[] values)
	{
		var dimension = (Math.Sqrt(4 * values.Length + 1) + 1) / 2;
		if (dimension != Math.Floor(dimension)) throw new ArgumentException("La matrice de transformation n'a pas une dimension utilisable", nameof(values));
		Matrix<T> result = Identity((int)dimension);

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
	public T this[int row, int col] => this.components[row, col];

	/// <summary>
	/// Détermine si la matrice est triangulaire ou diagonale
	/// </summary>
	private void DetermineTrianglurisedAndDiagonlized()
	{
		if (this.isTriangularised is not null && this.isDiagonalized is not null && this.isIdentity is not null) return;
		if (!IsSquare)
		{
			this.isDiagonalized = false;
			this.isTriangularised = false;
			this.isIdentity = false;
			return;
		}
		int dimension = components.GetLength(0);

		bool upside = true;
		bool downside = true;
		bool isIdentity = true;

		for (int row = 0; row < dimension; row++)
		{
			for (int col = 0; col < dimension; col++)
			{
				if (row == col)
				{
					if (this.components[row, col] == T.Zero)
					{
						isDiagonalized = false;
						isTriangularised = false;
						return;
					}
					else if (this.components[row, col] != T.One)
					{
						isIdentity = false;
					}
				}
				if (this.components[row, col] != T.Zero)
				{
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
			int lastRow = this.Rows - 1;
			int lastCol = this.Columns - 1;
			for (int col = 0; col < lastCol - 1; col++)
			{
				if (this.components[lastRow, col] != T.Zero) return false;
			}
			return this.components[lastRow, lastCol] == T.One;
		}
	}

	public Matrix<T> Pad(int newRows, int newColumns)
	{
		T[,] paddedMatrix = new T[newRows, newColumns];
		var rowsCount = MathEx.Min(newRows, this.Rows);
		var columnCount = MathEx.Min(newColumns, this.Columns);
		for (int i = 0; i < rowsCount; i++)
		{
			for (int j = 0; j < columnCount; j++)
			{
				paddedMatrix[i, j] = this.components[i, j];
			}
		}
		return new Matrix<T>(paddedMatrix);
	}

	/// <summary>
	/// Déterminant de la matrice
	/// </summary>
	public T Determinant
	{
		get
		{
			if (determinant is null)
			{
				if (!IsSquare)
				{
					throw new InvalidOperationException("La matrice n'est pas une matrice carrée");
				}
				var columns = new ComputeColumns<T>(components.GetLength(0));
				this.determinant = ComputeDeterminant(0, columns);
			}
			return this.determinant.Value;
		}
	}

	public Vector<T>[] ToVectors()
	{
		Vector<T>[] result = new Vector<T>[Columns];
		for (int x = 0; x < Columns; x++)
		{
			T[] vComponents = new T[Rows];
			for (int y = 0; y < Rows; y++)
			{
				vComponents[y] = this[y, x];
			}
			result[x] = new Vector<T>(vComponents);
		}
		return result;
	}

	public override string ToString() => ToString("", CultureInfo.CurrentCulture);

	public string ToString(string format) => ToString(format, CultureInfo.CurrentCulture);

	public string ToString(string format, IFormatProvider formatProvider)
	{
		format ??= "";
		StringBuilder sb = new ();
		string componentsSeparator = ", ";
		string lineSeparator = Environment.NewLine;
		int decimals = 2;

		if (formatProvider is CultureInfo c)
		{
			componentsSeparator = c.TextInfo.ListSeparator;
			decimals = c.NumberFormat.NumberDecimalDigits;
		}
		else if (formatProvider is NumberFormatInfo n)
		{
			if (n.CurrencyDecimalSeparator == ",") componentsSeparator = ";";
			decimals = n.NumberDecimalDigits;
		}

		switch (format.ToUpper())
		{
			case "S":
				lineSeparator = " ";
				break;
			case "C":
				lineSeparator = ", ";
				break;
			case "SC":
				lineSeparator = " ; ";
				break;
		}

		sb.Append("{ ");
		for (int row = 0; row < components.GetLength(0); row++)
		{
			if (row > 0) sb.Append(lineSeparator);
			sb.Append("{ ");
			for (int col = 0; col < components.GetLength(1); col++)
			{
				if (col > 0) sb.Append(componentsSeparator);
				sb.Append(MathEx.Round(components[row, col], decimals));
			}
			sb.Append(" }");
		}
		sb.Append(" }");
		return sb.ToString();
	}

	public override bool Equals(object obj) => obj switch
	{
		Matrix<T> m => Equals(m),
		T[,] a => Equals(a),
		T[][] b => Equals(b),
		Vector<T>[] v => Equals(v),
		_ => false,
	};

	public bool Equals(Matrix<T> other)
	{
		if (other is null) return false;
		if (ReferenceEquals(this, other)) return true;
		return this.GetHashCode() == other.GetHashCode() && Equals(other.components);
	}

	public bool Equals(T[,] other)
	{
		if (other is null) return false;
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

	public bool Equals(T[][] other)
	{
		if (other is null) return false;
		if (Rows != other.GetLength(0)) return false;
		for (int i = 0; i < this.components.GetLength(0); i++)
		{
			T[] otherRow = other[i];
			if (otherRow.Length > Columns) return false;
			for (int j = 0; j < this.components.GetLength(1); j++)
			{
				if (j > otherRow.Length)
				{
					if (this[i, j] != T.Zero) return false;
				}
				else if (this[i, j] != otherRow[j])
				{
					return false;
				}
			}
		}
		return true;
	}

	public bool Equals(params Vector<T>[] other)
	{
		if (other is null) return false;
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

	public object Clone() => new Matrix<T>(this.components);
}
