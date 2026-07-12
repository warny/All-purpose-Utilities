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
    /// <param name="dimension">Matrix dimension. Must be positive.</param>
    /// <returns>New identity matrix.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="dimension"/> is not positive.</exception>
    /// <remarks>
    /// Delegates to <see cref="Matrix{T}.Identity(int)"/>, which is the single implementation of
    /// identity-matrix construction: an earlier, independent implementation here performed no
    /// validation at all, so a zero dimension silently built a 0×0 matrix and a negative dimension
    /// failed through raw array allocation instead of a clean, documented exception — disagreeing
    /// with <see cref="Matrix{T}.Identity(int)"/>'s domain and exception behavior for the same
    /// operation (see TODO-2026-07-11-pass5.md item #62). Two independently maintained factories for
    /// the same construction had already drifted once for <see cref="Diagonal{T}(IEnumerable{T})"/>;
    /// keeping one implementation avoids a repeat.
    /// </remarks>
    public static Matrix<T> Identity<T>(int dimension)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        => Matrix<T>.Identity(dimension);

    /// <summary>
    /// Creates a diagonal matrix from the provided values.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="values">Diagonal values.</param>
    /// <returns>New diagonal matrix.</returns>
    /// <remarks>
    /// Delegates to <see cref="Matrix{T}.Diagonal(IEnumerable{T})"/>, which is the single
    /// implementation of diagonal-matrix construction: an earlier, independent implementation here
    /// cached <c>isTriangular</c>/<c>isDiagonal</c> as <see langword="false"/> whenever any diagonal
    /// value was zero, even though a matrix with zero diagonal entries is still diagonal and
    /// triangular by definition (only its invertibility/determinant changes). Two independently
    /// maintained factories for the same construction had already drifted into different, incorrect
    /// behavior; keeping one implementation avoids a repeat.
    /// </remarks>
    public static Matrix<T> Diagonal<T>(params IEnumerable<T> values)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        => Matrix<T>.Diagonal(values);

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
    /// Generates a shear (skew) matrix using the provided angles.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="angles">
    /// One angle per off-diagonal coefficient of the base <c>d × d</c> shear block, in row-major
    /// order skipping the diagonal (row 0's <c>d-1</c> off-diagonal columns, then row 1's, and so
    /// on). The base dimension <c>d</c> is inferred from the count: <c>d * (d - 1)</c> angles are
    /// required, since a full shear has one free coefficient per off-diagonal position.
    /// </param>
    /// <returns>
    /// A new <c>(d+1) × (d+1)</c> homogeneous shear matrix: identity everywhere except the supplied
    /// <c>tan(angle)</c> values at the base block's off-diagonal positions.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the angle count does not match <c>d * (d - 1)</c> for any integer <c>d</c>.</exception>
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
            for (int y = 0; y < baseDimension - 1; y++)
            {
                int column = y < x ? y : y + 1;
                array[x, column] = T.Tan(anglesArray[coefficientIndex]);
                coefficientIndex++;
            }
        }

        // Unlike hardcoded false, null defers isIdentity/isTriangular/isDiagonal to lazy recomputation
        // (see TODO-2026-07-11-pass5.md item #68): with the degenerate zero-angle/base-dimension-1 input
        // (no coefficients to place), the loop above leaves the array exactly the identity, which
        // hardcoded false could never report correctly. For any non-degenerate input, at least one
        // off-diagonal coefficient is filled on both sides of the diagonal (the loop covers every
        // off-diagonal base-block position), so recomputation still correctly resolves to false there.
        return new Matrix<T>(array, null, null, null, null);
    }

    /// <summary>
    /// Generates a rotation matrix.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="angles">
    /// Angles of rotation, one per axis pair of the base <c>d × d</c> rotation block: <c>d * (d - 1) / 2</c>
    /// angles are required for a given base dimension <c>d</c>. Must not be empty: with zero angles, the
    /// <c>d * (d - 1) / 2 = n</c> formula solves to the degenerate <c>d = 1</c> (a meaningless "1×1
    /// rotation", since rotation requires at least two axes to rotate between) rather than identifying
    /// which ambient dimension's identity rotation the caller intended (see
    /// TODO-2026-07-11-pass5.md item #66). To build an identity matrix of a specific dimension, call
    /// <see cref="Identity{T}(int)"/> directly instead.
    /// </param>
    /// <returns>New rotation matrix.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="angles"/> is empty, or its count does not match <c>d * (d - 1) / 2</c>
    /// for any integer <c>d</c>.
    /// </exception>
    public static Matrix<T> Rotation<T>(params IEnumerable<T> angles)
        where T : struct, IFloatingPoint<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
    {
        T[] anglesArray = angles.ToArray();
        if (anglesArray.Length == 0)
        {
            throw new ArgumentException(
                "At least one angle is required: an empty angle list is ambiguous about the intended " +
                $"ambient dimension (it would always resolve to a degenerate 1x1 base rotation). Use " +
                $"{nameof(Identity)}<T>(int) to build an identity matrix of a specific dimension instead.",
                nameof(angles));
        }
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

                // isIdentity/isTriangular/isDiagonal depend on the specific angle (e.g. angle = 0 makes
                // this elementary rotation the identity, which hardcoded false could never report - see
                // TODO-2026-07-11-pass5.md item #68), so null defers to lazy recomputation. The
                // determinant of any plane rotation is always cos^2 + sin^2 = 1, regardless of angle, so
                // it is supplied directly instead of left for recomputation.
                Matrix<T> rotation = new Matrix<T>(rotationArray, null, null, null, T.One);
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
        bool allZero = true;
        for (int i = 0; i < valuesArray.Length; i++)
        {
            array[i, lastColumn] = valuesArray[i];
            if (valuesArray[i] != T.Zero) allZero = false;
        }

        // Every flag below is mathematically guaranteed, not just a lazy default (see
        // TODO-2026-07-11-pass5.md item #68): the translation entries only ever occupy strictly-upper
        // positions (row i < lastColumn for every i in range), so the matrix is always upper triangular
        // regardless of the supplied values; it is diagonal/the identity exactly when every translation
        // value is zero; and its determinant is always 1 (an upper-triangular matrix with an all-ones
        // diagonal).
        return new Matrix<T>(array, isIdentity: allZero, isTriangular: true, isDiagonal: allZero, determinant: T.One);
    }

    /// <summary>
    /// Generates a general affine transformation matrix.
    /// </summary>
    /// <typeparam name="T">Numeric type of the matrix.</typeparam>
    /// <param name="values">
    /// Coefficients for the upper <c>d × (d+1)</c> affine block, in row-major order: for each of the
    /// <c>d</c> base-dimension rows, <c>d</c> linear coefficients followed by that row's translation
    /// coefficient (last column). The base dimension <c>d</c> is inferred from the count:
    /// <c>d * (d + 1)</c> values are required. For example, a 2D transform needs 6 values
    /// <c>[a, b, tx, c, d, ty]</c>, producing the matrix rows <c>[a, b, tx]</c>, <c>[c, d, ty]</c>.
    /// </param>
    /// <returns>
    /// A new <c>(d+1) × (d+1)</c> homogeneous transformation matrix: the supplied coefficients fill
    /// the upper affine block, and the final row is left as the homogeneous row <c>[0, …, 0, 1]</c>,
    /// consistent with this library's matrix-times-column-vector multiplication convention (the
    /// same convention <see cref="Translation{T}(IEnumerable{T})"/> uses for its translation column).
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the value count does not match <c>d * (d + 1)</c> for any integer <c>d</c>.</exception>
    public static Matrix<T> Transform<T>(params IEnumerable<T> values)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
    {
        T[] valuesArray = values.ToArray();
        // Solve n = d * (d + 1) for the base dimension d, then verify with exact integer
        // arithmetic (the closed-form root is only used to pick a candidate).
        double baseDimensionEstimate = (Math.Sqrt(4 * valuesArray.Length + 1) - 1) / 2;
        int baseDimension = (int)Math.Round(baseDimensionEstimate);
        if (baseDimension <= 0 || baseDimension * (baseDimension + 1) != valuesArray.Length)
            throw new ArgumentException("Invalid coefficient count for transformation matrix", nameof(values));

        int matrixDimension = baseDimension + 1;
        var array = new T[matrixDimension, matrixDimension];

        for (int row = 0; row < matrixDimension; row++)
        {
            for (int col = 0; col < matrixDimension; col++)
            {
                array[row, col] = row == col ? T.One : T.Zero;
            }
        }

        // Populate the upper d x (d+1) affine block; the final row is left untouched (homogeneous
        // [0, ..., 0, 1]) rather than overwritten with supplied coefficients.
        int index = 0;
        for (int row = 0; row < baseDimension; row++)
        {
            for (int col = 0; col < matrixDimension; col++)
            {
                array[row, col] = valuesArray[index];
                index++;
            }
        }

        // The linear block is arbitrary caller-supplied data, so none of these flags are provable at
        // construction time in general (e.g. the caller could still supply exactly the identity
        // coefficients); null defers to lazy recomputation instead of hardcoding false (see
        // TODO-2026-07-11-pass5.md item #68).
        return new Matrix<T>(array, null, null, null, null);
    }
}
