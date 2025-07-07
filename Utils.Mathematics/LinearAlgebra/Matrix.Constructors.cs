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

        internal Matrix(T[,] array, bool isIdentity, bool isDiagonalized, bool isTriangularised, T? determinant)
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


}
