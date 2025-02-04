using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Mathematics.LinearAlgebra;

public sealed partial class Matrix<T>
{
	public Matrix(int dimensionX, int dimensionY)
	{
		components = new T[dimensionX, dimensionY];
	}

	private Matrix(T[,] array, bool isIdentity, bool isDiagonalized, bool isTriangularised, T? determinant)
	{
		array.Arg().MustNotBeNull();
		this.components = array;
		this.isDiagonalized = isDiagonalized;
		this.isTriangularised = isTriangularised;
		this.isIdentity = isIdentity;
		this.determinant = determinant;
	}

	public Matrix(T[,] array)
	{
		array.Arg().MustNotBeNull();
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
		array.Arg().MustNotBeNull();
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
		vectors.Arg().MustNotBeNull();
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
		matrix.Arg().MustNotBeNull();

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
	public static Matrix<T> Skew(params T[] angles)
	{
		var dimension = (Math.Sqrt(4 * angles.Length + 1) + 1) / 2;
		if (dimension != Math.Floor(dimension)) throw new ArgumentException("La matrice de transformation n'a pas une dimension utilisable", nameof(angles));

		Matrix<T> matrix = Identity((int)dimension + 1);
		int i = 0;
		for (int x = 0; x < dimension; x++)
		{
			for (int y = 0; y < dimension; y++)
			{
				matrix.components[x, y >= x ? y : y + 1] = T.Tan(angles[i]);
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
	public static Matrix<T> Rotation(params T[] angles)
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
				T cos = T.Cos(angles[angleIndex]);
				T sin = T.Sin(angles[angleIndex]);

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

}
