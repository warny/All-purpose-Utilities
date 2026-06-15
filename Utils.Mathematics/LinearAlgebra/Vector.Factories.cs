namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Static factory methods for <see cref="Vector{T}"/>.
/// </summary>
public sealed partial class Vector<T>
{
    /// <summary>
    /// Returns a zero vector of the specified dimension.
    /// </summary>
    /// <param name="dimensions">Number of dimensions.</param>
    public static Vector<T> Zero(int dimensions)
    {
        if (dimensions <= 0) throw new ArgumentException("Vector dimension cannot be 0", nameof(dimensions));
        return new Vector<T>(dimensions);
    }

    /// <summary>
    /// Returns a unit vector along the specified axis.
    /// </summary>
    /// <param name="axis">Zero-based index of the axis that carries the value 1.</param>
    /// <param name="dimensions">Total number of dimensions.</param>
    public static Vector<T> Unit(int axis, int dimensions)
    {
        if (dimensions <= 0) throw new ArgumentException("Vector dimension cannot be 0", nameof(dimensions));
        ArgumentOutOfRangeException.ThrowIfNegative(axis);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(axis, dimensions);
        var v = new Vector<T>(dimensions);
        v.components[axis] = T.One;
        return v;
    }
}
