using System;
using System.Numerics;
using Utils.Geography.Projections;
using Utils.Objects;

namespace Utils.Geography.Model
{
    /// <summary>
    /// Represents an immutable point in a projected coordinate system with an associated projection transformation.
    /// </summary>
    /// <typeparam name="T">A numeric type that supports IEEE 754 floating-point operations.</typeparam>
    public sealed class ProjectedPoint<T> : IEquatable<ProjectedPoint<T>>, IFormattable
        where T : struct, IFloatingPointIeee754<T>
    {
        /// <summary>
        /// The projection transformation associated with this point.
        /// </summary>
        public IProjectionTransformation<T> Projection { get; }

        /// <summary>
        /// The X coordinate of this point.
        /// </summary>
        public T X { get; }

        /// <summary>
        /// The Y coordinate of this point.
        /// </summary>
        public T Y { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectedPoint{T}"/> class.
        /// </summary>
        /// <param name="x">The X coordinate of the point.</param>
        /// <param name="y">The Y coordinate of the point.</param>
        /// <param name="projection">The projection transformation associated with this point.</param>
        public ProjectedPoint(T x, T y, IProjectionTransformation<T> projection)
        {
            Projection = projection ?? throw new ArgumentNullException(nameof(projection));
            X = x;
            Y = y;
        }

        /// <summary>
        /// Deconstructs the point into its X and Y coordinates.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        public void Deconstruct(out T x, out T y)
        {
            x = X;
            y = Y;
        }

        /// <summary>
        /// Deconstructs the point into its X and Y coordinates and the associated projection.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="projection">The associated projection transformation.</param>
        public void Deconstruct(out T x, out T y, out IProjectionTransformation<T> projection)
        {
            x = X;
            y = Y;
            projection = Projection;
        }

        /// <summary>
        /// Determines whether the specified <see cref="ProjectedPoint{T}"/> is equal to the current one.
        /// </summary>
        /// <param name="other">The other <see cref="ProjectedPoint{T}"/> to compare to.</param>
        /// <returns>True if the points are equal, false otherwise.</returns>
        public bool Equals(ProjectedPoint<T> other)
        {
            if (other is null) return false;
            return X.Equals(other.X) && Y.Equals(other.Y) && Projection.Equals(other.Projection);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ProjectedPoint<T> other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return ObjectUtils.ComputeHash(X, Y, Projection.GetHashCode());
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"x={X}, y={Y}, projection={Projection}";
        }

        /// <summary>
        /// Returns a string representation of the point with a specified format.
        /// </summary>
        /// <param name="format">A format string for the numeric values.</param>
        /// <param name="formatProvider">An object that provides culture-specific formatting information.</param>
        /// <returns>A formatted string representing the point.</returns>
        public string ToString(string format, IFormatProvider formatProvider)
        {
            return $"x={X.ToString(format, formatProvider)}, y={Y.ToString(format, formatProvider)}, projection={Projection}";
        }
    }
}
