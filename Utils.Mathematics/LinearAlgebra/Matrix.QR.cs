using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// QR decomposition for the <see cref="Matrix{T}"/> type.
/// </summary>
public sealed partial class Matrix<T>
{
    private static readonly T QrEpsilon = T.CreateChecked(1e-12);

    /// <summary>
    /// Performs a QR decomposition of this matrix using the modified Gram–Schmidt algorithm.
    /// </summary>
    /// <remarks>
    /// For an m×n matrix A with m ≥ n and full column rank, returns Q (m×n, orthonormal columns)
    /// and R (n×n, upper triangular) such that A = Q·R.
    /// </remarks>
    /// <returns>A tuple containing the orthogonal matrix Q and the upper-triangular matrix R.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the matrix has more columns than rows, or when its columns are not linearly independent.
    /// </exception>
    public (Matrix<T> Q, Matrix<T> R) DecomposeQR()
    {
        if (Rows < Columns)
            throw new InvalidOperationException("QR decomposition requires Rows ≥ Columns.");

        int m = Rows, n = Columns;
        T[,] q = new T[m, n];
        T[,] r = new T[n, n];
        T[,] a = ToArray();

        for (int j = 0; j < n; j++)
        {
            // Copy column j of A into q[:,j]
            for (int i = 0; i < m; i++) q[i, j] = a[i, j];

            // Subtract projections onto already-computed orthonormal columns
            for (int k = 0; k < j; k++)
            {
                T dot = T.Zero;
                for (int i = 0; i < m; i++) dot += q[i, k] * q[i, j];
                r[k, j] = dot;
                for (int i = 0; i < m; i++) q[i, j] -= dot * q[i, k];
            }

            // Normalise
            T norm = T.Zero;
            for (int i = 0; i < m; i++) norm += q[i, j] * q[i, j];
            norm = T.Sqrt(norm);

            if (norm <= QrEpsilon)
                throw new InvalidOperationException(
                    "Matrix columns are linearly dependent; QR decomposition requires full column rank.");

            r[j, j] = norm;
            for (int i = 0; i < m; i++) q[i, j] /= norm;
        }

        return (new Matrix<T>(q), new Matrix<T>(r));
    }
}
