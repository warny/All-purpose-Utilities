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
    /// Result of <see cref="TryDecomposePivoted"/>: a pivoted Gauss elimination of a square matrix
    /// <c>A</c> such that <c>P·A = L·U</c>, where <c>P</c> is the permutation matrix implied by
    /// <see cref="Permutation"/> (row <c>i</c> of <c>P·A</c> is row <see cref="Permutation"/><c>[i]</c>
    /// of <c>A</c>).
    /// </summary>
    /// <param name="L">Lower unitriangular factor (unit diagonal, elimination multipliers below it).</param>
    /// <param name="U">Upper triangular factor.</param>
    /// <param name="Permutation">Row permutation applied to <c>A</c> before elimination.</param>
    /// <param name="Swaps">
    /// Number of row transpositions actually performed; <c>(-1)^Swaps</c> is the determinant of the
    /// implied permutation matrix <c>P</c>.
    /// </param>
    private readonly record struct PivotedElimination(T[,] L, T[,] U, int[] Permutation, int Swaps);

    /// <summary>
    /// Performs the pivoted Gauss elimination shared by <see cref="DiagonalizeLU"/>,
    /// <see cref="ComputeDeterminant"/>, <see cref="Solve"/>, and <see cref="Invert"/>, so pivot
    /// selection, tolerance policy, and numerical behavior cannot silently drift between these four
    /// operations the way four independent reimplementations previously could (see
    /// TODO-2026-07-11-pass5.md item #69). Uses partial pivoting (largest-magnitude pivot in each
    /// column) and stores the elimination multipliers directly in <c>L</c>, rather than as a
    /// by-product of applying the elimination row operations to an identity matrix. Operates entirely
    /// on local array copies to preserve immutability.
    /// </summary>
    /// <param name="relativeSingularityTolerance">
    /// Overrides the default relative-plus-absolute pivot tolerance (see <see cref="DefaultTolerance"/>)
    /// used to reject a numerically near-singular matrix. When supplied, the effective absolute
    /// threshold is this value multiplied by the matrix's largest entry (no absolute floor is added,
    /// unlike the default).
    /// </param>
    /// <param name="decomposition">The computed decomposition, or <c>default</c> when this method returns <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the matrix could be decomposed; <see langword="false"/> when a pivot
    /// column's largest available magnitude does not exceed the singularity tolerance (the matrix is
    /// singular or numerically near-singular). Returning a sentinel rather than throwing lets
    /// <see cref="ComputeDeterminant"/> report the mathematically well-defined zero determinant for a
    /// singular matrix without using an exception for ordinary control flow.
    /// </returns>
    private bool TryDecomposePivoted(T? relativeSingularityTolerance, out PivotedElimination decomposition)
    {
        int n = Rows;
        T[,] u = ToArray();
        T[,] l = new T[n, n];
        int[] permutation = new int[n];

        for (int i = 0; i < n; i++)
        {
            l[i, i] = T.One;
            permutation[i] = i;
        }

        T pivotTolerance = relativeSingularityTolerance is { } explicitTolerance
            ? MaxAbsoluteEntry(u) * explicitTolerance
            : DefaultTolerance(MaxAbsoluteEntry(u), n);

        int swaps = 0;
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

            if (T.Abs(u[pivotRow, k]) <= pivotTolerance)
            {
                decomposition = default;
                return false;
            }

            if (pivotRow != k)
            {
                PermuteRows(u, k, pivotRow);
                // Only the already-computed multiplier columns (0..k-1) need to move with the row;
                // column k onward is either not yet computed or the identity diagonal being formed.
                PermuteRows(l, k, pivotRow, k);
                (permutation[k], permutation[pivotRow]) = (permutation[pivotRow], permutation[k]);
                swaps++;
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

        decomposition = new PivotedElimination(l, u, permutation, swaps);
        return true;
    }

    /// <summary>
    /// Solves <c>L·y = P·b</c> by forward substitution (<c>L</c> has a unit diagonal, so no division is
    /// needed on the diagonal step) followed by <c>U·x = y</c> by back substitution, reusing an already
    /// computed <see cref="PivotedElimination"/>. Shared by <see cref="Solve"/> and <see cref="Invert"/>
    /// (which calls this once per column of the identity matrix).
    /// </summary>
    /// <param name="decomposition">A decomposition of the current matrix from <see cref="TryDecomposePivoted"/>.</param>
    /// <param name="permutedRightHandSide">
    /// The right-hand side already permuted according to <see cref="PivotedElimination.Permutation"/>,
    /// i.e. <c>P·b</c> (index <c>i</c> holds <c>b[Permutation[i]]</c>).
    /// </param>
    /// <returns>The solution <c>x</c> of <c>A·x = b</c>.</returns>
    private static T[] SolvePermuted(PivotedElimination decomposition, T[] permutedRightHandSide)
    {
        int n = permutedRightHandSide.Length;
        T[] y = new T[n];
        for (int i = 0; i < n; i++)
        {
            T sum = permutedRightHandSide[i];
            for (int j = 0; j < i; j++)
                sum -= decomposition.L[i, j] * y[j];
            y[i] = sum;
        }

        T[] x = new T[n];
        for (int i = n - 1; i >= 0; i--)
        {
            T sum = y[i];
            for (int j = i + 1; j < n; j++)
                sum -= decomposition.U[i, j] * x[j];
            x[i] = sum / decomposition.U[i, i];
        }

        return x;
    }

    /// <summary>
    /// Performs a pivoted LU decomposition of the current square matrix: a lower unitriangular
    /// matrix L, an upper triangular matrix U, and a permutation matrix P such that P * A = L * U,
    /// where A is the current matrix.
    /// </summary>
    /// <param name="relativeSingularityTolerance">
    /// Overrides the default relative-plus-absolute pivot tolerance (see <see cref="DefaultTolerance"/>)
    /// used to reject a numerically near-singular matrix; see <see cref="TryDecomposePivoted"/>. Must be
    /// finite and non-negative when supplied.
    /// </param>
    /// <remarks>
    /// Delegates to the pivoted elimination shared with <see cref="ComputeDeterminant"/>,
    /// <see cref="Solve"/>, and <see cref="Invert"/> (see TODO-2026-07-11-pass5.md item #69).
    /// Structural metadata on the returned matrices reflects only what is mathematically guaranteed for
    /// every input: <c>L</c>'s unit diagonal makes it always triangular with determinant one, and
    /// <c>U</c> is always triangular; whether <c>L</c>, <c>U</c>, or <c>P</c> also happen to be diagonal
    /// or the identity (e.g. when no pivoting was needed, or the source was already triangular) is left
    /// to lazy recomputation rather than hardcoded as false (see item #61/#68). <c>P</c>'s determinant
    /// (the sign of the permutation) is known directly from the elimination's swap count, so it is
    /// supplied rather than left for recomputation.
    /// </remarks>
    /// <returns>A tuple containing the lower-triangular, upper-triangular, and permutation matrices.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the matrix is not square or is singular.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="relativeSingularityTolerance"/> is supplied but not finite or is negative.</exception>
    public (Matrix<T> L, Matrix<T> U, Matrix<T> P) DiagonalizeLU(T? relativeSingularityTolerance = null)
    {
        if (!IsSquare)
        {
            throw new InvalidOperationException("The matrix must be square for LU decomposition.");
        }
        if (relativeSingularityTolerance is { } explicitTolerance)
            ValidateTolerance(explicitTolerance, nameof(relativeSingularityTolerance));

        if (!TryDecomposePivoted(relativeSingularityTolerance, out PivotedElimination decomposition))
        {
            throw new InvalidOperationException("The matrix is singular and cannot be decomposed.");
        }

        int n = Rows;
        T[,] p = new T[n, n];
        for (int i = 0; i < n; i++)
        {
            p[i, decomposition.Permutation[i]] = T.One;
        }

        T permutationDeterminant = decomposition.Swaps % 2 == 0 ? T.One : -T.One;
        Matrix<T> L = new Matrix<T>(decomposition.L, null, true, null, T.One);
        Matrix<T> U = new Matrix<T>(decomposition.U, null, true, null, null);
        Matrix<T> P = new Matrix<T>(p, null, null, null, permutationDeterminant);
        return (L, U, P);
    }

    /// <summary>
    /// Computes the determinant using the shared pivoted elimination (see
    /// <see cref="TryDecomposePivoted"/> and TODO-2026-07-11-pass5.md item #69), returning the
    /// mathematically well-defined zero determinant for a singular or numerically near-singular matrix
    /// instead of throwing - determinant is defined for every square matrix, unlike decomposition/solve/
    /// inversion, which genuinely have no answer to give in that case.
    /// </summary>
    private T ComputeDeterminant()
    {
        // A pivot merely close to zero (not exactly zero) would otherwise amplify rounding error into a
        // huge/infinite/NaN result instead of the mathematically expected near-zero determinant of a
        // near-singular matrix - see the tolerance rationale on TryDecomposePivoted/DefaultTolerance.
        if (!TryDecomposePivoted(relativeSingularityTolerance: null, out PivotedElimination decomposition))
        {
            return T.Zero;
        }

        T det = decomposition.Swaps % 2 == 0 ? T.One : -T.One;
        int n = Rows;
        for (int i = 0; i < n; i++)
            det *= decomposition.U[i, i];
        return det;
    }

    /// <summary>
    /// Inverts the matrix if it is invertible.
    /// </summary>
    /// <param name="relativeSingularityTolerance">
    /// Overrides the default relative-plus-absolute pivot tolerance (see <see cref="DefaultTolerance"/>)
    /// used to reject a numerically near-singular matrix. When supplied, the effective absolute
    /// threshold is this value multiplied by the matrix's largest entry (no absolute floor is added,
    /// unlike the default). Must be finite and non-negative when supplied.
    /// </param>
    /// <returns>A new matrix representing the inverse of the current matrix.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the matrix is not square.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="relativeSingularityTolerance"/> is supplied but not finite or is negative.</exception>
    /// <remarks>
    /// Delegates to the pivoted elimination shared with <see cref="DiagonalizeLU"/>,
    /// <see cref="ComputeDeterminant"/>, and <see cref="Solve"/> (see TODO-2026-07-11-pass5.md item #69):
    /// column <c>j</c> of the inverse is the solution of <c>A·x = e_j</c> for the <c>j</c>-th standard
    /// basis vector, computed via the same forward/back substitution as <see cref="Solve"/>.
    /// </remarks>
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

        if (!TryDecomposePivoted(relativeSingularityTolerance, out PivotedElimination decomposition))
        {
            throw new InvalidOperationException("The matrix is singular or numerically near-singular and cannot be reliably inverted.");
        }

        int n = Rows;
        T[,] inverse = new T[n, n];
        T[] permutedColumn = new T[n];
        for (int col = 0; col < n; col++)
        {
            for (int i = 0; i < n; i++)
                permutedColumn[i] = decomposition.Permutation[i] == col ? T.One : T.Zero;

            T[] x = SolvePermuted(decomposition, permutedColumn);
            for (int row = 0; row < n; row++)
                inverse[row, col] = x[row];
        }

        // Unlike false (a permanently cached, potentially wrong negative answer that disables lazy
        // recomputation - see TODO-2026-07-11-pass5.md item #61), null defers isIdentity/isTriangular/
        // isDiagonal to the first access of the corresponding property, which recomputes them from the
        // actual computed inverse array. This correctly reports e.g. the inverse of a diagonal matrix
        // as still diagonal, instead of always false regardless of the source's actual structure.
        return new Matrix<T>(inverse, null, null, null, null);
    }
}
