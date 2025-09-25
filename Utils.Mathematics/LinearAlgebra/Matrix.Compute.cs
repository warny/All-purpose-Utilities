using System;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Provides advanced computations for the <see cref="Matrix{T}"/> type.
/// </summary>
public partial class Matrix<T>
{
        /// <summary>
        /// Applies a linear transformation to the specified row of an array-based matrix.
        /// </summary>
        /// <param name="matrix">Matrix to transform.</param>
        /// <param name="targetRow">Row index to replace.</param>
        /// <param name="transformations">Coefficients describing the transformation.</param>
        private static void ApplyLinearTransformation(T[,] matrix, int targetRow, T[] transformations)
        {
                if (transformations[targetRow] == T.Zero)
                {
                        throw new ArgumentOutOfRangeException(nameof(targetRow), $"The transformation of row {targetRow} cannot nullify its own value.");
                }

                int rows = matrix.GetLength(0);
                int cols = matrix.GetLength(1);
                T[] newRow = new T[cols];

                for (int col = 0; col < cols; col++)
                {
                        T temp = T.Zero;
                        for (int row = 0; row < rows; row++)
                        {
                                temp += matrix[row, col] * transformations[row];
                        }
                        newRow[col] = temp;
                }

                for (int col = 0; col < cols; col++)
                {
                        matrix[targetRow, col] = newRow[col];
                }
        }

        /// <summary>
        /// Swaps two rows of an array-based matrix in place.
        /// </summary>
        /// <param name="matrix">Matrix whose rows should be permuted.</param>
        /// <param name="row1">First row index.</param>
        /// <param name="row2">Second row index.</param>
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
        /// Performs LU decomposition of the current square matrix, resulting in a lower triangular matrix L
        /// and an upper triangular matrix U such that the original matrix equals L multiplied by U.
        /// </summary>
        /// <remarks>
        /// The decomposition uses the same sequence of elementary row operations as the original mutable implementation
        /// but operates entirely on local array copies to preserve immutability.
        /// </remarks>
        /// <returns>A tuple containing the lower and upper triangular matrices.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the matrix is not square or is singular.</exception>
        public (Matrix<T> L, Matrix<T> U) DiagonalizeLU()
        {
                if (!IsSquare)
                {
                        throw new InvalidOperationException("The matrix must be square for LU decomposition.");
                }

                int n = Rows;
                T[,] u = ToArray();
                T[,] l = new T[n, n];

                for (int i = 0; i < n; i++)
                {
                        l[i, i] = T.One;
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
                                PermuteRows(l, k, pivotRow, k);
                        }

                        T[] transformations = new T[n];
                        for (int row = k + 1; row < n; row++)
                        {
                                transformations[row] = T.One;
                                transformations[k] = -u[row, k] / u[k, k];

                                ApplyLinearTransformation(u, row, transformations);
                                ApplyLinearTransformation(l, row, transformations);

                                transformations[row] = T.Zero;
                                transformations[k] = T.Zero;
                        }
                }

                Matrix<T> L = new Matrix<T>(l, false, false, true, null);
                Matrix<T> U = new Matrix<T>(u, false, false, true, null);
                return (L, U);
        }

        /// <summary>
        /// Inverts the matrix if it is invertible.
        /// </summary>
        /// <returns>A new matrix representing the inverse of the current matrix.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the matrix is not square.</exception>
        public Matrix<T> Invert()
        {
                if (!IsSquare)
                {
                        throw new InvalidOperationException("The matrix is not square.");
                }

                if (IsIdentity)
                {
                        return new Matrix<T>(this);
                }

                int n = Rows;
                T[,] working = ToArray();
                T[,] inverse = new T[n, n];

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

                        if (working[pivotRow, pivotIndex].Equals(T.Zero))
                        {
                                throw new InvalidOperationException("The matrix is singular and cannot be inverted.");
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
