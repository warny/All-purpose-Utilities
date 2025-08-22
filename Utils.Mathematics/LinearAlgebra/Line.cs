using System;
using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Represents a line in an n-dimensional space.
/// </summary>
/// <typeparam name="T">Floating-point type.</typeparam>
public class Line<T> : IFormattable, IEquatable<Line<T>>, ICloneable
    where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
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
    /// <exception cref="ArgumentException">Thrown when vectors do not share the same dimension.</exception>
    public Line(Vector<T> point, Vector<T> direction)
    {
        if (point.Dimension != direction.Dimension)
            throw new ArgumentException("Point and direction must be of the same dimension.");

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
    /// <returns><c>true</c> if equal; otherwise, <c>false</c>.</returns>
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
    public override int GetHashCode() => HashCode.Combine(Point, Direction);

    /// <summary>
    /// Returns a string representation of the line.
    /// </summary>
    /// <param name="format">Format string.</param>
    /// <param name="formatProvider">Format provider.</param>
    /// <returns>A string representation of the line.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider)
        => $"Point: {Point}, Direction: {Direction}";
}

