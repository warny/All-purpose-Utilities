using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Eigenvalue decomposition for the <see cref="Matrix{T}"/> type.
/// </summary>
public sealed partial class Matrix<T>
{
    /// <summary>
    /// Computes the real eigenvalues and corresponding eigenvectors of a symmetric matrix
    /// using the QR iteration algorithm.
    /// </summary>
    /// <remarks>
    /// Only real symmetric matrices are supported; all eigenvalues of such matrices are guaranteed real.
    /// Eigenvalues are returned in descending order of absolute value.
    /// <para>
    /// Uses the symmetric QR algorithm with a Wilkinson shift and deflation: each step shifts the
    /// active (not-yet-deflated) leading submatrix by an estimate of the eigenvalue closest to its
    /// trailing corner before applying <c>A ← R·Q</c>, which - unlike plain unshifted iteration -
    /// gives locally cubic convergence and remains fast even for closely-spaced (clustered)
    /// eigenvalues. Once the last row/column of the active submatrix decouples (goes to zero within
    /// tolerance), that eigenvalue is locked in and the active submatrix shrinks by one, so later
    /// eigenvalues do not have to pay for the cost of resolving earlier ones.
    /// </para>
    /// <para>
    /// <b>Known limitation:</b> unlike a textbook implementation, this does not first reduce the
    /// matrix to tridiagonal form, so each QR step still costs <c>O(m³)</c> for the current active
    /// size <c>m</c> (via <see cref="DecomposeQR(T?)"/>) rather than the <c>O(m)</c> a tridiagonal
    /// reduction would allow. This affects performance, not correctness or convergence rate: the
    /// shift is still computed from (and applied to) the true active submatrix at each step.
    /// </para>
    /// </remarks>
    /// <param name="maxIterations">Maximum number of QR iterations before giving up. Must be greater than zero.</param>
    /// <param name="convergenceTolerance">
    /// Overrides the default relative-plus-absolute convergence tolerance (see
    /// <see cref="DefaultTolerance"/>) used to decide when the off-diagonal magnitude is small
    /// enough to stop iterating. When supplied, the effective absolute threshold is this value
    /// multiplied by the matrix's largest entry. Independently configurable from
    /// <paramref name="symmetryTolerance"/> and <paramref name="rankTolerance"/>. Must be finite and
    /// non-negative when supplied.
    /// </param>
    /// <param name="symmetryTolerance">
    /// Overrides the default tolerance (see <see cref="DefaultTolerance"/>) used by the upfront
    /// <see cref="IsSymmetric(T?)"/> input-validation check. Forwarded directly to
    /// <see cref="IsSymmetric(T?)"/>; independently configurable from
    /// <paramref name="convergenceTolerance"/> and <paramref name="rankTolerance"/>. Must be finite
    /// and non-negative when supplied.
    /// </param>
    /// <param name="rankTolerance">
    /// Overrides the default tolerance (see <see cref="DefaultTolerance"/>) used by each QR
    /// iteration's internal <see cref="DecomposeQR(T?)"/> call to decide whether a column is already
    /// numerically rank-deficient. Forwarded directly to <see cref="DecomposeQR(T?)"/> at every
    /// iteration; independently configurable from <paramref name="convergenceTolerance"/> and
    /// <paramref name="symmetryTolerance"/>. Must be finite and non-negative when supplied.
    /// </param>
    /// <returns>
    /// A tuple containing an array of eigenvalues (descending by magnitude) and a matrix whose
    /// columns are the corresponding eigenvectors.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxIterations"/> is not positive, or any of
    /// <paramref name="convergenceTolerance"/>, <paramref name="symmetryTolerance"/>,
    /// <paramref name="rankTolerance"/> is supplied but not finite or is negative.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the matrix is not square, not symmetric, or fails to converge.
    /// </exception>
    public (T[] Eigenvalues, Matrix<T> Eigenvectors) ComputeEigenvalues(int maxIterations = 1000, T? convergenceTolerance = null, T? symmetryTolerance = null, T? rankTolerance = null)
    {
        if (maxIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxIterations), maxIterations, "Must be greater than zero.");
        if (convergenceTolerance is { } explicitConvergenceTolerance)
            ValidateTolerance(explicitConvergenceTolerance, nameof(convergenceTolerance));
        if (symmetryTolerance is { } explicitSymmetryToleranceArg)
            ValidateTolerance(explicitSymmetryToleranceArg, nameof(symmetryTolerance));
        if (rankTolerance is { } explicitRankToleranceArg)
            ValidateTolerance(explicitRankToleranceArg, nameof(rankTolerance));
        if (!IsSquare)
            throw new InvalidOperationException("Eigenvalue decomposition requires a square matrix.");

        int n = Rows;
        if (!IsSymmetric(symmetryTolerance))
            throw new InvalidOperationException("This implementation only supports real symmetric matrices.");

        // Working copy for QR iteration
        T[,] a = ToArray();

        // Scale-aware (rather than a hard-coded 1e-10 absolute literal) convergence tolerance,
        // fixed at the outset from the original matrix's magnitude: similarity transformations
        // (A <- Q^T A Q at each step) preserve the Frobenius norm, so the initial scale remains a
        // valid reference throughout the iteration.
        T effectiveConvergenceTolerance = convergenceTolerance is { } explicitTolerance
            ? MaxAbsoluteEntry(a) * explicitTolerance
            : DefaultTolerance(MaxAbsoluteEntry(a), n);

        // Accumulate eigenvectors: V = Q_0 · Q_1 · …
        T[,] v = new T[n, n];
        for (int i = 0; i < n; i++) v[i, i] = T.One;

        // Active-submatrix size: the leading m x m block of `a` still under iteration. Once the last
        // row/column of that block decouples (its off-diagonal entries vanish within tolerance), the
        // corresponding eigenvalue is already correct on the diagonal and the active size shrinks by
        // one, so later (already-resolved) eigenvalues never have to be revisited.
        int m = n;
        int totalIterations = 0;
        while (m > 1)
        {
            bool deflated = false;
            for (; totalIterations < maxIterations; totalIterations++)
            {
                if (LastRowOffDiagonalNorm(a, m) <= effectiveConvergenceTolerance)
                {
                    deflated = true;
                    break;
                }

                // Wilkinson shift: an estimate of the eigenvalue of the trailing 2x2 block closest to
                // its bottom-right corner. Shifting by (an estimate of) an eigenvalue makes A - shift*I
                // nearly singular, which drives Householder QR to decouple the last row/column far
                // faster than unshifted iteration - typically within a handful of steps per
                // deflation, instead of a number of steps governed by how close consecutive
                // eigenvalues are to each other.
                T shift = WilkinsonShift(a, m);

                for (int i = 0; i < m; i++) a[i, i] -= shift;

                var (q, r) = new Matrix<T>(ExtractLeadingBlock(a, m)).DecomposeQR(rankTolerance);
                T[,] qArray = q.ToArray();

                // A_active ← R·Q + shift·I  (this is the shifted analogue of A_{k+1} = R_k · Q_k;
                // adding the shift back preserves the eigenvalues of the *unshifted* active block).
                WriteLeadingBlock(a, m, (r * q).ToArray());
                for (int i = 0; i < m; i++) a[i, i] += shift;

                // V[:, 0:m) ← V[:, 0:m) · Q  (columns m..n-1 are untouched: Q is block-diagonal with
                // an implicit identity outside the active block).
                T[,] newV = new T[n, n];
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < m; j++)
                    {
                        T sum = T.Zero;
                        for (int k = 0; k < m; k++) sum += v[i, k] * qArray[k, j];
                        newV[i, j] = sum;
                    }
                    for (int j = m; j < n; j++) newV[i, j] = v[i, j];
                }
                v = newV;
            }

            if (!deflated)
                throw new InvalidOperationException(
                    $"QR iteration did not converge after {maxIterations} iterations.");

            m--;
        }

        // Extract eigenvalues from the diagonal
        T[] eigenvalues = new T[n];
        for (int i = 0; i < n; i++) eigenvalues[i] = a[i, i];

        // Sort by descending absolute value and reorder eigenvectors accordingly
        int[] order = Enumerable.Range(0, n)
            .OrderByDescending(i => T.Abs(eigenvalues[i]))
            .ToArray();

        T[] sortedValues = new T[n];
        T[,] sortedVectors = new T[n, n];
        for (int j = 0; j < n; j++)
        {
            sortedValues[j] = eigenvalues[order[j]];
            for (int i = 0; i < n; i++)
                sortedVectors[i, j] = v[i, order[j]];
        }

        return (sortedValues, new Matrix<T>(sortedVectors));
    }

    /// <summary>
    /// Returns <see langword="true"/> when this square matrix is symmetric (A[i,j] == A[j,i]) within
    /// the default relative-plus-absolute tolerance (see <see cref="DefaultTolerance"/>).
    /// </summary>
    public bool IsSymmetric() => IsSymmetric(null);

    /// <summary>
    /// Returns <see langword="true"/> when this square matrix is symmetric (A[i,j] == A[j,i]) within
    /// <paramref name="symmetryTolerance"/>.
    /// </summary>
    /// <param name="symmetryTolerance">
    /// Overrides the default relative-plus-absolute tolerance (see <see cref="DefaultTolerance"/>)
    /// used to compare A[i,j] to A[j,i]. When supplied, the effective absolute threshold is this
    /// value multiplied by the matrix's largest entry. This is independent of
    /// <see cref="ComputeEigenvalues"/>'s (separately configurable) convergence tolerance. Must be
    /// finite and non-negative when supplied.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="symmetryTolerance"/> is supplied but not finite or is negative.</exception>
    public bool IsSymmetric(T? symmetryTolerance)
    {
        if (!IsSquare) return false;
        if (symmetryTolerance is { } explicitSymmetryTolerance)
            ValidateTolerance(explicitSymmetryTolerance, nameof(symmetryTolerance));

        T[,] array = ToArray();
        T tolerance = symmetryTolerance is { } explicitTolerance
            ? MaxAbsoluteEntry(array) * explicitTolerance
            : DefaultTolerance(MaxAbsoluteEntry(array), Rows);
        for (int i = 0; i < Rows; i++)
            for (int j = i + 1; j < Columns; j++)
                if (T.Abs(array[i, j] - array[j, i]) > tolerance) return false;
        return true;
    }

    /// <summary>
    /// Norm of the off-diagonal entries of the last row of the leading <paramref name="m"/> x
    /// <paramref name="m"/> active submatrix (equivalently, by symmetry, its last column). Used as the
    /// deflation test: once this vanishes within tolerance, the active block's last row/column has
    /// decoupled from the rest and <c>a[m-1, m-1]</c> is already a converged eigenvalue.
    /// </summary>
    private static T LastRowOffDiagonalNorm(T[,] a, int m)
    {
        T sum = T.Zero;
        for (int j = 0; j < m - 1; j++) sum += a[m - 1, j] * a[m - 1, j];
        return T.Sqrt(sum);
    }

    /// <summary>
    /// Computes the Wilkinson shift for the active <paramref name="m"/> x <paramref name="m"/>
    /// submatrix: the eigenvalue of its trailing 2x2 block closest to the block's bottom-right entry.
    /// Shifting by (an estimate of) an eigenvalue of the active block drives that block's QR iteration
    /// to converge locally cubically instead of only linearly.
    /// </summary>
    private static T WilkinsonShift(T[,] a, int m)
    {
        if (m < 2) return a[m - 1, m - 1];

        T two = T.One + T.One;
        T a11 = a[m - 2, m - 2];
        T a12 = a[m - 2, m - 1];
        T a22 = a[m - 1, m - 1];
        T delta = (a11 - a22) / two;
        T sign = delta >= T.Zero ? T.One : -T.One;
        T denom = delta + sign * T.Sqrt(delta * delta + a12 * a12);
        // denom is zero only when delta and a12 are both already zero, i.e. the trailing 2x2 block is
        // already diagonal (a11 == a22, no coupling) - any shift works then, so fall back to a22.
        return denom == T.Zero ? a22 : a22 - (a12 * a12) / denom;
    }

    /// <summary>Copies the leading <paramref name="size"/> x <paramref name="size"/> block of <paramref name="source"/>.</summary>
    private static T[,] ExtractLeadingBlock(T[,] source, int size)
    {
        T[,] block = new T[size, size];
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                block[i, j] = source[i, j];
        return block;
    }

    /// <summary>Writes <paramref name="block"/> back into the leading <paramref name="size"/> x <paramref name="size"/> region of <paramref name="destination"/>.</summary>
    private static void WriteLeadingBlock(T[,] destination, int size, T[,] block)
    {
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                destination[i, j] = block[i, j];
    }
}
