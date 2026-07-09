using System.Numerics;
using Utils.Geography.Model;
using Utils.Mathematics;

namespace Utils.Geography.Projections;

/// <summary>
/// Implements the stereographic map projection for spherical coordinates, centered at
/// (lat=0°, lon=0°).
/// </summary>
/// <remarks>
/// This projection has a single true singularity: the antipodal point (lat=0°, lon=±180°), which is
/// the "point at infinity" for a stereographic projection centered at (0°, 0°) — there is no finite
/// (x, y) that correctly represents it. <see cref="GeoPointToMapPoint"/> does not throw at that point;
/// instead it substitutes <c>T.Epsilon</c> (the smallest positive value representable by <typeparamref name="T"/>)
/// for the zero denominator, which produces an extremely large but finite <c>k</c> factor and, in turn,
/// extremely large (not NaN, but potentially positive infinity after overflow) <c>x</c>/<c>y</c> values
/// rather than a clear error. Callers that need points near the antipodal meridian should check for this
/// case explicitly (e.g. <c>degree.AreEqual(geoPoint.Latitude, 0, tolerance) &amp;&amp;
/// degree.AreEqual(System.Math.Abs(geoPoint.Longitude), 180, tolerance)</c>) rather than relying on the
/// output being finite and meaningful.
/// </remarks>
/// <typeparam name="T">Floating-point type used for calculations.</typeparam>
public class StereographicProjection<T> : IProjectionTransformation<T>
        where T : struct, IFloatingPointIeee754<T>
{
    /// <summary>
    /// Provides trigonometric helpers that operate on degree values.
    /// </summary>
    private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

    /// <inheritdoc/>
    /// <remarks>
    /// Unlike Mercator, this projection's singularity is not at the poles (both poles are finite:
    /// ρ=1) but at the antipodal point (lat=0°, lon=±180°) — see the class remarks. Because that
    /// singularity is a single point rather than a well-defined latitude cutoff, there is no clean
    /// finite envelope that fully contains every representable point the way there is for the other
    /// projections in this package. <see cref="Bounds"/> reports the finite extent reached at the
    /// poles (x,y ∈ [-1,1]); points near the antipodal meridian can legitimately fall well outside it,
    /// and <see cref="IProjectionTransformation{T}.Normalize"/> will report a value outside <c>[0,1]</c>
    /// for them rather than clamping.
    /// </remarks>
    public (T MinX, T MaxX, T MinY, T MaxY) Bounds => (-T.One, T.One, -T.One, T.One);

    /// <summary>
    /// Projects geographic coordinates onto the stereographic projection plane.
    /// </summary>
    /// <param name="geoPoint">Geographic coordinates in degrees.</param>
    /// <returns>Projected map coordinates.</returns>
    public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geoPoint)
    {
        T lat = geoPoint.Latitude;
        T lon = geoPoint.Longitude;

        // k = 1 / [1 + cos(lat)*cos(lon)]
        T cosLat = degree.Cos(lat);
        T cosLon = degree.Cos(lon);
        T denom = T.One + (cosLat * cosLon);
        // denom is exactly zero only at the antipodal point (lat=0, lon=+-180): see the class remarks
        // for why this substitutes a near-zero denominator instead of throwing.
        if (T.IsZero(denom)) denom = T.Epsilon;
        T k = T.One / denom;

        T sinLat = degree.Sin(lat);
        T sinLon = degree.Sin(lon);

        // x = k*cos(lat)*sin(lon)
        // y = k*sin(lat)
        T x = k * cosLat * sinLon;
        T y = k * sinLat;

        return new ProjectedPoint<T>(x, y, this);
    }

    /// <summary>
    /// Converts stereographic projection coordinates back to geographic coordinates.
    /// </summary>
    /// <param name="mapPoint">Projected map coordinates.</param>
    /// <returns>Geographic coordinates in degrees.</returns>
    public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mapPoint)
    {
        T x = mapPoint.X;
        T y = mapPoint.Y;

        // ρ = sqrt(x^2 + y^2)
        T rho = T.Sqrt(x * x + y * y);
        if (T.IsZero(rho))
        {
            // (x=0,y=0) => lat=0, lon=0
            return new GeoPoint<T>(T.Zero, T.Zero);
        }

        // c = 2 * atan(ρ)
        // We'll do it in degrees, so we use degree.Atan
        T c = T.CreateChecked(2) * degree.Atan(rho);

        // lat = asin( (y*sin(c)) / ρ )
        T sinC = degree.Sin(c);
        T latFactor = (y * sinC) / rho;
        if (latFactor > T.One) latFactor = T.One;
        if (latFactor < -T.One) latFactor = -T.One;
        T lat = degree.Asin(latFactor);

        // lon = atan2( x*sin(c), ρ*cos(c) )
        T cosC = degree.Cos(c);
        T lon = degree.Atan2(x * sinC, rho * cosC);

        return new GeoPoint<T>(lat, lon);
    }
}
