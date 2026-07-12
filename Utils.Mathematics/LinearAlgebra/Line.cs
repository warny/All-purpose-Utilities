using System;
using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Represents a line in an n-dimensional space.
/// </summary>
/// <typeparam name="T">Floating-point type.</typeparam>
public class Line<T> : IFormattable, IEquatable<Line<T>>, ICloneable
    where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, IRootFunctions<T>
{
    /// <summary>
    /// A point on the line.
    /// </summary>
    public Vector<T> Point { get; }

    /// <summary>
    /// Direction vector of the line.
    /// </summary>
    public Vector<T> Direction { get; }

    /// <summary>
    /// Gets the dimension of the line.
    /// </summary>
    public int Dimension => Point.Dimension;

    /// <summary>
    /// Initializes a new instance of the <see cref="Line{T}"/> class.
    /// </summary>
    /// <param name="point">A point on the line.</param>
    /// <param name="direction">Direction vector of the line.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when vectors do not share the same dimension, or when <paramref name="direction"/> is zero
    /// or numerically negligible (a line has no well-defined direction in that case, and
    /// <see cref="DistanceTo"/> would divide by zero).
    /// </exception>
    public Line(Vector<T> point, Vector<T> direction)
    {
        if (point.Dimension != direction.Dimension)
            throw new ArgumentException("Point and direction must be of the same dimension.");

        // Reuses Vector<T>.Normalize's scale-aware zero/near-zero tolerance policy (see its
        // DefaultNormTolerance) instead of an independent threshold, per the "same numerical policy"
        // guidance in TODO-2026-07-11-pass3.md. The normalized copy itself is discarded: Direction keeps
        // its original (non-unit) scale, only used here to validate it is non-negligible.
        try
        {
            direction.Normalize();
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException("Line direction cannot be zero or numerically negligible.", nameof(direction), ex);
        }

        Point = point;
        Direction = direction;
    }

    /// <summary>
    /// Computes the distance from the line to the specified point.
    /// </summary>
    /// <param name="point">Point to compute the distance to.</param>
    /// <returns>The shortest distance between the line and the point.</returns>
    /// <exception cref="ArgumentException">Thrown when the point does not share the same dimension.</exception>
    public T DistanceTo(Vector<T> point)
    {
        if (point.Dimension != this.Point.Dimension)
            throw new ArgumentException("All vectors must have the same dimension.");

        // Vector from line point to target point
        var pq = point - this.Point;

        // Projection of PQ on the direction
        T t = (pq * this.Direction) / (this.Direction * this.Direction);
        var projection = t * this.Direction;

        // Closest point on the line
        var closestPoint = this.Point + projection;

        // Distance between the point and its projection
        var distanceVector = point - closestPoint;
        return distanceVector.Norm;
    }

    /// <summary>
    /// Creates a copy of the line.
    /// </summary>
    /// <returns>A new line with the same point and direction.</returns>
    public object Clone() => new Line<T>(new Vector<T>(Point), new Vector<T>(Direction));

    /// <summary>
    /// Determines whether the specified line is equal to the current line.
    /// </summary>
    /// <param name="other">Line to compare.</param>
    /// <returns><see langword="true"/> if equal; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This compares the exact stored representation (<see cref="Point"/> and <see cref="Direction"/>),
    /// not geometric equivalence: two lines that describe the same infinite line but were built from a
    /// different anchor point, or a direction scaled by a nonzero factor (including a negative one),
    /// compare unequal here. Use <see cref="IsGeometricallyEquivalentTo"/> for a tolerance-aware
    /// comparison of the geometric object instead.
    /// </remarks>
    public bool Equals(Line<T>? other)
        => other is not null && Point.Equals(other.Point) && Direction.Equals(other.Direction);

    /// <inheritdoc/>
    public override bool Equals(object? other)
        => other switch
        {
            Line<T> line => Equals(line),
            _ => false
        };

    /// <inheritdoc/>
    /// <remarks>Consistent with the exact representation equality of <see cref="Equals(Line{T})"/>.</remarks>
    public override int GetHashCode() => HashCode.Combine(Point, Direction);

    /// <summary>
    /// Determines whether this line and <paramref name="other"/> describe the same geometric line,
    /// independently of which point was used as the anchor or how the direction vector was scaled
    /// (including a negative scale, i.e. the opposite direction).
    /// </summary>
    /// <param name="other">Line to compare against.</param>
    /// <param name="tolerance">
    /// Tolerance applied to both the direction-parallelism check and the point-on-line distance check.
    /// Must be finite and non-negative; there is no implicit default, since an appropriate tolerance
    /// depends on the caller's own scale and precision requirements.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when both lines have the same dimension, parallel directions (within
    /// <paramref name="tolerance"/>), and <paramref name="other"/>'s anchor point lies on this line
    /// (within <paramref name="tolerance"/>); otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not finite or is negative.</exception>
    public bool IsGeometricallyEquivalentTo(Line<T> other, T tolerance)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!T.IsFinite(tolerance) || tolerance < T.Zero)
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be finite and non-negative.");

        if (Dimension != other.Dimension) return false;

        // Two directions are parallel (collinear, either same or opposite sense) exactly when the
        // magnitude of the dot product of their unit vectors is 1.
        var thisUnit = Direction.Normalize();
        var otherUnit = other.Direction.Normalize();
        T absCosine = T.Abs(thisUnit * otherUnit);
        if (T.Abs(absCosine - T.One) > tolerance) return false;

        return DistanceTo(other.Point) <= tolerance;
    }

    /// <summary>
    /// Returns a string representation of the line.
    /// </summary>
    /// <param name="format">Format string.</param>
    /// <param name="formatProvider">Format provider.</param>
    /// <returns>A string representation of the line.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider)
        => $"Point: {Point}, Direction: {Direction}";
}

