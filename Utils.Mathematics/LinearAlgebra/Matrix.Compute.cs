using System;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Provides advanced computations for the <see cref="Matrix{T}"/> type.
/// </summary>
public partial class Matrix<T>
{
    /// <summary>
    /// Returns the largest absolute value among an array-based matrix's entries, used to derive a
    /// scale-aware (rather than absolute) pivot tolerance for singularity checks.
    /// </summary>
    private static T MaxAbsoluteEntry(T[,] matrix)
    {
        T max = T.Zero;
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                max = T.Max(max, T.Abs(matrix[i, j]));
        return max;
    }

    /// <summary>
    /// Swaps two rows of an array-based matrix in place.
    /// </summary>
    /// <param name="matrix">Matrix whose rows should be permuted.</param>
    /// <param name="row1">First row index.</param>
    /// <param name="row2">Second row index.</param>
    /// <param name="limit">Optional limit on the number of columns to swap. If negative, all columns are swapped.</param>
    private static void PermuteRows(T[,] matrix, int row1, int row2, int limit = -1)
    {
        if (row1 == row2)
        {
            return;
        }

        int cols = limit >= 0 ? limit : matrix.GetLength(1);
        for (int col = 0; col < cols; col++)
        {
            (matrix[row1, col], matrix[row2, col]) = (matrix[row2, col], matrix[row1, col]);
        }
    }

    /// <summary>
    /// Performs a pivoted LU decomposition of the current square matrix: a lower unitriangular
    /// matrix L, an upper triangular matrix U, and a permutation matrix P such that P * A = L * U,
    /// where A is the current matrix.
    /// </summary>
    /// <remarks>
    /// Uses partial pivoting (largest-magnitude pivot in each column) and stores the elimination
    /// multipliers directly in L, as required for L to be the actual lower-triangular LU factor
    /// rather than a by-product of applying the elimination row operations to an identity matrix.
    /// Operates entirely on local array copies to preserve immutability.
    /// </remarks>
    /// <returns>A tuple containing the lower-triangular, upper-triangular, and permutation matrices.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the matrix is not square or is singular.</exception>
    public (Matrix<T> L, Matrix<T> U, Matrix<T> P) DiagonalizeLU()
    {
        if (!IsSquare)
        {
            throw new InvalidOperationException("The matrix must be square for LU decomposition.");
        }

        int n = Rows;
        T[,] u = ToArray();
        T[,] l = new T[n, n];
        int[] permutation = new int[n];

        for (int i = 0; i < n; i++)
        {
            l[i, i] = T.One;
            permutation[i] = i;
        }

        for (int k = 0; k < n; k++)
        {
            int pivotRow = k;
            for (int i = k + 1; i < n; i++)
            {
                if (T.Abs(u[i, k]) > T.Abs(u[pivotRow, k]))
                {
                    pivotRow = i;
                }
            }

            if (u[pivotRow, k].Equals(T.Zero))
            {
                throw new InvalidOperationException("The matrix is singular and cannot be decomposed.");
            }

            if (pivotRow != k)
            {
                PermuteRows(u, k, pivotRow);
                // Only the already-computed multiplier columns (0..k-1) need to move with the row;
                // column k onward is either not yet computed or the identity diagonal being formed.
                PermuteRows(l, k, pivotRow, k);
                (permutation[k], permutation[pivotRow]) = (permutation[pivotRow], permutation[k]);
            }

            for (int row = k + 1; row < n; row++)
            {
                T multiplier = u[row, k] / u[k, k];
                l[row, k] = multiplier;
                for (int col = k; col < n; col++)
                {
                    u[row, col] -= multiplier * u[k, col];
                }
            }
        }

        T[,] p = new T[n, n];
        for (int i = 0; i < n; i++)
        {
            p[i, permutation[i]] = T.One;
        }

        Matrix<T> L = new Matrix<T>(l, false, true, false, T.One);
        Matrix<T> U = new Matrix<T>(u, false, true, false, null);
        Matrix<T> P = new Matrix<T>(p, false, false, false, null);
        return (L, U, P);
    }

    /// <summary>
    /// Inverts the matrix if it is invertible.
    /// </summary>
    /// <param name="relativeSingularityTolerance">
    /// Overrides the default relative pivot tolerance (see <see cref="DefaultSingularityRelativeTolerance"/>)
    /// used to reject a numerically near-singular matrix; the effective absolute threshold is this
    /// value multiplied by the matrix's largest entry. Must be finite and non-negative when supplied.
    /// </param>
    /// <returns>A new matrix representing the inverse of the current matrix.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the matrix is not square.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="relativeSingularityTolerance"/> is supplied but not finite or is negative.</exception>
    public Matrix<T> Invert(T? relativeSingularityTolerance = null)
    {
        if (!IsSquare)
        {
            throw new InvalidOperationException("The matrix is not square.");
        }
        if (relativeSingularityTolerance is { } explicitTolerance)
            ValidateTolerance(explicitTolerance, nameof(relativeSingularityTolerance));

        if (IsIdentity)
        {
            return new Matrix<T>(this);
        }

        int n = Rows;
        T[,] working = ToArray();
        T[,] inverse = new T[n, n];
        T pivotTolerance = MaxAbsoluteEntry(working) * (relativeSingularityTolerance ?? DefaultSingularityRelativeTolerance(n));

        for (int i = 0; i < n; i++)
        {
            inverse[i, i] = T.One;
        }

        for (int pivotIndex = 0; pivotIndex < n; pivotIndex++)
        {
            int pivotRow = pivotIndex;
            T pivotMagnitude = T.Abs(working[pivotRow, pivotIndex]);

            for (int row = pivotIndex + 1; row < n; row++)
            {
                T candidate = T.Abs(working[row, pivotIndex]);
                if (candidate > pivotMagnitude)
                {
                    pivotMagnitude = candidate;
                    pivotRow = row;
                }
            }

            if (pivotMagnitude <= pivotTolerance)
            {
                throw new InvalidOperationException("The matrix is singular or numerically near-singular and cannot be reliably inverted.");
            }

            if (pivotRow != pivotIndex)
            {
                PermuteRows(working, pivotIndex, pivotRow);
                PermuteRows(inverse, pivotIndex, pivotRow);
            }

            T pivot = working[pivotIndex, pivotIndex];
            for (int col = 0; col < n; col++)
            {
                working[pivotIndex, col] /= pivot;
                inverse[pivotIndex, col] /= pivot;
            }

            for (int row = 0; row < n; row++)
            {
                if (row == pivotIndex) continue;

                T factor = working[row, pivotIndex];
                for (int col = 0; col < n; col++)
                {
                    working[row, col] -= factor * working[pivotIndex, col];
                    inverse[row, col] -= factor * inverse[pivotIndex, col];
                }
            }
        }

        return new Matrix<T>(inverse, false, false, false, null);
    }
}
