using System;
using System.Numerics;
using Utils.Geography.Model;

namespace Utils.Geography.Projections;

/// <summary>
/// Represents a generic transformation interface that converts
/// <see cref="GeoPoint{T}"/> objects to <see cref="ProjectedPoint{T}"/> objects and vice versa.
/// </summary>
/// <typeparam name="T">
/// Numeric type implementing <see cref="IFloatingPointIeee754{T}"/>.
/// </typeparam>
public interface IProjectionTransformation<T>
    where T : struct, IFloatingPointIeee754<T>
{
    /// <summary>
    /// Transforms a geographic point (latitude/longitude) into a projected map point.
    /// </summary>
    /// <param name="geoPoint">A geographic point.</param>
    /// <returns>The corresponding projected point on a 2D map.</returns>
    ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geoPoint);

    /// <summary>
    /// Transforms a projected map point back into a geographic point (latitude/longitude).
    /// </summary>
    /// <param name="mapPoint">A projected point on a 2D map.</param>
    /// <returns>The corresponding geographic point.</returns>
    GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mapPoint);
}

/// <summary>
/// Represents an abstract base class for map projection transformations
/// that convert between geographic (<see cref="GeoPoint{T}"/>) and projected
/// (<see cref="ProjectedPoint{T}"/>) coordinates.
/// </summary>
/// <typeparam name="T">
/// Numeric type implementing <see cref="IFloatingPointIeee754{T}"/>.
/// </typeparam>
public abstract class ProjectionTransformation<T> :
    IProjectionTransformation<T>,
    IEquatable<ProjectionTransformation<T>>,
    IEqualityOperators<ProjectionTransformation<T>, ProjectionTransformation<T>, bool>
    where T : struct, IFloatingPointIeee754<T>
{
    /// <inheritdoc/>
    public abstract ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geoPoint);

    /// <inheritdoc/>
    public abstract GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mapPoint);

    /// <inheritdoc/>
    public override bool Equals(object obj)
        => obj switch
        {
            ProjectionTransformation<T> other => Equals(other),
            _ => false
        };

    /// <summary>
    /// Determines whether the specified <paramref name="other"/> projection transformation
    /// is equal to the current one. The default implementation simply checks
    /// if both are of the exact same runtime type.
    /// </summary>
    /// <param name="other">Another <see cref="ProjectionTransformation{T}"/> to compare.</param>
    /// <returns>
    /// <see langword="true"/> if both instances are of the same runtime type; otherwise <see langword="false"/>.
    /// </returns>
    public virtual bool Equals(ProjectionTransformation<T> other)
        => other is not null && this.GetType() == other.GetType();

    /// <inheritdoc/>
    public override int GetHashCode()
        => this.GetType().Name.GetHashCode(StringComparison.Ordinal);


    /// <summary>
    /// Equality operator for <see cref="ProjectionTransformation{T}"/>.
    /// </summary>
    /// <param name="p1">The first transformation.</param>
    /// <param name="p2">The second transformation.</param>
    /// <returns><see langword="true"/> if both transformations are equal; otherwise <see langword="false"/>.</returns>
    public static bool operator ==(ProjectionTransformation<T> p1, ProjectionTransformation<T> p2)
        => p1?.Equals(p2) ?? p2 is null;

    /// <summary>
    /// Inequality operator for <see cref="ProjectionTransformation{T}"/>.
    /// </summary>
    /// <param name="p1">The first transformation.</param>
    /// <param name="p2">The second transformation.</param>
    /// <returns><see langword="true"/> if the transformations differ; otherwise <see langword="false"/>.</returns>
    public static bool operator !=(ProjectionTransformation<T> p1, ProjectionTransformation<T> p2)
        => !(p1 == p2);
}
