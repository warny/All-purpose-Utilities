using System.Numerics;
using Utils.Collections;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Represents a mathematical vector.
/// </summary>
public sealed partial class Vector<T> : IEquatable<Vector<T>>, IEquatable<T[]>, ICloneable
    where T : struct, IFloatingPoint<T>, IRootFunctions<T>
{
    /// <summary>
    /// Equality comparer used to evaluate component arrays.
    /// </summary>
    private static EnumerableEqualityComparer<T> ComponentComparer { get; } = EnumerableEqualityComparer<T>.Default;

    /// <summary>
    /// Vector components.
    /// </summary>
    internal readonly T[] components;

    /// <summary>
    /// Length of the vector (computed lazily).
    /// </summary>
    private T? norm;

    /// <summary>
    /// Initializes a vector with the given dimension.
    /// </summary>
    /// <param name="dimensions">Number of dimensions.</param>
    private Vector(int dimensions)
    {
        components = new T[dimensions];
        norm = T.Zero;
    }

    /// <summary>
    /// Initializes a vector with the provided components.
    /// </summary>
    /// <param name="components">Component values of the vector.</param>
    /// <exception cref="ArgumentException">Thrown when no components are provided.</exception>
    public Vector(params T[] components)
    {
        if (components.Length == 0) throw new ArgumentException("Vector dimension cannot be 0", nameof(components));
        this.components = new T[components.Length];
        Array.Copy(components, this.components, components.Length);
    }

    /// <summary>
    /// Initializes a new instance by copying another vector.
    /// </summary>
    /// <param name="vector">Vector to copy.</param>
    public Vector(Vector<T> vector)
    {
        components = new T[vector.components.Length];
        Array.Copy(vector.components, components, vector.components.Length);
    }

    /// <summary>
    /// Gets the value of the specified component.
    /// </summary>
    /// <param name="dimension">Component index.</param>
    public T this[int dimension] => this.components[dimension];

    /// <summary>
    /// Gets the vector dimension.
    /// </summary>
    public int Dimension => this.components.Length;

    /// <summary>
    /// Gets the length of the vector.
    /// </summary>
    public T Norm
    {
        get
        {
            if (norm is not null) return norm.Value;
            T temp = T.Zero;
            for (int i = 0; i < this.components.Length; i++)
            {
                temp += this.components[i] * this.components[i];
            }
            norm = T.Sqrt(temp);
            return norm.Value;
        }
    }

    /// <summary>
    /// Returns a normalized version of the vector.
    /// </summary>
    /// <returns>The normalized vector.</returns>
    public Vector<T> Normalize() => this / Norm;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj switch
    {
        Vector<T> v => Equals(v),
        double[] a => Equals(a),
        _ => false,
    };

    /// <summary>
    /// Determines whether the current vector is equal to another vector.
    /// </summary>
    /// <param name="other">The vector to compare with.</param>
    /// <returns><see langword="true"/> if vectors are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(Vector<T>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(other.components);
    }

    /// <summary>
    /// Determines whether the current vector is equal to the specified component array.
    /// </summary>
    /// <param name="other">Component array to compare with.</param>
    /// <returns><see langword="true"/> if arrays are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(T[] other)
    {
        if (other is null) return false;
        return ComponentComparer.Equals(this.components, other);
    }

    /// <summary>
    /// Returns a string that represents the current vector.
    /// </summary>
    /// <returns>A string representation of the vector.</returns>
    public override string ToString()
        => $"({string.Join(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator, components)})";

    /// <summary>
    /// Converts a vector for use in a normal space.
    /// </summary>
    /// <returns>The converted vector for normal space.</returns>
    public Vector<T> ToNormalSpace()
    {
        Vector<T> result = new(Dimension + 1);
        Array.Copy(components, result.components, Dimension);
        result.components[Dimension] = T.One;
        return result;
    }

    /// <summary>
    /// Converts a vector usable in normal space to a vector usable in Cartesian space.
    /// </summary>
    /// <returns>The converted vector for Cartesian space.</returns>
    public Vector<T> FromNormalSpace()
    {
        var temp = this;
        if (temp[temp.Dimension - 1] != T.One)
        {
            temp /= temp[temp.Dimension - 1];
        }
        Vector<T> result = new(Dimension - 1);
        Array.Copy(temp.components, result.components, Dimension - 1);
        return result;
    }

    /// <summary>
    /// Returns the cross product of (n-1) vectors of dimension n.
    /// </summary>
    /// <param name="vectors">Vectors of dimension n.</param>
    /// <returns>A normal vector.</returns>
    /// <exception cref="ArgumentException">Thrown if vectors are not all of dimension n.</exception>
    public static Vector<T> Product(params Vector<T>[] vectors)
    {
        int dimensions = vectors.Length + 1;
        foreach (var vector in vectors)
        {
            if (vector.components.Length != dimensions)
            {
                throw new ArgumentException(string.Format("All vectors are not of dimension {0}", dimensions), "vectors");
            }
        }

        T[] result = new T[dimensions];
        var columns = Enumerable.Range(0, dimensions);
        T sign = T.One;
        foreach (var column in columns)
        {
            var nextColumns = columns.Where(c => c != column);
            result[column] = sign * ComputeProduct(1, nextColumns, vectors);
            sign = -sign;
        }

        return new Vector<T>(result);
    }

    /// <summary>
    /// Recursive computation of the cross product of n-1 vectors in an n-dimensional space.
    /// </summary>
    /// <param name="recurrence">The current recursion step.</param>
    /// <param name="columns">The columns to process for computation.</param>
    /// <param name="vectors">The vectors to compute the product.</param>
    /// <returns>The computed product.</returns>
    private static T ComputeProduct(int recurrence, IEnumerable<int> columns, Vector<T>[] vectors)
    {
        T result = T.Zero;
        T sign = T.One;
        foreach (int column in columns)
        {
            T temp = sign;
            if (recurrence > 0)
            {
                temp *= vectors[recurrence - 1].components[column];

                var nextColumns = columns.Where(c => c != column);
                if (temp != T.Zero && nextColumns.Any())
                {
                    temp *= ComputeProduct(recurrence + 1, nextColumns, vectors);
                }
            }
            result += temp;
            sign = -sign;
        }
        return result;
    }

    /// <summary>
    /// Returns a hash code for the vector.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            var temp = components.Length.GetHashCode();
            foreach (var el in components)
            {
                temp = ((temp * 23) << 1) + el.GetHashCode();
            }
            return temp;
        }
    }

    /// <summary>
    /// Creates a copy of the vector.
    /// </summary>
    /// <returns>A new vector with the same components.</returns>
    public object Clone() => new Vector<T>(this);
}

