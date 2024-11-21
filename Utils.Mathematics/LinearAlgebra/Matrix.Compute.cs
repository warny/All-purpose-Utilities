using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.Objects;

namespace Utils.Mathematics.LinearAlgebra;

public partial class Matrix<T>
{
	/// <summary>
	/// Applies a linear transformation to the specified row of the matrix using the given transformation coefficients.
	/// </summary>
	private static void ApplyLinearTransformation(int targetRow, T[] transformations, params Matrix<T>[] matrices)
	{
		if (transformations[targetRow] == T.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(targetRow), $"The transformation of row {targetRow} cannot nullify its own value.");
		}

		foreach (Matrix<T> matrix in matrices)
		{
			int rows = matrix.Rows;
			int cols = matrix.Columns;

			for (int col = 0; col < cols; col++)
			{
				T temp = T.Zero;
				for (int row = 0; row < rows; row++)
				{
					temp += matrix[row, col] * transformations[row];
				}
				matrix.components[targetRow, col] = temp;
			}

			matrix.ResetMatrixProperties();
		}
	}

	/// <summary>
	/// Swaps two rows of the matrices in the given collection.
	/// </summary>
	private static void PermuteTransformation(int row1, int row2, params Matrix<T>[] matrices)
	{
		if (row1 == row2) return;

		foreach (Matrix<T> matrix in matrices)
		{
			int cols = matrix.Columns;

			for (int col = 0; col < cols; col++)
			{
				T temp = matrix[row1, col];
				matrix.components[row1, col] = matrix[row2, col];
				matrix.components[row2, col] = temp;
			}

			matrix.ResetMatrixProperties();
		}
	}

	/// <summary>
	/// Resets the properties related to the matrix's structure.
	/// </summary>
	private void ResetMatrixProperties()
	{
		bool isSquare = IsSquare;
		isDiagonalized = isSquare ? null : false;
		isTriangularised = isSquare ? null : false;
		isIdentity = isSquare ? null : false;
	}

	/// <summary>
	/// Performs LU decomposition of the current square matrix, resulting in a lower triangular matrix L
	/// and an upper triangular matrix U such that the original matrix A = L * U.
	/// </summary>
	/// <remarks>
	/// LU decomposition is used to decompose a given square matrix into two triangular matrices:
	/// L (lower triangular) and U (upper triangular). This decomposition is useful for solving linear
	/// systems, calculating determinants, and matrix inversion.
	/// </remarks>
	/// <returns>
	/// A tuple containing:
	/// <list type="bullet">
	/// <item>
	/// <description><see cref="Matrix{T}"/> L - The lower triangular matrix with ones on the diagonal.</description>
	/// </item>
	/// <item>
	/// <description><see cref="Matrix{T}"/> U - The upper triangular matrix.</description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown if the matrix is not square, as LU decomposition requires a square matrix.
	/// Thrown if the matrix is singular, meaning that it cannot be decomposed due to the presence of a zero pivot.
	/// </exception>
	public (Matrix<T> L, Matrix<T> U) DiagonalizeLU()
	{
		// Verify that the matrix is square
		if (!IsSquare)
		{
			throw new InvalidOperationException("The matrix must be square for LU decomposition.");
		}

		int n = Rows;

		// Create L as an identity matrix and U as a clone of the current matrix
		Matrix<T> L = Matrix<T>.Identity(n);
		Matrix<T> U = new Matrix<T>(this); // Cloning the original matrix

		T sign = T.One;

		for (int k = 0; k < n; k++)
		{
			// Partial pivoting: find the row with the largest pivot element
			int pivotRow = k;
			for (int i = k + 1; i < n; i++)
			{
				if (T.Abs(U[i, k]) > T.Abs(U[pivotRow, k]))
				{
					pivotRow = i;
				}
			}

			// If the pivot element is zero, the matrix is singular
			if (U[pivotRow, k].Equals(T.Zero))
			{
				throw new InvalidOperationException("The matrix is singular and cannot be decomposed.");
			}

			// Swap rows if needed (operating on matrices in the collection)
			if (pivotRow != k)
			{
				PermuteTransformation(k, pivotRow, U, L);
				sign = -sign; // Change the sign of the determinant due to row swapping
			}

			// Apply linear transformations to eliminate elements below the pivot
			T[] transformations = Enumerable.Repeat(T.Zero, Rows).ToArray();
			for (int row = k + 1; row < n; row++)
			{
				// Set the transformation coefficient for the target row to eliminate elements below pivot
				transformations[row] = T.One;
				transformations[k] = -U[row, k] / U[k, k];

				ApplyLinearTransformation(row, transformations, U, L);

				// Reset the transformation coefficient for the next iteration
				transformations[row] = T.Zero;
			}
		}

		// Return the matrices L and U
		return (L, U);
	}

	/// <summary>
	/// Inverts the matrix if it is invertible.
	/// </summary>
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

		var (start, result) = DiagonalizeLU();

		T[] transformations = Enumerable.Repeat(T.Zero, Rows).ToArray();
		for (int j = Rows - 1; j >= 0; j--)
		{
			transformations[j] = T.One / start[j, j];
			ApplyLinearTransformation(j, transformations, start, result);
			transformations[j] = T.One;

			for (int i = j + 1; i < Rows; i++)
			{
				transformations[i] = -start[j, i] / start[i, i];
			}
			ApplyLinearTransformation(j, transformations, start, result);
		}
		return result;
	}

	/// <summary>
	/// Helper class to manage columns for determinant computation.
	/// </summary>
	private class ComputeColumns<T> : IEnumerable, IEquatable<ComputeColumns<T>>
		where T : struct,
		IFloatingPoint<T>,
		IAdditionOperators<T, T, T>,
		ISubtractionOperators<T, T, T>,
		IMultiplyOperators<T, T, T>,
		IDivisionOperators<T, T, T>,
		IEquatable<T>,
		IEqualityOperators<T, T, bool>
	{
		public int ColumnsCount { get; }
		private readonly int[] columns;
		private readonly Dictionary<ComputeColumns<T>, T> cache;

		public int Level => columns.Length;

		public ComputeColumns(int columnsCount)
		{
			columns = Enumerable.Range(0, columnsCount).ToArray();
			cache = new Dictionary<ComputeColumns<T>, T>();
			ColumnsCount = columnsCount;
		}

		private ComputeColumns(int[] columns, Dictionary<ComputeColumns<T>, T> cache, int columnsCount)
		{
			this.columns = columns;
			this.cache = cache;
			ColumnsCount = columnsCount;
		}

		public ComputeColumns<T> NextLevel(int columnToRemove)
		{
			return new ComputeColumns<T>(columns.Where(c => c != columnToRemove).ToArray(), cache, ColumnsCount);
		}

		public bool TryGetCache(out T value) => cache.TryGetValue(this, out value);

		public void SetCache(T value) => cache[this] = value;

		public IEnumerator GetEnumerator() => columns.GetEnumerator();

		public override int GetHashCode() => ObjectUtils.ComputeHash(columns);

		public override bool Equals(object? obj) => obj is ComputeColumns<T> other && Equals(other);

		public bool Equals(ComputeColumns<T>? other) => other is not null && (ReferenceEquals(this, other) || Enumerable.SequenceEqual(columns, other.columns));

		public override string ToString() => string.Join(", ", columns);
	}
}
