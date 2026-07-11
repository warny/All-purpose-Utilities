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
    /// </remarks>
    /// <param name="maxIterations">Maximum number of QR iterations before giving up. Must be greater than zero.</param>
    /// <param name="convergenceTolerance">
    /// Overrides the default relative-plus-absolute convergence tolerance (see
    /// <see cref="DefaultTolerance"/>) used to decide when the off-diagonal magnitude is small
    /// enough to stop iterating. When supplied, the effective absolute threshold is this value
    /// multiplied by the matrix's largest entry. This is a threshold on the QR-iteration's own
    /// convergence and is independent of <see cref="IsSymmetric()"/>'s (separately configurable)
    /// input-validation tolerance. Must be finite and non-negative when supplied.
    /// </param>
    /// <returns>
    /// A tuple containing an array of eigenvalues (descending by magnitude) and a matrix whose
    /// columns are the corresponding eigenvectors.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxIterations"/> is not positive, or <paramref name="convergenceTolerance"/> is supplied but not finite or is negative.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the matrix is not square, not symmetric, or fails to converge.
    /// </exception>
    public (T[] Eigenvalues, Matrix<T> Eigenvectors) ComputeEigenvalues(int maxIterations = 1000, T? convergenceTolerance = null)
    {
        if (maxIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxIterations), maxIterations, "Must be greater than zero.");
        if (convergenceTolerance is { } explicitConvergenceTolerance)
            ValidateTolerance(explicitConvergenceTolerance, nameof(convergenceTolerance));
        if (!IsSquare)
            throw new InvalidOperationException("Eigenvalue decomposition requires a square matrix.");

        int n = Rows;
        if (!IsSymmetric())
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

        for (int iter = 0; iter < maxIterations; iter++)
        {
            if (OffDiagonalNorm(a, n) <= effectiveConvergenceTolerance) break;

            // QR decompose the current A
            var (q, r) = new Matrix<T>(a).DecomposeQR();

            // A ← R·Q  (this is A_{k+1} = R_k · Q_k)
            a = (r * q).ToArray();

            // V ← V·Q
            T[,] newV = new T[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    T sum = T.Zero;
                    for (int k = 0; k < n; k++) sum += v[i, k] * q[k, j];
                    newV[i, j] = sum;
                }
            v = newV;
        }

        // Check convergence independently of loop-entry/last-iteration conditions, rather than
        // only inside the loop's final pass: that tied the check to iter == maxIterations - 1,
        // which never ran at all for maxIterations <= 0 (now rejected above regardless), and made
        // the check easy to accidentally skip if the loop's control flow changed.
        if (OffDiagonalNorm(a, n) > effectiveConvergenceTolerance)
            throw new InvalidOperationException(
                $"QR iteration did not converge after {maxIterations} iterations.");

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

    /// <summary>Frobenius norm of strictly off-diagonal elements of the working array.</summary>
    private static T OffDiagonalNorm(T[,] a, int n)
    {
        T sum = T.Zero;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (i != j) sum += a[i, j] * a[i, j];
        return T.Sqrt(sum);
    }
}
