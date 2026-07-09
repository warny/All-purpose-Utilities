using System.Numerics;
using Utils.Geography.Model;
using Utils.Mathematics;

namespace Utils.Geography.Projections;

/// <summary>
/// Implements the Miller cylindrical map projection.
/// </summary>
/// <typeparam name="T">Floating-point type used for calculations.</typeparam>
public class MillerProjection<T> : IProjectionTransformation<T>
        where T : struct, IFloatingPointIeee754<T>
{
    /// <summary>
    /// Provides trigonometric helpers that operate on degree values.
    /// </summary>
    private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

    /// <inheritdoc/>
    /// <remarks>
    /// Unlike Mercator, Miller's Y is finite even at the poles: the "inside" tangent argument is scaled
    /// down to <c>45° + (2/5)·lat</c>, which only reaches <c>81°</c> at lat=±90° (well short of the 90°
    /// singularity), so no practical cutoff is needed here.
    /// </remarks>
    public (T MinX, T MaxX, T MinY, T MaxY) Bounds
    {
        get
        {
            T maxY = GeoPointToMapPoint(new GeoPoint<T>(degree.RightAngle, T.Zero)).Y;
            return (-degree.StraightAngle, degree.StraightAngle, -maxY, maxY);
        }
    }

    /// <summary>
    /// Projects geographic coordinates to Miller cylindrical coordinates.
    /// </summary>
    /// <param name="geoPoint">Geographic coordinates in degrees.</param>
    /// <returns>Projected map coordinates.</returns>
    public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geoPoint)
    {
        T lon = geoPoint.Longitude;
        T lat = geoPoint.Latitude;

        // y = (5/4)*ln(tan(45 + (2/5)*lat))
        T inside = T.CreateChecked(45) + (lat * T.CreateChecked(2) / T.CreateChecked(5));
        T tanVal = degree.Tan(inside);
        T y = (T.CreateChecked(5) / T.CreateChecked(4)) * T.Log(tanVal);

        // x = longitude
        return new ProjectedPoint<T>(lon, y, this);
    }

    /// <summary>
    /// Converts Miller cylindrical coordinates back to geographic coordinates.
    /// </summary>
    /// <param name="mapPoint">Projected map coordinates.</param>
    /// <returns>Geographic coordinates in degrees.</returns>
    public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mapPoint)
    {
        T x = mapPoint.X;
        T y = mapPoint.Y;

        // lat = (5/2)*(atan(exp(4y/5)) - 45°)
        T exponent = T.Exp((T.CreateChecked(4) * y) / T.CreateChecked(5));
        T angleDeg = degree.Atan(exponent); // returns degrees
        T lat = (T.CreateChecked(5) / T.CreateChecked(2)) * (angleDeg - T.CreateChecked(45));

        T lon = x;
        return new GeoPoint<T>(lat, lon);
    }
}
