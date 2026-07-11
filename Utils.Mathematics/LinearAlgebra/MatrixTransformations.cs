using System;
using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Provides factory methods to create common transformation matrices.
/// </summary>
public static class MatrixTransformations
{
    /// <summary>
    /// Creates an identity matrix of the specified dimension.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="dimension">Matrix dimension.</param>
    /// <returns>New identity matrix.</returns>
    public static Matrix<T> Identity<T>(int dimension)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
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
    /// Creates a diagonal matrix from the provided values.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="values">Diagonal values.</param>
    /// <returns>New diagonal matrix.</returns>
    public static Matrix<T> Diagonal<T>(params IEnumerable<T> values)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
    {
        T[] valuesArray = values.ToArray();
        int dimension = valuesArray.Length;
        var array = new T[dimension, dimension];
        bool allOne = true;
        bool oneZero = false;
        T newDeterminant = T.One;
        for (int i = 0; i < dimension; i++)
        {
            if (valuesArray[i] == T.Zero)
            {
                oneZero = true;
                allOne = false;
            }
            else if (valuesArray[i] != T.One)
            {
                allOne = false;
            }
            for (int j = 0; j < dimension; j++)
            {
                array[i, j] = i == j ? valuesArray[i] : T.Zero;
            }
            newDeterminant *= valuesArray[i];
        }
        return new Matrix<T>(array, allOne, !oneZero, !oneZero, newDeterminant);
    }

    /// <summary>
    /// Generates a scaling matrix.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="coefficients">Scaling factors for each axis.</param>
    /// <returns>New scaling matrix.</returns>
    public static Matrix<T> Scaling<T>(params IEnumerable<T> coefficients)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
    {
        T[] coefficientsArray = coefficients.ToArray();
        int dimension = coefficientsArray.Length + 1;
        var array = new T[dimension, dimension];
        bool allOne = true;
        T determinant = T.One;

        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j < dimension; j++)
            {
                bool isDiagonal = i == j;
                if (!isDiagonal)
                {
                    array[i, j] = T.Zero;
                    continue;
                }

                T value = i < coefficientsArray.Length ? coefficientsArray[i] : T.One;
                array[i, j] = value;
                determinant *= value;
                allOne &= value == T.One;
            }
        }

        return new Matrix<T>(array, allOne, true, true, determinant);
    }

    /// <summary>
    /// Generates a shear matrix using the provided angles.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="angles">Angles of the shear.</param>
    /// <returns>New shear matrix.</returns>
    public static Matrix<T> Skew<T>(params IEnumerable<T> angles)
        where T : struct, IFloatingPoint<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
    {
        T[] anglesArray = angles.ToArray();
        var dimension = (Math.Sqrt(4 * anglesArray.Length + 1) + 1) / 2;
        if (dimension != Math.Floor(dimension))
            throw new ArgumentException("Invalid dimension for skew matrix", nameof(angles));

        int baseDimension = (int)dimension;
        int matrixDimension = baseDimension + 1;
        var array = new T[matrixDimension, matrixDimension];

        for (int idx = 0; idx < matrixDimension; idx++)
        {
            for (int j = 0; j < matrixDimension; j++)
            {
                array[idx, j] = idx == j ? T.One : T.Zero;
            }
        }

        int coefficientIndex = 0;
        for (int x = 0; x < baseDimension; x++)
        {
            for (int y = 0; y < baseDimension; y++)
            {
                int column = y >= x ? y : y + 1;
                array[x, column] = T.Tan(anglesArray[coefficientIndex]);
                coefficientIndex++;
            }
        }

        return new Matrix<T>(array, false, false, false, null);
    }

    /// <summary>
    /// Generates a rotation matrix.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="angles">Angles of rotation.</param>
    /// <returns>New rotation matrix.</returns>
    public static Matrix<T> Rotation<T>(params IEnumerable<T> angles)
        where T : struct, IFloatingPoint<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
    {
        T[] anglesArray = angles.ToArray();
        double baseComputeDimension = (1 + Math.Sqrt(8 * anglesArray.Length + 1)) / 2;
        int dimension = (int)Math.Floor(baseComputeDimension);
        if (baseComputeDimension != dimension)
        {
            throw new ArgumentException("Angles count does not match a dimension", nameof(angles));
        }
        Matrix<T> result = Identity<T>(dimension + 1);
        int angleIndex = 0;
        for (int dim1 = 0; dim1 < dimension; dim1++)
        {
            for (int dim2 = dim1 + 1; dim2 < dimension; dim2++)
            {
                T cos = T.Cos(anglesArray[angleIndex]);
                T sin = T.Sin(anglesArray[angleIndex]);

                int matrixDimension = dimension + 1;
                var rotationArray = new T[matrixDimension, matrixDimension];
                for (int i = 0; i < matrixDimension; i++)
                {
                    for (int j = 0; j < matrixDimension; j++)
                    {
                        rotationArray[i, j] = i == j ? T.One : T.Zero;
                    }
                }

                rotationArray[dim1, dim1] = cos;
                rotationArray[dim2, dim2] = cos;
                rotationArray[dim1, dim2] = -sin;
                rotationArray[dim2, dim1] = sin;

                Matrix<T> rotation = new Matrix<T>(rotationArray, false, false, false, null);
                result *= rotation;
                angleIndex++;
            }
        }
        return result;
    }

    /// <summary>
    /// Generates a translation matrix.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="values">Translation values.</param>
    /// <returns>New translation matrix.</returns>
    public static Matrix<T> Translation<T>(params IEnumerable<T> values)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
    {
        T[] valuesArray = values.ToArray();
        int dimension = valuesArray.Length + 1;
        var array = new T[dimension, dimension];

        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j < dimension; j++)
            {
                array[i, j] = i == j ? T.One : T.Zero;
            }
        }

        int lastColumn = dimension - 1;
        for (int i = 0; i < valuesArray.Length; i++)
        {
            array[i, lastColumn] = valuesArray[i];
        }

        return new Matrix<T>(array, false, false, false, null);
    }

    /// <summary>
    /// Generates a transformation matrix.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="values">Transformation coefficients.</param>
    /// <returns>New transformation matrix.</returns>
    public static Matrix<T> Transform<T>(params IEnumerable<T> values)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
    {
        T[] valuesArray = values.ToArray();
        var dimension = (Math.Sqrt(4 * valuesArray.Length + 1) + 1) / 2;
        if (dimension != Math.Floor(dimension))
            throw new ArgumentException("Invalid dimension for transformation matrix", nameof(values));
        int matrixDimension = (int)dimension;
        var array = new T[matrixDimension, matrixDimension];

        for (int row = 0; row < matrixDimension; row++)
        {
            for (int col = 0; col < matrixDimension; col++)
            {
                array[row, col] = row == col ? T.One : T.Zero;
            }
        }

        int index = 0;
        for (int x = 0; x < matrixDimension; x++)
        {
            for (int y = 0; y < matrixDimension - 1; y++)
            {
                array[x, y] = valuesArray[index];
                index++;
            }
        }

        return new Matrix<T>(array, false, false, false, null);
    }
}
