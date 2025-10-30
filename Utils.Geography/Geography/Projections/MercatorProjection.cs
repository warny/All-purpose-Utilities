using System;
using System.Numerics;
using Utils.Geography.Model;
using Utils.Geography.Projections;
using Utils.Mathematics;

namespace Utils.Geography.Projections;

/// <summary>
/// centered Mercator projection where (lat=0°, lon=0°) maps to (X=0, Y=0),
/// 
/// Forward transform: 
///   X = lon
///   Y = ln( tan(45° + lat/2) )
/// 
/// Inverse transform:
///   lon = X
///   lat = 2 * [ atan( exp(Y) ) - 45° ]
/// </summary>
/// <typeparam name="T">A floating-point type implementing IFloatingPointIeee754.</typeparam>
public class MercatorProjection<T> : IProjectionTransformation<T>
    where T : struct, IFloatingPointIeee754<T>
{
    private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

    /// <summary>
    /// Projects a geographic point (latitude/longitude in degrees) into a 2D "Mercator in degrees"
    /// space so that (0°,0°) → (0,0).
    /// </summary>
    /// <param name="geoPoint">The geographic point in degrees.</param>
    /// <returns>A <see cref="ProjectedPoint{T}"/> with X=lon and Y=ln(tan(45 + lat/2)).</returns>
    public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geoPoint)
    {
        // X = lon directly
        T x = geoPoint.Longitude;

        // Y = ln(tan(45° + lat/2))
        // Let's ensure we remain in valid domain for tan( ).
        // If lat ~ ±90°, tan(45 + 90/2=90) => tan(135) => negative
        // But mathematically it's correct for standard Mercator explosion.
        T halfLat = geoPoint.Latitude / T.CreateChecked(2);
        T angleForTan = T.CreateChecked(45) + halfLat; // in degrees
                                                       // compute tan(angleForTan) in degrees
        T tangent = degree.Tan(angleForTan);
        T y = T.Log(tangent);

        return new ProjectedPoint<T>(x, y, this);
    }

    /// <summary>
    /// Unprojects a map point (X, Y) back to latitude/longitude in degrees.
    /// X is treated as longitude, Y as ln(tan(45 + lat/2)).
    /// </summary>
    /// <param name="mapPoint">Projected point with X=lon, Y=ln(tan(45+lat/2)).</param>
    /// <returns>A geographic point in degrees.</returns>
    public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mapPoint)
    {
        // lon = X
        T lon = mapPoint.X;

        // Y = ln(tan(45 + lat/2)) => lat/2 = arctan(exp(Y)) - 45
        T eY = T.Exp(mapPoint.Y);
        T angleDeg = degree.Atan(eY); // returns an angle in degrees
        T halfLat = angleDeg - T.CreateChecked(45);
        T lat = halfLat * T.CreateChecked(2);

        return new GeoPoint<T>(lat, lon);
    }
}
