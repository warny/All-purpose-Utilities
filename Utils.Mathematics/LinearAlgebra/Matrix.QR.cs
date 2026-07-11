using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// QR decomposition for the <see cref="Matrix{T}"/> type.
/// </summary>
public sealed partial class Matrix<T>
{
    /// <summary>
    /// Performs a QR decomposition of this matrix using Householder reflections.
    /// </summary>
    /// <remarks>
    /// For an m×n matrix A with m ≥ n, returns Q (m×n, orthonormal columns) and R (n×n, upper
    /// triangular) such that A = Q·R. Unlike a Gram-Schmidt-based decomposition, Householder
    /// reflections remain well-defined when a column is linearly dependent on the ones before it: a
    /// rank-deficient column simply produces a (numerically) zero diagonal entry in R at that step
    /// rather than requiring the decomposition to be rejected, which is what allows singular square
    /// matrices to reach <see cref="ComputeEigenvalues"/>.
    /// </remarks>
    /// <param name="rankTolerance">
    /// Overrides the default relative-plus-absolute tolerance (see <see cref="DefaultTolerance"/>)
    /// used to decide whether a column's remaining sub-diagonal component is already numerically
    /// zero (rank-deficient at that step). When supplied, the effective absolute threshold is this
    /// value multiplied by the matrix's largest entry. Must be finite and non-negative when supplied.
    /// </param>
    /// <returns>A tuple containing the orthogonal matrix Q and the upper-triangular matrix R.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the matrix has more columns than rows.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rankTolerance"/> is supplied but not finite or is negative.</exception>
    public (Matrix<T> Q, Matrix<T> R) DecomposeQR(T? rankTolerance = null)
    {
        if (Rows < Columns)
            throw new InvalidOperationException("QR decomposition requires Rows ≥ Columns.");
        if (rankTolerance is { } explicitRankTolerance)
            ValidateTolerance(explicitRankTolerance, nameof(rankTolerance));

        int m = Rows, n = Columns;
        T[,] r = ToArray();
        T[,] qFull = new T[m, m];
        for (int i = 0; i < m; i++) qFull[i, i] = T.One;

        // Scale-aware (rather than a hard-coded 1e-12 absolute literal, meaningless across
        // arbitrary IFloatingPoint<T> precision) tolerance for detecting a column that is already
        // numerically zero below the diagonal.
        T qrTolerance = rankTolerance is { } explicitQrTolerance
            ? MaxAbsoluteEntry(r) * explicitQrTolerance
            : DefaultTolerance(MaxAbsoluteEntry(r), m);

        int steps = Math.Min(m - 1, n);
        for (int k = 0; k < steps; k++)
        {
            T normX = T.Zero;
            for (int i = k; i < m; i++) normX += r[i, k] * r[i, k];
            normX = T.Sqrt(normX);

            // The sub-column below the diagonal is already (numerically) zero: either genuinely
            // rank-deficient at this step, or already aligned with the target axis. Either way,
            // there is nothing to reflect, and R's diagonal entry here is correctly ~zero.
            if (normX <= qrTolerance)
                continue;

            T alpha = r[k, k] >= T.Zero ? -normX : normX;

            int len = m - k;
            T[] v = new T[len];
            for (int i = 0; i < len; i++) v[i] = r[k + i, k];
            v[0] -= alpha;

            T normV = T.Zero;
            for (int i = 0; i < len; i++) normV += v[i] * v[i];
            normV = T.Sqrt(normV);
            if (normV <= qrTolerance)
                continue;
            for (int i = 0; i < len; i++) v[i] /= normV;

            // Apply the reflection H = I - 2vv^T to R, restricted to rows k..m-1 and columns k..n-1.
            for (int col = k; col < n; col++)
            {
                T dot = T.Zero;
                for (int i = 0; i < len; i++) dot += v[i] * r[k + i, col];
                T scale = dot + dot;
                for (int i = 0; i < len; i++) r[k + i, col] -= scale * v[i];
            }

            // Accumulate the same reflection into Q (applied to columns k..m-1, every row).
            for (int row = 0; row < m; row++)
            {
                T dot = T.Zero;
                for (int i = 0; i < len; i++) dot += qFull[row, k + i] * v[i];
                T scale = dot + dot;
                for (int i = 0; i < len; i++) qFull[row, k + i] -= scale * v[i];
            }
        }

        // Extract the thin (economy) form: the first n columns of Q and the n x n upper-triangular R.
        T[,] q = new T[m, n];
        for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
                q[i, j] = qFull[i, j];

        T[,] rThin = new T[n, n];
        for (int i = 0; i < n; i++)
            for (int j = i; j < n; j++)
                rThin[i, j] = r[i, j];

        return (new Matrix<T>(q), new Matrix<T>(rThin));
    }
}
