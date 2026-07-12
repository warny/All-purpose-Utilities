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
    /// Overrides the default relative-plus-absolute pivot tolerance (see <see cref="DefaultTolerance"/>)
    /// used to reject a numerically near-singular matrix. When supplied, the effective absolute
    /// threshold is this value multiplied by the matrix's largest entry (no absolute floor is added,
    /// unlike the default); pass a smaller value to accept more ill-conditioned systems, or a larger
    /// value to reject them more aggressively. Must be finite and non-negative when supplied.
    /// </param>
    /// <returns>The solution vector <c>x</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the matrix is not square or is singular.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="b"/> has the wrong dimension.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="relativeSingularityTolerance"/> is supplied but not finite or is negative.</exception>
    /// <remarks>
    /// Delegates to the pivoted elimination shared with <see cref="DiagonalizeLU"/>,
    /// <see cref="Determinant"/>, and <see cref="Invert"/> (see TODO-2026-07-11-pass5.md item #69):
    /// <c>A·x = b</c> becomes <c>L·U·x = P·b</c> (since <c>P·A = L·U</c>), solved by forward
    /// substitution for <c>y</c> in <c>L·y = P·b</c> followed by back substitution for <c>x</c> in
    /// <c>U·x = y</c>.
    /// </remarks>
    public Vector<T> Solve(Vector<T> b, T? relativeSingularityTolerance = null)
    {
        if (!IsSquare)
            throw new InvalidOperationException("Only square matrices can be solved with this method.");
        if (b.Dimension != Rows)
            throw new ArgumentException($"Vector dimension {b.Dimension} does not match matrix row count {Rows}.", nameof(b));
        if (relativeSingularityTolerance is { } explicitTolerance)
            ValidateTolerance(explicitTolerance, nameof(relativeSingularityTolerance));

        if (!TryDecomposePivoted(relativeSingularityTolerance, out PivotedElimination decomposition))
        {
            throw new InvalidOperationException("Matrix is singular or numerically near-singular; the system has no reliable unique solution.");
        }

        int n = Rows;
        T[] permutedRightHandSide = new T[n];
        for (int i = 0; i < n; i++)
            permutedRightHandSide[i] = b[decomposition.Permutation[i]];

        return new Vector<T>(SolvePermuted(decomposition, permutedRightHandSide));
    }
}
