using System;
using System.Numerics;
using Utils.Objects;

namespace Utils.Mathematics.LinearAlgebra;

public partial class Matrix<T> :
	IAdditionOperators<Matrix<T>, Matrix<T>, Matrix<T>>,
	ISubtractionOperators<Matrix<T>, Matrix<T>, Matrix<T>>,
	IEqualityOperators<Matrix<T>, Matrix<T>, bool>,
	IEqualityOperators<Matrix<T>, object, bool>,
	IMultiplyOperators<Matrix<T>, Matrix<T>, Matrix<T>>,
	IMultiplyOperators<Matrix<T>, T, Matrix<T>>,
	IMultiplyOperators<Matrix<T>, Vector<T>, Vector<T>>,
	IDivisionOperators<Matrix<T>, T, Matrix<T>>,
	IUnaryNegationOperators<Matrix<T>, Matrix<T>>,
	IUnaryPlusOperators<Matrix<T>, Matrix<T>>
{
	/// <summary>
	/// Adds two matrices element-wise.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the matrices have different dimensions.</exception>
	public static Matrix<T> operator +(Matrix<T> matrix1, Matrix<T> matrix2)
	{
		if (matrix1.Rows != matrix2.Rows || matrix1.Columns != matrix2.Columns)
		{
			throw new InvalidOperationException("The matrices do not have the same dimensions.");
		}

		var result = new T[matrix1.Rows, matrix1.Columns];
		for (int i = 0; i < matrix1.Rows; i++)
		{
			for (int j = 0; j < matrix1.Columns; j++)
			{
				result[i, j] = matrix1.components[i, j] + matrix2.components[i, j];
			}
		}
		return new Matrix<T>(result);
	}

	/// <summary>
	/// Subtracts one matrix from another element-wise.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the matrices have different dimensions.</exception>
	public static Matrix<T> operator -(Matrix<T> matrix1, Matrix<T> matrix2)
	{
		if (matrix1.Rows != matrix2.Rows || matrix1.Columns != matrix2.Columns)
		{
			throw new InvalidOperationException("The matrices do not have the same dimensions.");
		}

		var result = new T[matrix1.Rows, matrix1.Columns];
		for (int i = 0; i < matrix1.Rows; i++)
		{
			for (int j = 0; j < matrix1.Columns; j++)
			{
				result[i, j] = matrix1.components[i, j] - matrix2.components[i, j];
			}
		}
		return new Matrix<T>(result);
	}

	/// <summary>
	/// Negates all elements of the matrix.
	/// </summary>
	public static Matrix<T> operator -(Matrix<T> matrix)
	{
		var result = new T[matrix.Rows, matrix.Columns];
		for (int i = 0; i < matrix.Rows; i++)
		{
			for (int j = 0; j < matrix.Columns; j++)
			{
				result[i, j] = -matrix.components[i, j];
			}
		}
		return new Matrix<T>(result);
	}

	/// <summary>
	/// Multiplies a matrix by a scalar.
	/// </summary>
	public static Matrix<T> operator *(T scalar, Matrix<T> matrix)
	{
		var result = new T[matrix.Rows, matrix.Columns];
		for (int i = 0; i < matrix.Rows; i++)
		{
			for (int j = 0; j < matrix.Columns; j++)
			{
				result[i, j] = scalar * matrix.components[i, j];
			}
		}
		return new Matrix<T>(result);
	}

	/// <summary>
	/// Multiplies a matrix by a scalar.
	/// </summary>
	public static Matrix<T> operator *(Matrix<T> matrix, T scalar) => scalar * matrix;

	/// <summary>
	/// Divides a matrix by a scalar.
	/// </summary>
	public static Matrix<T> operator /(Matrix<T> matrix, T scalar)
	{
		var result = new T[matrix.Rows, matrix.Columns];
		for (int i = 0; i < matrix.Rows; i++)
		{
			for (int j = 0; j < matrix.Columns; j++)
			{
				result[i, j] = matrix.components[i, j] / scalar;
			}
		}
		return new Matrix<T>(result);
	}

	/// <summary>
	/// Multiplies two matrices.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the matrices have incompatible dimensions for multiplication.</exception>
	public static Matrix<T> operator *(Matrix<T> matrix1, Matrix<T> matrix2)
	{
		if (matrix1.Columns != matrix2.Rows)
		{
			throw new InvalidOperationException("The matrices have incompatible dimensions for multiplication.");
		}

		var result = new T[matrix1.Rows, matrix2.Columns];
		for (int i = 0; i < matrix1.Rows; i++)
		{
			for (int j = 0; j < matrix2.Columns; j++)
			{
				T temp = T.Zero;
				for (int k = 0; k < matrix1.Columns; k++)
				{
					temp += matrix1.components[i, k] * matrix2.components[k, j];
				}
				result[i, j] = temp;
			}
		}
		return new Matrix<T>(result);
	}

	/// <summary>
	/// Multiplies a matrix by a vector.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the matrix and vector have incompatible dimensions.</exception>
	public static Vector<T> operator *(Matrix<T> matrix, Vector<T> vector)
	{
		if (matrix.Columns != vector.Dimension)
		{
			throw new InvalidOperationException("The matrix and vector have incompatible dimensions.");
		}

		T[] result = new T[matrix.Rows];
		for (int row = 0; row < matrix.Rows; row++)
		{
			T temp = T.Zero;
			for (int col = 0; col < matrix.Columns; col++)
			{
				temp += matrix.components[row, col] * vector[col];
			}
			result[row] = temp;
		}
		return new Vector<T>(result);
	}

	/// <summary>
	/// Returns the matrix itself (unary plus).
	/// </summary>
	public static Matrix<T> operator +(Matrix<T> value) => new(value);

	/// <summary>
	/// Checks if two matrices are equal.
	/// </summary>
	public static bool operator ==(Matrix<T> matrix1, Matrix<T> matrix2) => matrix1?.Equals(matrix2) ?? matrix2 is null;

	/// <summary>
	/// Checks if two matrices are not equal.
	/// </summary>
	public static bool operator !=(Matrix<T> matrix1, Matrix<T> matrix2) => !matrix1?.Equals(matrix2) ?? matrix2 is not null;

	/// <summary>
	/// Checks if the matrix is equal to an object.
	/// </summary>
	public static bool operator ==(Matrix<T> matrix, object obj) => matrix?.Equals(obj) ?? obj is null;

	/// <summary>
	/// Checks if the matrix is not equal to an object.
	/// </summary>
	public static bool operator !=(Matrix<T> matrix, object obj) => !matrix?.Equals(obj) ?? obj is not null;
}
