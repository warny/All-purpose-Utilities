using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics.LinearAlgebra;

public partial class Matrix :
	IAdditionOperators<Matrix, Matrix, Matrix>,
    ISubtractionOperators<Matrix, Matrix, Matrix>,
    IEqualityOperators<Matrix, Matrix, bool>,
    IEqualityOperators<Matrix, object, bool>,
    IMultiplyOperators<Matrix, Matrix, Matrix>,
	IMultiplyOperators<Matrix, double, Matrix>,
    IMultiplyOperators<Matrix, Vector, Vector>,
    IMultiplyOperators<Matrix, Point, Point>,
    IDivisionOperators<Matrix, double, Matrix>
{
    public static Matrix operator +(Matrix matrix1, Matrix matrix2)
	{
		if (matrix1.components.GetLength(0) != matrix2.components.GetLength(0) || matrix1.components.GetLength(1) != matrix2.components.GetLength(1))
		{
			throw new ArgumentException("Les matrices n'ont pas les mêmes dimensions");
		}
		var result = new double[matrix1.components.GetLength(0), matrix1.components.GetLength(1)];
		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				result[i, j] = matrix1.components[i, j] + matrix2.components[i, j];
			}
		}
		return new Matrix(result);
	}

	public static Matrix operator -(Matrix matrix1, Matrix matrix2)
	{
		if (matrix1.components.GetLength(0) != matrix2.components.GetLength(0) || matrix1.components.GetLength(1) != matrix2.components.GetLength(1))
		{
			throw new ArgumentException("Les matrices n'ont pas les mêmes dimensions");
		}
		var result = new double[matrix1.components.GetLength(0), matrix1.components.GetLength(1)];
		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				result[i, j] = matrix1.components[i, j] - matrix2.components[i, j];
			}
		}
		return new Matrix(result);
	}

	public static Matrix operator -(Matrix matrix)
	{
		var result = new double[matrix.components.GetLength(0), matrix.components.GetLength(1)];
		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				result[i, j] = -matrix.components[i, j];
			}
		}
		return new Matrix(result);
	}

	public static Matrix operator *(double number, Matrix matrix)
	{
		var result = new double[matrix.components.GetLength(0), matrix.components.GetLength(1)];

		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				result[i, j] = Math.Round(number * matrix.components[i, j], 15);
			}
		}
		return new Matrix(result);
	}

	public static Matrix operator *(Matrix matrix, double number) => number * matrix;

	public static Matrix operator /(Matrix matrix, double number)
	{
		var result = new double[matrix.components.GetLength(0), matrix.components.GetLength(1)];

		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				result[i, j] = matrix.components[i, j] / number;
			}
		}
		return new Matrix(result);
	}

	public static Matrix operator *(Matrix matrix1, Matrix matrix2)
	{
		if (matrix1.components.GetLength(0) != matrix2.components.GetLength(1))
		{
			throw new ArgumentException("Les matrices n'ont pas de dimensions compatibles");
		}
		var depth = matrix1.components.GetLength(0);
		var result = new double[matrix1.components.GetLength(1), matrix2.components.GetLength(0)];

		for (int i = 0; i < result.GetLength(0); i++)
		{
			for (int j = 0; j < result.GetLength(1); j++)
			{
				double temp = 0;
				for (int k = 0; k < depth; k++)
				{
					temp += matrix1.components[i, k] * matrix2.components[k, j];
				}
				result[i, j] = temp;
			}
		}

		return new Matrix(result);
	}

    public static Vector operator *(Matrix matrix, Vector vector)
    {
        if (matrix.Columns != vector.Dimension)
        {
            throw new ArgumentException("Les dimensions de la matrice et du vecteur ne sont pas compatibles avec cette opération");
        }

        double[] result = new double[vector.Dimension];
        for (int row = 0; row < matrix.Rows; row++)
        {
            double temp = 0;
            for (int col = 0; col < matrix.Columns; col++)
            {
                temp += matrix[row, col] * vector.components[col];
            }
            result[row] = temp;
        }
        return new Vector(result);
    }

    public static Point operator *(Matrix matrix, Point point)
    {
        if (!matrix.IsSquare)
        {
            throw new ArgumentException("La matrice doit être carrée");
        }
        else if (!(matrix.Rows == point.Dimension || (matrix.Rows - 1 == point.Dimension && matrix.IsNormalSpace)))
        {
            throw new ArgumentException("Les dimensions de la matrice et du point ne sont pas compatibles avec cette opération");
        }
        double[] result = new double[point.Dimension + 1];
        result[^1] = 1;
        for (int row = 0; row < matrix.Rows; row++)
        {
            double temp = 0;
            for (int col = 0; col < matrix.Columns; col++)
            {
                temp += matrix[row, col] * point.components[col];
            }
            result[row] = temp;
        }
        if (result[^1] != 1)
        {
            throw new InvalidOperationException("Erreur lors de l'application de la matrice de transformation");
        }
        return new Point(result);
    }

    public static bool operator ==(Matrix matrix1, Matrix matrix2) => matrix1.Equals(matrix2);
	public static bool operator !=(Matrix matrix1, Matrix matrix2) => !matrix1.Equals(matrix2);
	public static bool operator ==(Matrix matrix1, object obj) => matrix1.Equals(obj);
	public static bool operator !=(Matrix matrix1, object obj) => !matrix1.Equals(obj);
}
