using System.Numerics;
using Utils.Geography.Model;
using Utils.Mathematics;

namespace Utils.Geography.Projections;

/// <summary>
/// Implements the polar aspect of the Lambert azimuthal equal-area projection for spherical
/// coordinates: the projection is centered on the north pole (latitude=90°), which maps to
/// (x=0, y=0), not on the equator/prime meridian.
/// </summary>
/// <typeparam name="T">Floating-point type used for calculations.</typeparam>
public class LambertAzimuthalEqualArea<T> : IProjectionTransformation<T>
        where T : struct, IFloatingPointIeee754<T>
{
    /// <summary>
    /// Provides trigonometric helpers that operate on degree values.
    /// </summary>
    private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

    /// <summary>
    /// Gets the precomputed square root of two used by the projection formula.
    /// </summary>
    private static T Sqrt2 { get; } = T.Sqrt(T.CreateChecked(2));

    /// <inheritdoc/>
    /// <remarks>
    /// Both axes are finite over the whole sphere for this projection: ρ (and therefore both x and y)
    /// reaches its maximum, finite value of exactly 2 at the south pole (lat=-90°, the point diametrically
    /// opposite this projection's center), so no practical cutoff is needed here (unlike, e.g., Mercator).
    /// Because this is a polar azimuthal projection, x and y do not independently track longitude/latitude
    /// the way they do for cylindrical projections — see <see cref="IProjectionTransformation{T}.Normalize"/>.
    /// </remarks>
    public (T MinX, T MaxX, T MinY, T MaxY) Bounds
    {
        get
        {
            T two = T.CreateChecked(2);
            return (-two, two, -two, two);
        }
    }

    /// <summary>
    /// Projects geographic coordinates onto the Lambert azimuthal equal-area plane.
    /// </summary>
    /// <param name="geoPoint">Geographic coordinates in degrees.</param>
    /// <returns>Projected map coordinates.</returns>
    public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geoPoint)
    {
        // lat, lon in degrees
        T lat = geoPoint.Latitude;
        T lon = geoPoint.Longitude;

        // ρ = √2 * sqrt(1 - sin(lat))
        T sinLat = degree.Sin(lat);
        T factor = T.One - sinLat;
        if (factor < T.Zero) factor = T.Zero; // clamp if needed
        T rho = Sqrt2 * T.Sqrt(factor);

        T sinLon = degree.Sin(lon);
        T cosLon = degree.Cos(lon);

        // x = ρ sin(lon)
        // y = -ρ cos(lon)
        T x = rho * sinLon;
        T y = -rho * cosLon;

        return new ProjectedPoint<T>(x, y, this);
    }

    /// <summary>
    /// Converts Lambert azimuthal equal-area coordinates back to geographic coordinates.
    /// </summary>
    /// <param name="mapPoint">Projected map coordinates.</param>
    /// <returns>Geographic coordinates in degrees.</returns>
    public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mapPoint)
    {
        T x = mapPoint.X;
        T y = mapPoint.Y;

        // ρ = sqrt(x^2 + y^2)
        T rho = T.Sqrt(x * x + y * y);

        // C = (ρ^2) / 2
        T c = (rho * rho) / T.CreateChecked(2);

        // lat = asin(1 - C)
        T latFactor = T.One - c;
        if (latFactor > T.One) latFactor = T.One;
        if (latFactor < -T.One) latFactor = -T.One;
        T lat = degree.Asin(latFactor);

        // lon = atan2(x, -y)
        // so if y=0 => watch sign of x
        T lon = degree.Atan2(x, -y);

        return new GeoPoint<T>(lat, lon);
    }

}
