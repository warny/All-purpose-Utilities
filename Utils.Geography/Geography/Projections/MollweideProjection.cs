using System;
using System.Numerics;
using Utils.Geography.Model;
using Utils.Geography.Projections;
using Utils.Mathematics;

namespace Utils.Geography.Projections;

/// <summary>
/// Mollweide (Homalographic) equal-area projection, implemented with degree-based trigonometry.
/// 
/// Lat/Lon in degrees =&gt; (x,y) in "Mollweide units" with:
///   - (0°,0°) maps to (0,0).
///   - The bounding ellipse is roughly x ∈ [-2√2..+2√2], y ∈ [-√2..+√2].
///   
/// Usage:
///   var projector = new MollweideProjectionDegrees&lt;double&gt;();
///   var projected = projector.GeoPointToMapPoint(new GeoPoint&lt;double&gt;(latDeg, lonDeg));
///   var unprojected = projector.MapPointToGeoPoint(projected);
/// </summary>
/// <typeparam name="T">
/// A floating point type implementing IFloatingPointIeee754, e.g. float/double/decimal.
/// </typeparam>
public class MollweideProjection<T> : IProjectionTransformation<T>
    where T : struct, IFloatingPointIeee754<T>
{
    // We'll use "degree" so that degree.Sin(45) = sin(45°) in normal radians internally.
    private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

    // For iteration tolerance
    private static readonly T Eps = T.CreateChecked(1.0e-7);
    private const int MaxIter = 10;

    /// <inheritdoc/>
    /// <remarks>
    /// Both axes are finite over the whole sphere for this projection (the bounding ellipse fits
    /// exactly within x ∈ [-2√2, 2√2], y ∈ [-√2, √2], reached at lon=±180°/lat=0° and lat=±90°
    /// respectively), so no practical cutoff is needed here (unlike, e.g., Mercator).
    /// </remarks>
    public (T MinX, T MaxX, T MinY, T MaxY) Bounds
    {
        get
        {
            T two = T.CreateChecked(2);
            T maxX = two * Sqrt2;
            return (-maxX, maxX, -Sqrt2, Sqrt2);
        }
    }

    /// <summary>
    /// Projects (latitude, longitude) in degrees => Mollweide (x, y).
    ///
    /// The Newton solve and trigonometry are carried out in radians (the mathematically correct
    /// unit for the classic Mollweide equation); latitude/longitude are only converted to/from
    /// degrees at the boundary of this method.
    ///
    /// Forward formula:
    ///   (1) Solve 2θ + sin(2θ) = π * sin(lat), for θ ∈ [-π/2..+π/2].
    ///   (2) x = (2√2 / π) * lon * cos(θ)
    ///   (3) y = √2 * sin(θ)
    /// </summary>
    public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geoPoint)
    {
        T latRad = degree.ToRadian(geoPoint.Latitude);
        T lonRad = degree.ToRadian(geoPoint.Longitude);

        // 1) Solve for θ using Newton's method, starting from θ₀=lat (a close approximation
        //    that keeps the iteration stable near the poles, where the naive target/2 guess
        //    can overshoot and fail to converge within the iteration budget):
        T target = T.Pi * T.Sin(latRad);
        T theta = SolveTheta(target, latRad);

        // 2) x = (2√2/π) * lon * cos(θ)
        // 3) y = √2 * sin(θ)
        T cosTheta = T.Cos(theta);
        T sinTheta = T.Sin(theta);

        T x = (T.CreateChecked(2) * Sqrt2 / T.Pi) * lonRad * cosTheta;
        T y = Sqrt2 * sinTheta;

        return new ProjectedPoint<T>(x, y, this);
    }

    /// <summary>
    /// Unprojects (x,y) in Mollweide => (lat, lon) in degrees.
    ///
    /// Inverse (all computed in radians, converted to degrees only for the result):
    ///   (1) θ = asin( y / √2 )
    ///   (2) lon = x * (π / 2√2 cos(θ))
    ///   (3) lat = asin( [2θ + sin(2θ)] / π )
    /// </summary>
    public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mapPoint)
    {
        T x = mapPoint.X;
        T y = mapPoint.Y;

        // 1) θ = asin(y/√2)
        T ratio = y / Sqrt2;
        // If ratio is outside [-1..1], clamp it
        if (ratio > T.One) ratio = T.One;
        else if (ratio < -T.One) ratio = -T.One;

        T theta = T.Asin(ratio);
        T cosTheta = T.Cos(theta);

        // 2) lon = x / [ (2√2/π) * cos(θ) ] => lon = x * π / (2√2 cos(θ))
        T lonRad = T.Zero;
        if (!T.IsZero(cosTheta))
        {
            lonRad = x * T.Pi / (T.CreateChecked(2) * Sqrt2 * cosTheta);
        }

        // 3) lat => from sin(lat) = [2θ + sin(2θ)] / π
        T twoTheta = theta + theta;
        T sin2θ = T.Sin(twoTheta);
        T latFactor = (twoTheta + sin2θ) / T.Pi;

        // clamp if out of [-1..1]
        if (latFactor > T.One) latFactor = T.One;
        else if (latFactor < -T.One) latFactor = -T.One;

        T latRad = T.Asin(latFactor);

        return new GeoPoint<T>(degree.FromRadian(latRad), degree.FromRadian(lonRad));
    }

    /// <summary>
    /// Solve 2θ + sin(2θ) = target, with θ in radians.
    ///
    /// Newton iteration:
    ///   f(θ) = 2θ + sin(2θ) - target
    ///   f'(θ) = 2 + 2 cos(2θ)
    /// </summary>
    /// <param name="target">Right-hand side of the equation, π*sin(lat).</param>
    /// <param name="initialGuess">
    /// Starting value for θ. Using the latitude itself (rather than <paramref name="target"/>/2)
    /// keeps the iteration stable near the poles, where the naive guess can overshoot into a
    /// region with a near-zero derivative and fail to converge within <see cref="MaxIter"/> steps.
    /// </param>
    private static T SolveTheta(T target, T initialGuess)
    {
        T theta = initialGuess;

        for (int i = 0; i < MaxIter; i++)
        {
            // f(θ)=2θ + sin(2θ) - target
            T twoTheta = theta + theta;
            T sin2θ = T.Sin(twoTheta);
            T f = twoTheta + sin2θ - target;

            // f'(θ)=2 + 2 cos(2θ)
            T cos2θ = T.Cos(twoTheta);
            T fprime = T.CreateChecked(2) + (T.CreateChecked(2) * cos2θ);

            if (T.IsZero(fprime))
                break; // degenerate case, not likely unless θ=±π/2

            T dθ = f / fprime;
            theta -= dθ;

            if (T.Abs(dθ) < Eps)
                break;
        }

        return theta;
    }

    /// <summary>
    /// Helper that returns √2 in type T, so we don't keep converting or re-computing.
    /// </summary>
    private readonly static T Sqrt2 = T.Sqrt(T.CreateChecked(2));
}
