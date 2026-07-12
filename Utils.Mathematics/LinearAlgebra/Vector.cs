using System.Collections;
using System.Numerics;
using Utils.Collections;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Represents a mathematical vector.
/// </summary>
public sealed partial class Vector<T> : IEquatable<Vector<T>>, IEquatable<T[]>, ICloneable, IEnumerable<T>
    where T : struct, IFloatingPoint<T>, IRootFunctions<T>
{
    /// <summary>
    /// Equality comparer used to evaluate component arrays.
    /// </summary>
    private static EnumerableEqualityComparer<T> ComponentComparer { get; } = EnumerableEqualityComparer<T>.Default;

    /// <summary>
    /// Vector components.
    /// </summary>
    private readonly T[] components;

    /// <summary>
    /// Length of the vector (computed lazily).
    /// </summary>
    private T? norm;

    /// <summary>
    /// Smallest positive value such that <c>1 + MachineEpsilon != 1</c> for <typeparamref name="T"/>,
    /// used to derive scale-aware default tolerances (see <see cref="DefaultNormTolerance"/>).
    /// </summary>
    private static readonly T MachineEpsilon = NumericPrecision.MachineEpsilon<T>();

    /// <summary>
    /// Validates that a caller-supplied tolerance is usable (finite and non-negative).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not finite or is negative.</exception>
    private static void ValidateTolerance(T tolerance, string parameterName)
    {
        if (!T.IsFinite(tolerance) || tolerance < T.Zero)
            throw new ArgumentOutOfRangeException(parameterName, tolerance, "Tolerance must be finite and non-negative.");
    }

    /// <summary>
    /// Returns the largest absolute value among a component array, used as the scale reference for
    /// <see cref="DefaultNormTolerance"/>.
    /// </summary>
    private static T MaxAbsoluteComponent(T[] components)
    {
        T max = T.Zero;
        for (int i = 0; i < components.Length; i++)
            max = T.Max(max, T.Abs(components[i]));
        return max;
    }

    /// <summary>
    /// Default relative-plus-absolute tolerance below which a norm or homogeneous coordinate is
    /// treated as numerically zero, when the caller does not supply an explicit override. The
    /// relative component (<see cref="MachineEpsilon"/> scaled by dimension and the components'
    /// magnitude) rejects negligible-but-nonzero values proportionally to the vector's own scale; the
    /// "+ 1" absolute floor keeps the tolerance non-zero even when every component is already close
    /// to zero, so a vector like <c>(1e-300, 1e-300, 1e-300)</c> is still correctly rejected instead
    /// of producing a technically-nonzero-but-meaningless normalized result.
    /// </summary>
    private static T DefaultNormTolerance(T[] components)
        => MachineEpsilon * T.CreateChecked(components.Length) * (MaxAbsoluteComponent(components) + T.One);

    /// <summary>
    /// Initializes a vector with the given dimension.
    /// </summary>
    /// <param name="dimensions">Number of dimensions.</param>
    private Vector(int dimensions)
    {
        components = new T[dimensions];
        norm = null;
    }

    /// <summary>
    /// Initializes a vector with the provided components.
    /// </summary>
    /// <param name="components">Component values of the vector.</param>
    /// <exception cref="ArgumentException">Thrown when no components are provided.</exception>
    public Vector(params IEnumerable<T> components)
    {
        this.components = components.ToArray();
        if (this.components.Length == 0) throw new ArgumentException("Vector dimension cannot be 0", nameof(components));
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
    /// <remarks>
    /// Computed via a scaled sum-of-squares accumulation (the same running-scale technique as BLAS
    /// <c>nrm2</c>/<c>hypot</c>), rather than summing <c>component * component</c> directly. A direct sum
    /// can overflow to infinity for large-but-representable components (whose square individually
    /// overflows even though the true norm does not), or underflow to zero for small-but-representable
    /// components (whose square underflows to zero even though it contributes to a representable norm).
    /// Tracking the largest component seen so far as a running scale and accumulating only the *ratio*
    /// of each component to that scale keeps every intermediate value within a representable range.
    /// </remarks>
    public T Norm
    {
        get
        {
            if (norm is not null) return norm.Value;

            T scale = T.Zero;
            T sumOfSquaredRatios = T.One;
            for (int i = 0; i < this.components.Length; i++)
            {
                T absValue = T.Abs(this.components[i]);
                if (absValue == T.Zero) continue;

                if (scale < absValue)
                {
                    T ratio = scale / absValue;
                    sumOfSquaredRatios = T.One + sumOfSquaredRatios * ratio * ratio;
                    scale = absValue;
                }
                else
                {
                    T ratio = absValue / scale;
                    sumOfSquaredRatios += ratio * ratio;
                }
            }

            norm = scale == T.Zero ? T.Zero : scale * T.Sqrt(sumOfSquaredRatios);
            return norm.Value;
        }
    }

    /// <summary>
    /// Returns a normalized version of the vector.
    /// </summary>
    /// <param name="tolerance">
    /// Overrides the default relative-plus-absolute tolerance (see <see cref="DefaultNormTolerance"/>)
    /// below which the norm is treated as zero. Pass <c>T.Zero</c> to reject only an exactly-zero
    /// norm. Must be finite and non-negative when supplied.
    /// </param>
    /// <returns>The normalized vector.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the vector's norm is zero or, absent an
    /// explicit <paramref name="tolerance"/>, numerically negligible relative to its components'
    /// magnitude (e.g. a component like <c>1e-300</c> that underflows to zero once squared, which
    /// would otherwise silently normalize to a wildly disproportionate result).</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is supplied but not finite or is negative.</exception>
    public Vector<T> Normalize(T? tolerance = null)
    {
        if (tolerance is { } explicitTolerance)
            ValidateTolerance(explicitTolerance, nameof(tolerance));

        T norm = Norm;
        T effectiveTolerance = tolerance ?? DefaultNormTolerance(components);
        if (norm <= effectiveTolerance)
            throw new InvalidOperationException("Cannot normalize a vector whose norm is zero or numerically negligible.");
        return this / norm;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj switch
    {
        Vector<T> v => Equals(v),
        T[] a => Equals(a),
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
    public bool Equals(T[]? other)
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
    /// <param name="tolerance">
    /// Overrides the default relative-plus-absolute tolerance (see <see cref="DefaultNormTolerance"/>)
    /// below which the homogeneous coordinate is treated as zero. Pass <c>T.Zero</c> to reject only
    /// an exactly-zero coordinate. Must be finite and non-negative when supplied.
    /// </param>
    /// <returns>The converted vector for Cartesian space.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the homogeneous coordinate (last component) is zero or, absent an explicit
    /// <paramref name="tolerance"/>, numerically negligible relative to the vector's components
    /// (e.g. <c>w = 1e-300</c>, which would otherwise silently divide the other components up to an
    /// astronomically large, meaningless magnitude instead of being recognized as a direction at
    /// infinity). A near-zero homogeneous coordinate represents a direction at infinity, not a point,
    /// and has no meaningful Cartesian equivalent.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is supplied but not finite or is negative.</exception>
    public Vector<T> FromNormalSpace(T? tolerance = null)
    {
        if (tolerance is { } explicitTolerance)
            ValidateTolerance(explicitTolerance, nameof(tolerance));

        var temp = this;
        T w = temp[temp.Dimension - 1];
        T effectiveTolerance = tolerance ?? DefaultNormTolerance(temp.components);
        if (T.Abs(w) <= effectiveTolerance)
            throw new InvalidOperationException("Cannot convert a homogeneous direction (zero or numerically negligible homogeneous coordinate) to a Cartesian point.");
        if (w != T.One)
        {
            temp /= w;
        }
        Vector<T> result = new(Dimension - 1);
        Array.Copy(temp.components, result.components, Dimension - 1);
        return result;
    }

    /// <summary>
    /// Returns the cross product of (n-1) vectors of dimension n.
    /// </summary>
    /// <param name="vectors">Vectors of dimension n.</param>
    /// <returns>A vector perpendicular to all input vectors.</returns>
    /// <exception cref="ArgumentException">Thrown if vectors are not all of dimension n.</exception>
    /// <remarks>
    /// Each component <c>result[i]</c> is <c>(-1)^i</c> times the determinant of the <c>(n-1)x(n-1)</c>
    /// minor formed by the input vectors' components, excluding column <c>i</c> (the standard Laplace
    /// expansion of the cross product along a virtual first row of basis vectors). Each minor's
    /// determinant is computed via <see cref="Matrix{T}.Determinant"/>, which uses Gaussian elimination
    /// (<c>O(n^3)</c>), rather than the previous direct recursive cofactor expansion (<c>O(n!)</c>) that
    /// also re-filtered the remaining-columns sequence at every recursion level.
    /// </remarks>
    public static Vector<T> CrossProduct(params Vector<T>[] vectors)
    {
        int dimensions = vectors.Length + 1;
        foreach (var vector in vectors)
        {
            if (vector.components.Length != dimensions)
            {
                throw new ArgumentException(string.Format("All vectors are not of dimension {0}", dimensions), nameof(vectors));
            }
        }

        T[] result = new T[dimensions];
        T sign = T.One;
        for (int excludedColumn = 0; excludedColumn < dimensions; excludedColumn++)
        {
            result[excludedColumn] = sign * MinorDeterminant(vectors, excludedColumn);
            sign = -sign;
        }

        return new Vector<T>(result);
    }

    /// <inheritdoc cref="CrossProduct(Vector{T}[])"/>
    public static Vector<T> Product(params Vector<T>[] vectors) => CrossProduct(vectors);

    /// <summary>
    /// Computes the determinant of the square matrix formed by using each of <paramref name="vectors"/>
    /// as a row, dropping the component at <paramref name="excludedColumn"/> from every row.
    /// </summary>
    /// <param name="vectors">The <c>n-1</c> input vectors of dimension <c>n</c>.</param>
    /// <param name="excludedColumn">Index of the component to drop from every row.</param>
    /// <returns>The determinant of the resulting <c>(n-1)x(n-1)</c> minor.</returns>
    private static T MinorDeterminant(Vector<T>[] vectors, int excludedColumn)
    {
        int n = vectors.Length;
        T[,] minor = new T[n, n];
        for (int row = 0; row < n; row++)
        {
            int col = 0;
            for (int component = 0; component < n + 1; component++)
            {
                if (component == excludedColumn) continue;
                minor[row, col] = vectors[row].components[component];
                col++;
            }
        }
        return new Matrix<T>(minor).Determinant;
    }

    /// <summary>
    /// Projects this vector onto <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The vector to project onto. Must be non-zero.</param>
    /// <returns>The projection of this vector in the direction of <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the vectors have different dimensions.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="other"/> is the zero vector.</exception>
    public Vector<T> ProjectOnto(Vector<T> other)
    {
        if (Dimension != other.Dimension)
            throw new ArgumentException("Vectors must have the same dimension.", nameof(other));
        T denominator = other * other;
        if (denominator == T.Zero)
            throw new InvalidOperationException("Cannot project onto the zero vector.");
        return ((this * other) / denominator) * other;
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

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)components).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

