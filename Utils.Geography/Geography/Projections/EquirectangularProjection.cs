using System;
using System.Numerics;
using Utils.Geography.Model;
using Utils.Geography.Projections;
using Utils.Mathematics;

namespace Utils.Geography.Projections;

/// <summary>
/// A simple Equirectangular (Plate Carrée) projection that uses degrees throughout:
/// 
///   X = longitude (in degrees)
///   Y = latitude (in degrees)
/// 
/// Hence (0°, 0°) => (0,0) with no offsets or scalings. If latitude exceeds ±90,
/// it is optionally clamped.
/// </summary>
/// <typeparam name="T">
/// A numeric type implementing IFloatingPointIeee754 (e.g., float, double, decimal).
/// </typeparam>
public class EquirectangularProjection<T> : IProjectionTransformation<T>
    where T : struct, IFloatingPointIeee754<T>
{
    // We'll use your degree-based trigonometry helper.
    private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

    /// <summary>
    /// Maximum absolute latitude, typically ±90° for Earth.
    /// </summary>
    private static readonly T MaxLatitude = degree.RightAngle;

    /// <summary>
    /// Clamps latitude to [-90..+90] if desired; otherwise you can remove or adjust this.
    /// </summary>
    private static T ClampLatitude(T latitude) => MathEx.Clamp(latitude, -MaxLatitude, MaxLatitude);

    /// <inheritdoc/>
    /// <remarks>
    /// Both axes are finite over the whole sphere for this projection (X=longitude ∈ [-180°,180°],
    /// Y=latitude ∈ [-90°,90°]), so no practical cutoff is needed here (unlike, e.g., Mercator).
    /// </remarks>
    public (T MinX, T MaxX, T MinY, T MaxY) Bounds
        => (-degree.StraightAngle, degree.StraightAngle, -MaxLatitude, MaxLatitude);

    /// <summary>
    /// Projects a geographic point (lat, lon in degrees) to a 2D plane:
    ///   X = longitude, Y = latitude.
    /// </summary>
    public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geopoint)
    {
        // Optionally clamp lat to [-90..+90]
        T lat = ClampLatitude(geopoint.Latitude);

        // You may also want to normalize longitude to [-180..+180],
        // or mod 360, etc. We'll keep it as-is:
        T lon = geopoint.Longitude;

        // Equirectangular: (X, Y) = (longitude, latitude).
        return new ProjectedPoint<T>(lon, lat, this);
    }

    /// <summary>
    /// Inverse: Unprojects a point (X, Y) back to geographic coordinates (lat, lon in degrees):
    ///   longitude = X, latitude = Y.
    /// </summary>
    public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mappoint)
    {
        // Possibly clamp Y to ±90:
        T lat = ClampLatitude(mappoint.Y);
        // Keep X as is:
        T lon = mappoint.X;

        return new GeoPoint<T>(lat, lon);
    }
}
