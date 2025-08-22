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
    /// <param name="dimensionX">Number of rows.</param>
    /// <param name="dimensionY">Number of columns.</param>
    public Matrix(int dimensionX, int dimensionY)
    {
        components = new T[dimensionX, dimensionY];
    }

    /// <summary>
    /// Initializes a matrix with a backing array and structural metadata.
    /// </summary>
    /// <param name="array">Backing array containing matrix values.</param>
    /// <param name="isIdentity">Indicates whether the matrix is an identity matrix.</param>
    /// <param name="isDiagonalized">Indicates whether the matrix is diagonalized.</param>
    /// <param name="isTriangularised">Indicates whether the matrix is triangularized.</param>
    /// <param name="determinant">Precomputed determinant, if available.</param>
    internal Matrix(T[,] array, bool isIdentity, bool isDiagonalized, bool isTriangularised, T? determinant)
    {
        array.Arg().MustNotBeNull();
        this.components = array;
        this.isDiagonalized = isDiagonalized;
        this.isTriangularised = isTriangularised;
        this.isIdentity = isIdentity;
        this.determinant = determinant;
    }

    /// <summary>
    /// Initializes a matrix from a 2D array.
    /// </summary>
    /// <param name="array">Array containing the matrix values.</param>
    public Matrix(T[,] array)
    {
        array.Arg().MustNotBeNull();
        components = new T[array.GetLength(0), array.GetLength(1)];
        Array.Copy(array, this.components, array.Length);
        var isSquare = IsSquare;
        isDiagonalized = isSquare ? false : (bool?)null;
        isTriangularised = isSquare ? false : (bool?)null;
        isIdentity = isSquare ? false : (bool?)null;
        determinant = null;
    }

    /// <summary>
    /// Initializes a matrix from a jagged array.
    /// </summary>
    /// <param name="array">Jagged array containing the matrix values.</param>
    public Matrix(T[][] array)
    {
        array.Arg().MustNotBeNull();
        int maxYLength = array.Select(a => a.Length).Max();
        components = new T[array.Length, maxYLength];

        for (int i = 0; i < array.Length; i++)
        {
            for (int j = 0; j < array[i].Length; j++)
            {
                components[i, j] = array[i][j];
            }
        }
        isDiagonalized = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
        isTriangularised = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
        isIdentity = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
        determinant = null;
    }

    /// <summary>
    /// Initializes a matrix from column vectors.
    /// </summary>
    /// <param name="vectors">Column vectors.</param>
    /// <exception cref="ArgumentException">Thrown when vectors do not share the same dimension.</exception>
    public Matrix(params Vector<T>[] vectors)
    {
        vectors.Arg().MustNotBeNull();
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
        isDiagonalized = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
        isTriangularised = components.GetLength(0) != components.GetLength(1) ? false : (bool?)null;
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
        this.isDiagonalized = matrix.isDiagonalized;
        this.isTriangularised = matrix.isTriangularised;
        this.isIdentity = matrix.isIdentity;
        this.determinant = matrix.determinant;
    }
}

