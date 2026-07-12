using Utils.Objects;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Represents a matrix and provides various constructors.
/// </summary>
public sealed partial class Matrix<T>
{
    /// <summary>
    /// Initializes a matrix with the specified dimensions.
    /// </summary>
    /// <param name="dimensionX">Number of rows. Must be positive.</param>
    /// <param name="dimensionY">Number of columns. Must be positive.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="dimensionX"/> or <paramref name="dimensionY"/> is not positive.
    /// 0×0 (and other zero/negative-dimensioned) matrices are not a supported construction, matching
    /// the same policy already enforced by <see cref="Identity(int)"/>, <see cref="Zero(int, int)"/>,
    /// and <see cref="Diagonal(IEnumerable{T})"/>, rather than relying on the CLR's own
    /// exception-type-inconsistent behavior for invalid multidimensional array allocation.
    /// </exception>
    public Matrix(int dimensionX, int dimensionY)
    {
        if (dimensionX <= 0) throw new ArgumentException("Row count must be positive", nameof(dimensionX));
        if (dimensionY <= 0) throw new ArgumentException("Column count must be positive", nameof(dimensionY));
        components = new T[dimensionX, dimensionY];
    }

    /// <summary>
    /// Initializes a matrix with a backing array and structural metadata.
    /// </summary>
    /// <param name="array">Backing array containing matrix values.</param>
    /// <param name="isIdentity">
    /// Indicates whether the matrix is an identity matrix, or <see langword="null"/> when the caller
    /// does not have a mathematically guaranteed answer. Unlike <see langword="false"/> - which
    /// permanently caches a (possibly wrong) negative answer and disables the lazy recomputation
    /// performed by <see cref="DetermineStructuralFlags"/> - <see langword="null"/> defers the
    /// determination to the first access of <see cref="IsIdentity"/>, computed from the actual values
    /// in <paramref name="array"/> (see TODO-2026-07-11-pass5.md item #61).
    /// </param>
    /// <param name="isTriangular">
    /// Indicates whether the matrix is triangular (upper or lower), or <see langword="null"/> per the
    /// same "unknown defers to lazy recomputation" rule as <paramref name="isIdentity"/>.
    /// </param>
    /// <param name="isDiagonal">
    /// Indicates whether the matrix is diagonal (all off-diagonal elements are zero), or
    /// <see langword="null"/> per the same "unknown defers to lazy recomputation" rule as
    /// <paramref name="isIdentity"/>.
    /// </param>
    /// <param name="determinant">Precomputed determinant, if available.</param>
    internal Matrix(T[,] array, bool? isIdentity, bool? isTriangular, bool? isDiagonal, T? determinant)
    {
        array.Arg().MustNotBeNull();
        this.components = array;
        this.isTriangular = isTriangular;
        this.isDiagonal = isDiagonal;
        this.isIdentity = isIdentity;
        this.determinant = determinant;
    }

    /// <summary>
    /// Initializes a matrix from a 2D array.
    /// </summary>
    /// <param name="array">Array containing the matrix values. Must have at least one row and one column.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="array"/> has zero rows or zero columns.</exception>
    public Matrix(T[,] array)
    {
        array.Arg().MustNotBeNull();
        if (array.GetLength(0) == 0 || array.GetLength(1) == 0)
            throw new ArgumentException("The array must have at least one row and one column.", nameof(array));
        components = new T[array.GetLength(0), array.GetLength(1)];
        Array.Copy(array, this.components, array.Length);
        var isSquare = IsSquare;
        isTriangular = isSquare ? (bool?)null : false;
        isDiagonal = isSquare ? (bool?)null : false;
        isIdentity = isSquare ? (bool?)null : false;
        determinant = null;
    }

    /// <summary>
    /// Initializes a matrix from a jagged array.
    /// </summary>
    /// <param name="array">Jagged array containing the matrix values. Must be non-empty, with no null rows, and at least one row must be non-empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="array"/> has no rows, contains a null row, or every row is empty.</exception>
    public Matrix(T[][] array)
    {
        array.Arg().MustNotBeNull();
        if (array.Length == 0)
            throw new ArgumentException("At least one row is required.", nameof(array));
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] is null)
                throw new ArgumentException($"Row {i} is null.", nameof(array));
        }

        int maxYLength = array.Select(a => a.Length).Max();
        if (maxYLength == 0)
            throw new ArgumentException("At least one row must be non-empty.", nameof(array));
        components = new T[array.Length, maxYLength];

        for (int i = 0; i < array.Length; i++)
        {
            for (int j = 0; j < array[i].Length; j++)
            {
                components[i, j] = array[i][j];
            }
        }
        isTriangular = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
        isDiagonal = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
        isIdentity = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
        determinant = null;
    }

    /// <summary>
    /// Initializes a matrix from column vectors.
    /// </summary>
    /// <param name="vectors">Column vectors. At least one is required, and none may be null.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when no vectors are provided, a vector is null, or vectors do not share the same dimension.
    /// </exception>
    public Matrix(params Vector<T>[] vectors)
    {
        vectors.Arg().MustNotBeNull();
        if (vectors.Length == 0)
            throw new ArgumentException("At least one vector is required.", nameof(vectors));
        for (int i = 0; i < vectors.Length; i++)
        {
            if (vectors[i] is null)
                throw new ArgumentException($"Vector {i} is null.", nameof(vectors));
        }
        if (vectors.Any(v => v.Dimension != vectors[0].Dimension))
            throw new ArgumentException("All vectors must have the same dimension", nameof(vectors));
        components = new T[vectors[0].Dimension, vectors.Length];
        for (int i = 0; i < vectors.Length; i++)
        {
            for (int j = 0; j < vectors[i].Dimension; j++)
            {
                components[j, i] = vectors[i][j];
            }
        }
        isTriangular = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
        isDiagonal = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
        isIdentity = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
        determinant = null;
    }

    /// <summary>
    /// Creates a copy of an existing matrix.
    /// </summary>
    /// <param name="matrix">Matrix to copy.</param>
    public Matrix(Matrix<T> matrix)
    {
        matrix.Arg().MustNotBeNull();

        this.components = new T[matrix.components.GetLength(0), matrix.components.GetLength(1)];
        Array.Copy(matrix.components, this.components, matrix.components.Length);
        this.isTriangular = matrix.isTriangular;
        this.isDiagonal = matrix.isDiagonal;
        this.isIdentity = matrix.isIdentity;
        this.determinant = matrix.determinant;
    }
}

