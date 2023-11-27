using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
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
    IMultiplyOperators<Matrix<T>, Point<T>, Point<T>>,
    IDivisionOperators<Matrix<T>, T, Matrix<T>>,
	IUnaryNegationOperators<Matrix<T>, Matrix<T>>,
    IUnaryPlusOperators<Matrix<T>, Matrix<T>>
{
    public static Matrix<T> operator +(Matrix<T> Matrix1, Matrix<T> Matrix2)
	{
		if (Matrix1.components.GetLength(0) != Matrix2.components.GetLength(0) || Matrix1.components.GetLength(1) != Matrix2.components.GetLength(1))
		{
			throw new ArgumentException("Les matrices n'ont pas les mêmes dimensions");
		}
		var result = new T[Matrix1.components.GetLength(0), Matrix1.components.GetLength(1)];
		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				result[i, j] = Matrix1.components[i, j] + Matrix2.components[i, j];
			}
		}
		return new Matrix<T>(result);
	}

	public static Matrix<T> operator -(Matrix<T> Matrix1, Matrix<T> Matrix2)
	{
		if (Matrix1.components.GetLength(0) != Matrix2.components.GetLength(0) || Matrix1.components.GetLength(1) != Matrix2.components.GetLength(1))
		{
			throw new ArgumentException("Les matrices n'ont pas les mêmes dimensions");
		}
		var result = new T[Matrix1.components.GetLength(0), Matrix1.components.GetLength(1)];
		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				result[i, j] = Matrix1.components[i, j] - Matrix2.components[i, j];
			}
		}
		return new Matrix<T>(result);
	}

	public static Matrix<T> operator -(Matrix<T> matrix)
	{
		var result = new T[matrix.components.GetLength(0), matrix.components.GetLength(1)];
		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				result[i, j] = -matrix.components[i, j];
			}
		}
		return new Matrix<T>(result);
	}

	public static Matrix<T> operator *(T number, Matrix<T> matrix)
	{
		var result = new T[matrix.components.GetLength(0), matrix.components.GetLength(1)];

		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				result[i, j] = MathEx.Round(number * matrix.components[i, j], 15);
			}
		}
		return new Matrix<T>(result);
	}

	public static Matrix<T> operator *(Matrix<T> matrix, T number) => number * matrix;

	public static Matrix<T> operator /(Matrix<T> Matrix, T number)
	{
		var result = new T[Matrix.components.GetLength(0), Matrix.components.GetLength(1)];

		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				result[i, j] = Matrix.components[i, j] / number;
			}
		}
		return new Matrix<T>(result);
	}

	public static Matrix<T> operator *(Matrix<T> matrix1, Matrix<T> matrix2)
	{
		if (matrix1.components.GetLength(0) != matrix2.components.GetLength(1))
		{
			throw new ArgumentException("Les matrices n'ont pas de dimensions compatibles");
		}
		var depth = matrix1.components.GetLength(0);
		var result = new T[matrix1.components.GetLength(1), matrix2.components.GetLength(0)];

		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				T temp = T.Zero;
				for (int k = 0; k < depth; k++)
				{
					temp += matrix1.components[i, k] * matrix2.components[k, j];
				}
				result[i, j] = temp;
			}
		}

		return new Matrix<T>(result);
	}

    public static Vector<T> operator *(Matrix<T> matrix, Vector<T> vector)
    {
        if (matrix.Columns != vector.Dimension)
        {
            throw new ArgumentException("Les dimensions de la matrice et du vecteur ne sont pas compatibles avec cette opération");
        }

        T[] result = new T[vector.Dimension];
        for (int row = 0; row < matrix.Rows; row++)
        {
            T temp = T.Zero;
            for (int col = 0; col < matrix.Columns; col++)
            {
                temp += matrix[row, col] * vector.components[col];
            }
            result[row] = temp;
        }
        return new Vector<T>(result);
    }

    public static Point<T> operator *(Matrix<T> matrix, Point<T> point)
    {
        if (!matrix.IsSquare)
        {
            throw new ArgumentException("La matrice doit être carrée");
        }
        else if (!(matrix.Rows == point.Dimension || (matrix.Rows - 1 == point.Dimension && matrix.IsNormalSpace)))
        {
            throw new ArgumentException("Les dimensions de la matrice et du point ne sont pas compatibles avec cette opération");
        }
        T[] result = new T[point.Dimension + 1];
        result[^1] = T.One;
        for (int row = 0; row < matrix.Rows; row++)
        {
            T temp = T.One;
            for (int col = 0; col < matrix.Columns; col++)
            {
                temp += matrix[row, col] * point.components[col];
            }
            result[row] = temp;
        }
        if (result[^1] != T.One)        {
            throw new InvalidOperationException("Erreur lors de l'application de la matrice de transformation");
        }
        return new Point<T>(result);
    }
    public static Matrix<T> operator +(Matrix<T> value) => new Matrix<T>(value);

    public static bool operator ==(Matrix<T> matrix1, Matrix<T> matrix2) => matrix1.Equals(matrix2);
	public static bool operator !=(Matrix<T> matrix1, Matrix<T> matrix2) => !matrix1.Equals(matrix2);
	public static bool operator ==(Matrix<T> matrix, object obj) => matrix.Equals(obj);
	public static bool operator !=(Matrix<T> matrix1, object obj) => !matrix1.Equals(obj);

}
