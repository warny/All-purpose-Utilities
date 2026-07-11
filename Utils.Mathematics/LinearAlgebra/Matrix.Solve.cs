using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Linear-system solver for the <see cref="Matrix{T}"/> type.
/// </summary>
public sealed partial class Matrix<T>
{
    /// <summary>
    /// Solves the linear system <c>A·x = b</c> and returns <c>x</c>.
    /// Uses Gaussian elimination with partial pivoting.
    /// </summary>
    /// <param name="b">Right-hand-side vector. Its dimension must equal the number of rows.</param>
    /// <param name="relativeSingularityTolerance">
    /// Overrides the default relative pivot tolerance (see <see cref="DefaultSingularityRelativeTolerance"/>)
    /// used to reject a numerically near-singular matrix; the effective absolute threshold is this
    /// value multiplied by the matrix's largest entry. Pass a smaller value to accept more
    /// ill-conditioned systems, or a larger value to reject them more aggressively. Must be finite
    /// and non-negative when supplied.
    /// </param>
    /// <returns>The solution vector <c>x</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the matrix is not square or is singular.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="b"/> has the wrong dimension.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="relativeSingularityTolerance"/> is supplied but not finite or is negative.</exception>
    public Vector<T> Solve(Vector<T> b, T? relativeSingularityTolerance = null)
    {
        if (!IsSquare)
            throw new InvalidOperationException("Only square matrices can be solved with this method.");
        if (b.Dimension != Rows)
            throw new ArgumentException($"Vector dimension {b.Dimension} does not match matrix row count {Rows}.", nameof(b));
        if (relativeSingularityTolerance is { } explicitTolerance)
            ValidateTolerance(explicitTolerance, nameof(relativeSingularityTolerance));

        int n = Rows;
        T[,] a = ToArray();
        T[] x = new T[n];
        for (int i = 0; i < n; i++) x[i] = b[i];

        T pivotTolerance = MaxAbsoluteEntry(a) * (relativeSingularityTolerance ?? DefaultSingularityRelativeTolerance(n));

        // Forward elimination with partial pivoting
        for (int col = 0; col < n; col++)
        {
            int pivotRow = col;
            for (int row = col + 1; row < n; row++)
                if (T.Abs(a[row, col]) > T.Abs(a[pivotRow, col]))
                    pivotRow = row;

            if (T.Abs(a[pivotRow, col]) <= pivotTolerance)
                throw new InvalidOperationException("Matrix is singular or numerically near-singular; the system has no reliable unique solution.");

            if (pivotRow != col)
            {
                for (int j = 0; j < n; j++) (a[col, j], a[pivotRow, j]) = (a[pivotRow, j], a[col, j]);
                (x[col], x[pivotRow]) = (x[pivotRow], x[col]);
            }

            for (int row = col + 1; row < n; row++)
            {
                T factor = a[row, col] / a[col, col];
                x[row] -= factor * x[col];
                for (int j = col; j < n; j++)
                    a[row, j] -= factor * a[col, j];
            }
        }

        // Back substitution
        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = i + 1; j < n; j++)
                x[i] -= a[i, j] * x[j];
            x[i] /= a[i, i];
        }

        return new Vector<T>(x);
    }
}
