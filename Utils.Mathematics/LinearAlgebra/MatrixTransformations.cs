using System;
using System.Numerics;
using System.Collections.Concurrent;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Provides factory methods to create common transformation matrices.
/// </summary>
public static class MatrixTransformations
{
    /// <summary>
    /// Generic cache for identity matrices keyed by their dimension.
    /// </summary>
    private static class IdentityCache<T> where T : struct, IFloatingPoint<T>
    {
        internal static readonly System.Collections.Concurrent.ConcurrentDictionary<int, Matrix<T>> Matrices = new();
    }
    /// <summary>
    /// Creates an identity matrix of the specified dimension.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="dimension">Matrix dimension.</param>
    /// <returns>New identity matrix.</returns>
    public static Matrix<T> Identity<T>(int dimension)
        where T : struct, IFloatingPoint<T>
    {
        if (dimension <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be greater than zero.");
        }

        Matrix<T> cached = IdentityCache<T>.Matrices.GetOrAdd(dimension, static dim =>
        {
            T[,] array = new T[dim, dim];
            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    array[i, j] = i == j ? T.One : T.Zero;
                }
            }
            return new Matrix<T>(array, true, true, true, T.One);
        });

        return new Matrix<T>(cached);
    }

    /// <summary>
    /// Creates a diagonal matrix from the provided values.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="values">Diagonal values.</param>
    /// <returns>New diagonal matrix.</returns>
    public static Matrix<T> Diagonal<T>(params T[] values)
        where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
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
    /// Generates a scaling matrix.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="coefficients">Scaling factors for each axis.</param>
    /// <returns>New scaling matrix.</returns>
    public static Matrix<T> Scaling<T>(params T[] coefficients)
        where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
    {
        Matrix<T> matrix = Identity<T>(coefficients.Length + 1);
        for (int i = 0; i < coefficients.Length; i++)
        {
            matrix.components[i, i] = coefficients[i];
        }
        matrix.ResetMatrixProperties();
        return matrix;
    }

    /// <summary>
    /// Generates a shear matrix using the provided angles.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="angles">Angles of the shear.</param>
    /// <returns>New shear matrix.</returns>
    public static Matrix<T> Skew<T>(params T[] angles)
        where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
    {
        var dimension = (Math.Sqrt(4 * angles.Length + 1) + 1) / 2;
        if (dimension != Math.Floor(dimension))
            throw new ArgumentException("Invalid dimension for skew matrix", nameof(angles));

        Matrix<T> matrix = Identity<T>((int)dimension + 1);
        int i = 0;
        for (int x = 0; x < dimension; x++)
        {
            for (int y = 0; y < dimension; y++)
            {
                matrix.components[x, y >= x ? y : y + 1] = T.Tan(angles[i]);
                i++;
            }
        }
        matrix.ResetMatrixProperties();
        return matrix;
    }

    /// <summary>
    /// Generates a rotation matrix.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="angles">Angles of rotation.</param>
    /// <returns>New rotation matrix.</returns>
    public static Matrix<T> Rotation<T>(params T[] angles)
        where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
    {
        double baseComputeDimension = (1 + Math.Sqrt(8 * angles.Length + 1)) / 2;
        int dimension = (int)Math.Floor(baseComputeDimension);
        if (baseComputeDimension != dimension)
        {
            throw new ArgumentException("Angles count does not match a dimension", nameof(angles));
        }
        Matrix<T> result = Identity<T>(dimension + 1);
        Matrix<T> rotation = Identity<T>(dimension + 1);
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
    /// Generates a translation matrix.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="values">Translation values.</param>
    /// <returns>New translation matrix.</returns>
    public static Matrix<T> Translation<T>(params T[] values)
        where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
    {
        Matrix<T> matrix = Identity<T>(values.Length + 1);
        int lastRow = matrix.Rows - 1;
        for (int i = 0; i < values.Length; i++)
        {
            matrix.components[lastRow, i] = values[i];
        }
        matrix.ResetMatrixProperties();
        return matrix;
    }

    /// <summary>
    /// Generates a transformation matrix.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="values">Transformation coefficients.</param>
    /// <returns>New transformation matrix.</returns>
    public static Matrix<T> Transform<T>(params T[] values)
        where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
    {
        var dimension = (Math.Sqrt(4 * values.Length + 1) + 1) / 2;
        if (dimension != Math.Floor(dimension))
            throw new ArgumentException("Invalid dimension for transformation matrix", nameof(values));
        Matrix<T> result = Identity<T>((int)dimension);

        int i = 0;
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
