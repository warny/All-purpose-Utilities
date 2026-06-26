using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Static factory methods for common <see cref="Matrix{T}"/> forms.
/// </summary>
public sealed partial class Matrix<T>
{
    /// <summary>
    /// Returns an n×n identity matrix.
    /// </summary>
    /// <param name="size">Number of rows and columns.</param>
    public static Matrix<T> Identity(int size)
    {
        if (size <= 0) throw new ArgumentException("Matrix size must be positive", nameof(size));
        var array = new T[size, size];
        for (int i = 0; i < size; i++)
            array[i, i] = T.One;
        return new Matrix<T>(array, isIdentity: true, isTriangular: true, isDiagonal: true, determinant: T.One);
    }

    /// <summary>
    /// Returns a matrix of the specified dimensions with all elements set to zero.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="columns">Number of columns.</param>
    public static Matrix<T> Zero(int rows, int columns)
    {
        if (rows <= 0) throw new ArgumentException("Row count must be positive", nameof(rows));
        if (columns <= 0) throw new ArgumentException("Column count must be positive", nameof(columns));
        return new Matrix<T>(rows, columns);
    }

    /// <summary>
    /// Returns a square diagonal matrix whose diagonal entries are <paramref name="values"/>.
    /// </summary>
    /// <param name="values">Diagonal values, left to right.</param>
    public static Matrix<T> Diagonal(params IEnumerable<T> values)
    {
        T[] valuesArray = values.ToArray();
        if (valuesArray.Length == 0) throw new ArgumentException("At least one diagonal value is required", nameof(values));
        int n = valuesArray.Length;
        var array = new T[n, n];
        T det = T.One;
        bool isIdentity = true;
        for (int i = 0; i < n; i++)
        {
            array[i, i] = valuesArray[i];
            det *= valuesArray[i];
            if (valuesArray[i] != T.One) isIdentity = false;
        }
        // A diagonal matrix is always triangular and diagonal regardless of zero entries on the diagonal.
        return new Matrix<T>(array, isIdentity: isIdentity, isTriangular: true, isDiagonal: true, determinant: det);
    }
}
