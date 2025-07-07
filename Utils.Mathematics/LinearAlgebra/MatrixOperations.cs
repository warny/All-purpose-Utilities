using System;
using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Provides matrix operation helpers that require advanced numeric capabilities.
/// </summary>
public static class MatrixOperations
{
    /// <summary>
    /// Multiplies a matrix by a vector.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix and vector.</typeparam>
    /// <param name="matrix">Matrix instance.</param>
    /// <param name="vector">Vector instance.</param>
    /// <returns>The resulting vector.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the matrix and vector have incompatible dimensions.</exception>
    public static Vector<T> Multiply<T>(Matrix<T> matrix, Vector<T> vector)
        where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, IRootFunctions<T>
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
                temp += matrix[row, col] * vector[col];
            }
            result[row] = temp;
        }

        return new Vector<T>(result);
    }
}
