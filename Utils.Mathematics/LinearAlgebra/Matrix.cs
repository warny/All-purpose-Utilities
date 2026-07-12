using System.Globalization;
using System.Numerics;
using System.Text;
using Utils.Arrays;
using Utils.Objects;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Represents a matrix of elements of type T.
/// </summary>
public sealed partial class Matrix<T> : IFormattable, IEquatable<Matrix<T>>, IEquatable<T[,]>, IEquatable<T[][]>, IEquatable<Vector<T>[]>, ICloneable
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
{
    private readonly T[,] components;
    private bool? isTriangular;
    private bool? isDiagonal;
    private bool? isIdentity;
    private T? determinant;
    private int? hashCode;

    /// <summary>
    /// The type's machine epsilon: the smallest positive value such that <c>1 + eps != 1</c> in
    /// <typeparamref name="T"/>'s own arithmetic. Computed generically rather than a hard-coded
    /// literal such as <c>1e-10</c>, which is meaningless (and, for low-precision types such as
    /// <see cref="Half"/>, would silently underflow to zero) across arbitrary
    /// <see cref="IFloatingPoint{TSelf}"/> types with wildly different precision.
    /// </summary>
    private static readonly T MachineEpsilon = NumericPrecision.MachineEpsilon<T>();

    /// <summary>
    /// Computes a default relative-plus-absolute tolerance for an operation over a problem of the
    /// given <paramref name="dimension"/> whose values have a maximum magnitude of
    /// <paramref name="scale"/>. Used as the shared default for the pivot/rank tolerance in
    /// <see cref="Solve"/>/<see cref="Invert"/>/<see cref="Determinant"/>, <see cref="DecomposeQR"/>'s
    /// rank-deficiency threshold, <see cref="IsSymmetric()"/>'s comparison tolerance, and
    /// <see cref="ComputeEigenvalues"/>'s convergence threshold - each of those members also accepts
    /// its own explicit override for callers who need a different threshold for that specific concern.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The relative term (<c>scale * dimension * eps</c>) follows the common numerical-linear-algebra
    /// convention that accumulated round-off grows roughly with problem size (e.g. LAPACK-style
    /// rank/near-singularity heuristics use <c>n * eps</c>). A flat multiplier large enough to be
    /// meaningful for bigger systems (an earlier version of this method used a fixed 100x with no
    /// dimension scaling) is far too loose for small matrices of a low-precision type: for a 2x2
    /// <see cref="Half"/> matrix, a flat 100x its machine epsilon is already about 10% of the
    /// matrix's magnitude, misclassifying ordinary invertible matrices as singular.
    /// </para>
    /// <para>
    /// The additive <c>+ 1</c> is a fixed absolute floor: without it, an all-zero or near-zero-scale
    /// matrix would compute a tolerance of exactly (or near) zero, collapsing the check back to exact
    /// equality precisely for the inputs where that is least appropriate.
    /// </para>
    /// <para>
    /// <b>Known limitation:</b> this uses a single scalar <paramref name="scale"/> (typically the
    /// largest entry across the whole matrix) for the entire problem. For a matrix whose entries span
    /// very different magnitudes (e.g. one huge diagonal entry alongside a small but numerically
    /// significant off-diagonal block), a single global scale can make the effective tolerance too
    /// loose for the smaller sub-problem, potentially treating it as already converged, singular, or
    /// symmetric relative to the whole matrix's scale when it is not relative to its own. Properly
    /// addressing that requires per-row/column equilibration or block-aware scaling, which this
    /// method does not attempt.
    /// </para>
    /// </remarks>
    private static T DefaultTolerance(T scale, int dimension)
        => MachineEpsilon * T.CreateChecked(dimension) * (scale + T.One);

    /// <summary>
    /// Validates that a caller-supplied tolerance is usable as a comparison threshold: finite and
    /// non-negative. A <see langword="NaN"/> tolerance would make every "is this within tolerance"
    /// comparison false (vacuously accepting everything as "not equal" or, depending on the
    /// comparison direction, silently accepting everything as "equal"), and a negative tolerance
    /// would reject exact matches.
    /// </summary>
    private static void ValidateTolerance(T tolerance, string parameterName)
    {
        if (!T.IsFinite(tolerance) || tolerance < T.Zero)
            throw new ArgumentOutOfRangeException(parameterName, tolerance, "Tolerance must be finite and non-negative.");
    }

    /// <summary>
    /// Gets the number of rows in the matrix.
    /// </summary>
    public int Rows => components.GetLength(0);

    /// <summary>
    /// Gets the number of columns in the matrix.
    /// </summary>
    public int Columns => components.GetLength(1);

    /// <summary>
    /// Indicates if the matrix is square.
    /// </summary>
    public bool IsSquare => components.GetLength(0) == components.GetLength(1);

    /// <summary>
    /// Gets or sets the value of an element at the specified position.
    /// </summary>
    /// <param name="row">Row index.</param>
    /// <param name="col">Column index.</param>
    /// <returns>The element at the specified position.</returns>
    public T this[int row, int col] => components[row, col];

    /// <summary>
    /// Lazily determines whether the matrix is triangular, diagonal, or identity.
    /// </summary>
    private void DetermineStructuralFlags()
    {
        if (isTriangular is not null && isDiagonal is not null && isIdentity is not null) return;
        if (!IsSquare)
        {
            isDiagonal = false;
            isTriangular = false;
            isIdentity = false;
            return;
        }

        int dimension = components.GetLength(0);
        bool upperTriangular = true;
        bool lowerTriangular = true;
        bool identityCheck = true;

        for (int row = 0; row < dimension; row++)
        {
            for (int col = 0; col < dimension; col++)
            {
                if (row == col && components[row, col] != T.One)
                    identityCheck = false;

                if (components[row, col] != T.Zero)
                {
                    if (row > col) upperTriangular = false;
                    else if (row < col) lowerTriangular = false;
                }

                if (!upperTriangular && !lowerTriangular) break;
            }

            if (!upperTriangular && !lowerTriangular) break;
        }

        isTriangular = upperTriangular || lowerTriangular;
        isDiagonal = upperTriangular && lowerTriangular;
        // Identity requires diagonal structure (all off-diagonal = 0) and all diagonal = 1.
        isIdentity = isDiagonal.Value && identityCheck;
    }

    /// <summary>
    /// Computes the triangular/diagonal/identity structure treating any value within
    /// <paramref name="tolerance"/> of the expected exact value (zero off-diagonal, one on the
    /// diagonal) as matching. Never caches into <see cref="isTriangular"/>/<see cref="isDiagonal"/>/
    /// <see cref="isIdentity"/>, which hold the exact-comparison result used by the parameterless
    /// properties.
    /// </summary>
    private (bool Triangular, bool Diagonal, bool Identity) DetermineStructuralFlags(T tolerance)
    {
        if (!IsSquare) return (false, false, false);

        int dimension = components.GetLength(0);
        bool upperTriangular = true;
        bool lowerTriangular = true;
        bool identityCheck = true;

        for (int row = 0; row < dimension; row++)
        {
            for (int col = 0; col < dimension; col++)
            {
                T value = components[row, col];
                if (row == col && T.Abs(value - T.One) > tolerance)
                    identityCheck = false;

                if (T.Abs(value) > tolerance)
                {
                    if (row > col) upperTriangular = false;
                    else if (row < col) lowerTriangular = false;
                }
            }
        }

        bool diagonal = upperTriangular && lowerTriangular;
        return (upperTriangular || lowerTriangular, diagonal, diagonal && identityCheck);
    }

    /// <summary>
    /// Indicates whether the matrix is triangular (upper or lower).
    /// </summary>
    public bool IsTriangular
    {
        get
        {
            DetermineStructuralFlags();
            return isTriangular ?? false;
        }
    }

    /// <summary>
    /// Indicates whether the matrix is diagonal (all off-diagonal elements are zero).
    /// </summary>
    public bool IsDiagonal
    {
        get
        {
            DetermineStructuralFlags();
            return isDiagonal ?? false;
        }
    }

    /// <summary>
    /// Indicates if the matrix is an identity matrix.
    /// </summary>
    public bool IsIdentity
    {
        get
        {
            DetermineStructuralFlags();
            return isIdentity ?? false;
        }
    }

    /// <summary>
    /// Indicates if the matrix represents a normal space.
    /// </summary>
    public bool IsNormalSpace
    {
        get
        {
            if (!IsSquare) return false;
            int lastRow = Rows - 1;
            int lastCol = Columns - 1;
            for (int col = 0; col < lastCol; col++)
            {
                if (components[lastRow, col] != T.Zero) return false;
            }
            return components[lastRow, lastCol] == T.One;
        }
    }

    /// <summary>
    /// Indicates whether the matrix is triangular (upper or lower) within <paramref name="tolerance"/>,
    /// treating any off-diagonal entry with absolute value at most <paramref name="tolerance"/> as
    /// zero. Unlike <see cref="IsTriangular"/>, which requires exact zero, this tolerates rounding
    /// noise left over from prior arithmetic or decomposition; it is a separate, explicitly opt-in
    /// predicate rather than a silently chosen global tolerance applied to <see cref="IsTriangular"/>.
    /// </summary>
    /// <param name="tolerance">Maximum absolute value an off-diagonal entry may have and still be treated as zero. Must be finite and non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not finite or is negative.</exception>
    public bool IsTriangularWithin(T tolerance)
    {
        ValidateTolerance(tolerance, nameof(tolerance));
        return DetermineStructuralFlags(tolerance).Triangular;
    }

    /// <summary>
    /// Indicates whether the matrix is diagonal within <paramref name="tolerance"/>. See
    /// <see cref="IsTriangularWithin"/> for the tolerance-vs-exact rationale.
    /// </summary>
    /// <param name="tolerance">Maximum absolute value an off-diagonal entry may have and still be treated as zero. Must be finite and non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not finite or is negative.</exception>
    public bool IsDiagonalWithin(T tolerance)
    {
        ValidateTolerance(tolerance, nameof(tolerance));
        return DetermineStructuralFlags(tolerance).Diagonal;
    }

    /// <summary>
    /// Indicates whether the matrix is the identity matrix within <paramref name="tolerance"/>,
    /// treating diagonal entries within <paramref name="tolerance"/> of one, and off-diagonal
    /// entries within <paramref name="tolerance"/> of zero, as matching. See
    /// <see cref="IsTriangularWithin"/> for the tolerance-vs-exact rationale.
    /// </summary>
    /// <param name="tolerance">Maximum allowed absolute deviation from the expected exact value. Must be finite and non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not finite or is negative.</exception>
    public bool IsIdentityWithin(T tolerance)
    {
        ValidateTolerance(tolerance, nameof(tolerance));
        return DetermineStructuralFlags(tolerance).Identity;
    }

    /// <summary>
    /// Indicates whether the matrix represents a normal space within <paramref name="tolerance"/>.
    /// See <see cref="IsTriangularWithin"/> for the tolerance-vs-exact rationale.
    /// </summary>
    /// <param name="tolerance">Maximum allowed absolute deviation from the expected exact value. Must be finite and non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not finite or is negative.</exception>
    public bool IsNormalSpaceWithin(T tolerance)
    {
        ValidateTolerance(tolerance, nameof(tolerance));
        if (!IsSquare) return false;
        int lastRow = Rows - 1;
        int lastCol = Columns - 1;
        for (int col = 0; col < lastCol; col++)
        {
            if (T.Abs(components[lastRow, col]) > tolerance) return false;
        }
        return T.Abs(components[lastRow, lastCol] - T.One) <= tolerance;
    }

    /// <summary>
    /// Resizes the matrix to the specified new dimensions, copying the overlapping top-left prefix and
    /// zero-filling any newly added rows/columns.
    /// </summary>
    /// <param name="newRows">The new number of rows. Must be positive.</param>
    /// <param name="newColumns">The new number of columns. Must be positive.</param>
    /// <returns>A new matrix with the requested dimensions.</returns>
    /// <remarks>
    /// Despite its name, this is a resize, not a pure enlargement: whenever <paramref name="newRows"/>
    /// and/or <paramref name="newColumns"/> is smaller than the corresponding current dimension, the
    /// returned matrix crops that dimension down (see TODO-2026-07-11-pass5.md item #67) instead of
    /// throwing or requiring the new dimensions to be at least the current ones. Only the overlapping
    /// <c>min(current, new)</c> prefix of rows/columns is preserved either way.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="newRows"/> or <paramref name="newColumns"/> is not positive.
    /// </exception>
    public Matrix<T> Pad(int newRows, int newColumns)
    {
        if (newRows <= 0) throw new ArgumentException("Row count must be positive", nameof(newRows));
        if (newColumns <= 0) throw new ArgumentException("Column count must be positive", nameof(newColumns));

        T[,] paddedMatrix = new T[newRows, newColumns];
        int rowCount = MathEx.Min(newRows, Rows);
        int colCount = MathEx.Min(newColumns, Columns);

        for (int i = 0; i < rowCount; i++)
        {
            for (int j = 0; j < colCount; j++)
            {
                paddedMatrix[i, j] = components[i, j];
            }
        }
        return new Matrix<T>(paddedMatrix);
    }

    /// <summary>
    /// Returns the transpose of this matrix (rows and columns swapped).
    /// </summary>
    /// <returns>A new <see cref="Matrix{T}"/> with dimensions <c>Columns × Rows</c>.</returns>
    public Matrix<T> Transpose()
    {
        var result = new T[Columns, Rows];
        for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Columns; col++)
                result[col, row] = components[row, col];
        return new Matrix<T>(result);
    }

    /// <summary>
    /// Returns the specified row as a vector.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <returns>A new <see cref="Vector{T}"/> containing the row elements.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="row"/> is out of range.</exception>
    public Vector<T> GetRow(int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);
        T[] result = new T[Columns];
        for (int col = 0; col < Columns; col++)
            result[col] = components[row, col];
        return new Vector<T>(result);
    }

    /// <summary>
    /// Returns the specified column as a vector.
    /// </summary>
    /// <param name="col">Zero-based column index.</param>
    /// <returns>A new <see cref="Vector{T}"/> containing the column elements.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="col"/> is out of range.</exception>
    public Vector<T> GetColumn(int col)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(col);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(col, Columns);
        T[] result = new T[Rows];
        for (int row = 0; row < Rows; row++)
            result[row] = components[row, col];
        return new Vector<T>(result);
    }

    /// <summary>
    /// Gets the determinant of the matrix.
    /// </summary>
    public T Determinant
    {
        get
        {
            if (determinant is null)
            {
                if (!IsSquare)
                    throw new InvalidOperationException("The matrix is not square.");

                determinant = ComputeDeterminant();
            }
            return determinant.Value;
        }
    }

    private T ComputeDeterminant()
    {
        int n = Rows;
        T[,] u = ToArray();
        int swaps = 0;
        // Elimination divides by the pivot below, so a pivot that is merely close to zero (not
        // exactly zero) would otherwise amplify rounding error into a huge/infinite/NaN result
        // instead of the mathematically expected near-zero determinant of a near-singular matrix.
        T pivotTolerance = DefaultTolerance(MaxAbsoluteEntry(u), n);

        for (int k = 0; k < n; k++)
        {
            int pivotRow = k;
            for (int i = k + 1; i < n; i++)
            {
                if (T.Abs(u[i, k]) > T.Abs(u[pivotRow, k]))
                    pivotRow = i;
            }

            if (T.Abs(u[pivotRow, k]) <= pivotTolerance)
                return T.Zero;

            if (pivotRow != k)
            {
                PermuteRows(u, k, pivotRow);
                swaps++;
            }

            for (int row = k + 1; row < n; row++)
            {
                T factor = u[row, k] / u[k, k];
                for (int col = k; col < n; col++)
                    u[row, col] -= factor * u[k, col];
            }
        }

        // Each row swap inverts the sign of the determinant.
        T det = (swaps % 2 == 0) ? T.One : -T.One;
        for (int i = 0; i < n; i++)
            det *= u[i, i];
        return det;
    }



    /// <summary>
    /// Converts the matrix to an array of vectors.
    /// </summary>
    /// <returns>An array of vectors representing the matrix columns.</returns>
    public Vector<T>[] ToVectors()
    {
        Vector<T>[] result = new Vector<T>[Columns];
        for (int col = 0; col < Columns; col++)
        {
            T[] columnComponents = new T[Rows];
            for (int row = 0; row < Rows; row++)
            {
                columnComponents[row] = components[row, col];
            }
            result[col] = new Vector<T>(columnComponents);
        }
        return result;
    }

    /// <summary>
    /// Returns a string representation of the matrix.
    /// </summary>
    public override string ToString() => ToString("", CultureInfo.CurrentCulture);

    /// <summary>
    /// Returns a formatted string representation of the matrix.
    /// </summary>
    public string ToString(string? format) => ToString(format, CultureInfo.CurrentCulture);

    /// <summary>
    /// Returns a formatted string representation of the matrix using the specified format provider.
    /// </summary>
    /// <param name="format">
    /// A composite format string: an optional layout token (<c>""</c>, <c>"S"</c>, <c>"C"</c>, or
    /// <c>"SC"</c>, selecting the row separator) optionally followed by <c>:</c> and a standard
    /// numeric format string (e.g. <c>"S:F2"</c>) forwarded verbatim to each element's own
    /// <see cref="IFormattable.ToString(string?, IFormatProvider?)"/>. When no numeric format is
    /// given, elements are formatted with their own default (<see langword="null"/>) format rather
    /// than being rounded to a culture-dependent number of decimals first: unlike the previous
    /// behavior, this never silently discards precision the caller did not ask to lose.
    /// </param>
    /// <param name="formatProvider">
    /// Culture or number-format info controlling the row/column separators and forwarded to each
    /// element's own formatter alongside the numeric format.
    /// </param>
    /// <exception cref="FormatException">Thrown when the layout token is not one of the recognized values.</exception>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        format ??= "";
        string layoutToken = format;
        string? numericFormat = null;
        int colonIndex = format.IndexOf(':');
        if (colonIndex >= 0)
        {
            layoutToken = format[..colonIndex];
            numericFormat = format[(colonIndex + 1)..];
        }

        StringBuilder sb = new StringBuilder();
        string componentsSeparator = ", ";
        string lineSeparator = Environment.NewLine;

        if (formatProvider is CultureInfo culture)
        {
            componentsSeparator = culture.TextInfo.ListSeparator;
        }
        else if (formatProvider is NumberFormatInfo numberFormat)
        {
            componentsSeparator = numberFormat.CurrencyDecimalSeparator == "," ? ";" : componentsSeparator;
        }

        switch (layoutToken.ToUpperInvariant())
        {
            case "":
                break;
            case "S":
                lineSeparator = " ";
                break;
            case "C":
                lineSeparator = ", ";
                break;
            case "SC":
                lineSeparator = " ; ";
                break;
            default:
                throw new FormatException($"Unrecognized matrix layout token '{layoutToken}'. Expected \"\", \"S\", \"C\", or \"SC\", optionally followed by \":<numeric format>\".");
        }

        sb.Append("{ ");
        for (int row = 0; row < Rows; row++)
        {
            if (row > 0) sb.Append(lineSeparator);
            sb.Append("{ ");
            for (int col = 0; col < Columns; col++)
            {
                if (col > 0) sb.Append(componentsSeparator);
                sb.Append(components[row, col].ToString(numericFormat, formatProvider));
            }
            sb.Append(" }");
        }
        sb.Append(" }");
        return sb.ToString();
    }

    /// <summary>
    /// Checks if this matrix is equal to another object.
    /// </summary>
    public override bool Equals(object? obj) => obj switch
    {
        Matrix<T> m => Equals(m),
        T[,] a => Equals(a),
        T[][] b => Equals(b),
        Vector<T>[] v => Equals(v),
        _ => false,
    };

    /// <summary>
    /// Checks if this matrix is equal to another matrix.
    /// </summary>
    /// <remarks>
    /// Compares dimensions and elements directly rather than gating on cached hash-code equality
    /// first. A correct hash implementation guarantees equal values hash equally, so a hash
    /// precondition here adds no correctness value; it only makes it hazardous to later introduce a
    /// tolerance-aware equality or hashing policy where two "equal" matrices might legitimately hash
    /// differently.
    /// </remarks>
    public bool Equals(Matrix<T>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(other.components);
    }

    /// <summary>
    /// Checks if this matrix is equal to a 2D array.
    /// </summary>
    public bool Equals(T[,]? other)
    {
        if (other is null) return false;
        if (Rows != other.GetLength(0) || Columns != other.GetLength(1)) return false;
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                if (!components[i, j].Equals(other[i, j])) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Checks if this matrix is equal to a jagged array.
    /// </summary>
    public bool Equals(T[][]? other)
    {
        if (other is null) return false;
        if (Rows != other.Length) return false;
        for (int i = 0; i < Rows; i++)
        {
            if (other[i].Length != Columns) return false;
            for (int j = 0; j < Columns; j++)
            {
                if (!components[i, j].Equals(other[i][j])) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Checks if this matrix is equal to an array of vectors.
    /// </summary>
    public bool Equals(params Vector<T>[]? other)
    {
        if (other is null) return false;
        if (other.Length != Columns) return false;
        for (int i = 0; i < Columns; i++)
        {
            var vector = other[i];
            if (vector.Dimension != Rows) return false;
            for (int j = 0; j < Rows; j++)
            {
                if (!components[j, i].Equals(vector[j])) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Gets the hash code for the matrix.
    /// </summary>
    public override int GetHashCode() => hashCode ??= ObjectUtils.ComputeHash(components);

    /// <summary>
    /// Converts the matrix to a 2D array.
    /// </summary>
    public T[,] ToArray() => (T[,])ArrayUtils.Copy(components);

    /// <summary>
    /// Creates a copy of the matrix.
    /// </summary>
    public object Clone() => new Matrix<T>(this);
}
