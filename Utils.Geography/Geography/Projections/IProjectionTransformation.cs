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

    /// <summary>
    /// Gets the rectangular bounds, expressed in this projection's own native output units, that
    /// <see cref="GeoPointToMapPoint"/> stays within for every valid <see cref="GeoPoint{T}"/>. Used by
    /// <see cref="Normalize"/> (and, in turn, by <see cref="Utils.Geography.Display.MapPoint{T}"/> and
    /// <see cref="Utils.Geography.Display.RepresentationConverter{T}"/>) to convert a projected point into
    /// map-fraction coordinates before scaling to a pixel/tile grid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a projection whose output is genuinely unbounded at some point of the sphere (e.g. Mercator at
    /// the poles), <see cref="Bounds"/> must report a practical, finite envelope rather than the true
    /// (infinite) mathematical range — see each projection's own documentation for how that envelope is
    /// chosen and what it excludes.
    /// </para>
    /// <para>
    /// This member has a default implementation (throwing <see cref="NotSupportedException"/>) rather
    /// than being required, so that adding it to this public interface does not break existing
    /// third-party <see cref="IProjectionTransformation{T}"/> implementations: they keep compiling and
    /// working exactly as before, and only fail — with a clear message — if they (or code that consumes
    /// them, like <see cref="Normalize"/>, <see cref="Utils.Geography.Display.MapPoint{T}"/>'s
    /// <see cref="ProjectedPoint{T}"/> constructor, or <see cref="Utils.Geography.Display.RepresentationConverter{T}.MappointToTile"/>)
    /// actually try to use it without overriding it. All 7 projections built into this package override it.
    /// </para>
    /// </remarks>
    (T MinX, T MaxX, T MinY, T MaxY) Bounds
        => throw new NotSupportedException(
            $"{GetType().Name} does not override {nameof(IProjectionTransformation<T>)}.{nameof(Bounds)}, " +
            $"so it cannot be used with {nameof(Normalize)} or with the APIs that depend on it " +
            $"(MapPoint<T>'s ProjectedPoint constructor, RepresentationConverter<T>.MappointToTile).");

    /// <summary>
    /// Normalizes <paramref name="projectedPoint"/> into map-fraction coordinates using <see cref="Bounds"/>:
    /// <c>(MinX, MinY)</c> maps to <c>(0, 0)</c> and <c>(MaxX, MaxY)</c> maps to <c>(1, 1)</c>, independently
    /// on each axis.
    /// </summary>
    /// <param name="projectedPoint">A point produced by <see cref="GeoPointToMapPoint"/> using this projection.</param>
    /// <returns>
    /// The normalized <c>(X, Y)</c> coordinates. For points within <see cref="Bounds"/> these are in
    /// <c>[0, 1]</c>; a point outside <see cref="Bounds"/> (e.g. near an excluded singularity, see its
    /// remarks) normalizes outside that range rather than being clamped.
    /// </returns>
    /// <remarks>
    /// This is a plain independent linear rescale of each axis: it does not assume that either axis
    /// corresponds to a cardinal direction (that assumption only holds for cylindrical/pseudo-cylindrical
    /// projections such as <see cref="MercatorProjection{T}"/>/<see cref="EquirectangularProjection{T}"/>,
    /// not for azimuthal ones such as <see cref="LambertAzimuthalEqualArea{T}"/>/<see cref="StereographicProjection{T}"/>
    /// where a single axis does not track latitude alone). Callers that need a specific tile-numbering
    /// orientation (e.g. row 0 = north, matching common slippy-map conventions) must apply that convention
    /// themselves on top of this normalized value.
    /// </remarks>
    (T X, T Y) Normalize(ProjectedPoint<T> projectedPoint)
    {
        var (minX, maxX, minY, maxY) = Bounds;
        T x = (projectedPoint.X - minX) / (maxX - minX);
        T y = (projectedPoint.Y - minY) / (maxY - minY);
        return (x, y);
    }
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

    // Bounds is intentionally not redeclared here: leaving it out means every existing subclass of
    // this base class automatically falls back to IProjectionTransformation<T>.Bounds' default
    // (throwing) implementation, exactly like a class that doesn't implement the interface member
    // directly — no breaking change for anyone who already derived from ProjectionTransformation<T>.
    // Subclasses that want Normalize/Bounds support can still override Bounds directly.

    /// <inheritdoc/>
    public override bool Equals(object? obj)
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
    public virtual bool Equals(ProjectionTransformation<T>? other)
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
    public static bool operator ==(ProjectionTransformation<T>? p1, ProjectionTransformation<T>? p2)
        => p1?.Equals(p2) ?? p2 is null;

    /// <summary>
    /// Inequality operator for <see cref="ProjectionTransformation{T}"/>.
    /// </summary>
    /// <param name="p1">The first transformation.</param>
    /// <param name="p2">The second transformation.</param>
    /// <returns><see langword="true"/> if the transformations differ; otherwise <see langword="false"/>.</returns>
    public static bool operator !=(ProjectionTransformation<T>? p1, ProjectionTransformation<T>? p2)
        => !(p1 == p2);
}
