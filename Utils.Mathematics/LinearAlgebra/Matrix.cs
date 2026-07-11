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
    /// Relative pivot tolerance used by <see cref="Solve"/> and <see cref="Invert"/> to reject a
    /// numerically near-singular matrix (magnitude relative to the matrix's largest entry) instead
    /// of dividing by a pivot that is merely close to zero, which would silently amplify rounding
    /// error into a huge, infinite, or NaN result while still returning an apparently valid answer.
    /// </summary>
    private static readonly T SingularityRelativeTolerance = T.CreateChecked(1e-10);

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
    /// <param name="tolerance">Maximum absolute value an off-diagonal entry may have and still be treated as zero.</param>
    public bool IsTriangularWithin(T tolerance) => DetermineStructuralFlags(tolerance).Triangular;

    /// <summary>
    /// Indicates whether the matrix is diagonal within <paramref name="tolerance"/>. See
    /// <see cref="IsTriangularWithin"/> for the tolerance-vs-exact rationale.
    /// </summary>
    /// <param name="tolerance">Maximum absolute value an off-diagonal entry may have and still be treated as zero.</param>
    public bool IsDiagonalWithin(T tolerance) => DetermineStructuralFlags(tolerance).Diagonal;

    /// <summary>
    /// Indicates whether the matrix is the identity matrix within <paramref name="tolerance"/>,
    /// treating diagonal entries within <paramref name="tolerance"/> of one, and off-diagonal
    /// entries within <paramref name="tolerance"/> of zero, as matching. See
    /// <see cref="IsTriangularWithin"/> for the tolerance-vs-exact rationale.
    /// </summary>
    /// <param name="tolerance">Maximum allowed absolute deviation from the expected exact value.</param>
    public bool IsIdentityWithin(T tolerance) => DetermineStructuralFlags(tolerance).Identity;

    /// <summary>
    /// Indicates whether the matrix represents a normal space within <paramref name="tolerance"/>.
    /// See <see cref="IsTriangularWithin"/> for the tolerance-vs-exact rationale.
    /// </summary>
    /// <param name="tolerance">Maximum allowed absolute deviation from the expected exact value.</param>
    public bool IsNormalSpaceWithin(T tolerance)
    {
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
    /// Pads the matrix to the specified new dimensions.
    /// </summary>
    /// <param name="newRows">The new number of rows.</param>
    /// <param name="newColumns">The new number of columns.</param>
    /// <returns>A new matrix with the padded dimensions.</returns>
    public Matrix<T> Pad(int newRows, int newColumns)
    {
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
        T pivotTolerance = MaxAbsoluteEntry(u) * SingularityRelativeTolerance;

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
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        format ??= "";
        StringBuilder sb = new StringBuilder();
        string componentsSeparator = ", ";
        string lineSeparator = Environment.NewLine;
        int decimals = 2;

        if (formatProvider is CultureInfo culture)
        {
            componentsSeparator = culture.TextInfo.ListSeparator;
            decimals = culture.NumberFormat.NumberDecimalDigits;
        }
        else if (formatProvider is NumberFormatInfo numberFormat)
        {
            componentsSeparator = numberFormat.CurrencyDecimalSeparator == "," ? ";" : componentsSeparator;
            decimals = numberFormat.NumberDecimalDigits;
        }

        switch (format.ToUpper())
        {
            case "S":
                lineSeparator = " ";
                break;
            case "C":
                lineSeparator = ", ";
                break;
            case "SC":
                lineSeparator = " ; ";
                break;
        }

        sb.Append("{ ");
        for (int row = 0; row < Rows; row++)
        {
            if (row > 0) sb.Append(lineSeparator);
            sb.Append("{ ");
            for (int col = 0; col < Columns; col++)
            {
                if (col > 0) sb.Append(componentsSeparator);
                sb.Append(T.Round(components[row, col], decimals));
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
    public bool Equals(Matrix<T>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return GetHashCode() == other.GetHashCode() && Equals(other.components);
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
