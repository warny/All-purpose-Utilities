using System;
using System.Globalization;
using System.Numerics;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Geography.Model;

/// <summary>
/// Represents a vector of displacement on a spherical geodesic with a bearing (heading direction).
/// </summary>
/// <typeparam name="T">The numeric type used for calculations, typically a floating point.</typeparam>
/// <remarks>
/// Wraps a <see cref="GeoPoint{T}"/> (composition) rather than inheriting from it: a vector "has a"
/// position, it isn't itself a point. Besides being the more accurate model, this also sidesteps a real
/// bug the previous inheritance-based design had: <see cref="GeoPoint{T}.Equals(object?)"/> matches any
/// <see cref="GeoPoint{T}"/>-derived instance via <c>obj is GeoPoint&lt;T&gt;</c>, so comparing a bare
/// <see cref="GeoPoint{T}"/> against a <see cref="GeoVector{T}"/> at the same coordinates used to report
/// them as equal (silently ignoring the bearing) while their hash codes (one over lat/lon, the other over
/// lat/lon/bearing) could differ — breaking the <see cref="object.GetHashCode"/> contract for any
/// <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>/<see cref="System.Collections.Generic.HashSet{T}"/>
/// mixing both types. Composition makes that mismatch impossible: <see cref="GeoVector{T}"/> and
/// <see cref="GeoPoint{T}"/> are unrelated types and can no longer compare equal to each other.
/// </remarks>
public readonly struct GeoVector<T> : IEquatable<GeoVector<T>>, IFormattable, IUnaryNegationOperators<GeoVector<T>, GeoVector<T>>, IEqualityOperators<GeoVector<T>, GeoVector<T>, bool>
    where T : struct, IFloatingPointIeee754<T>, IDivisionOperators<T, T, T>
{
    /// <summary>
    /// Angle calculator configured to work in degrees.
    /// </summary>
    private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

    /// <summary>
    /// Floating-point comparer used for the default tolerance in <see cref="IsApproximately(GeoVector{T})"/>.
    /// </summary>
    private static readonly FloatingPointComparer<T> comparer = new(GeoPoint<T>.EqualityPrecision);

    /// <summary>
    /// The geographic position this vector originates from. Immutable.
    /// </summary>
    public GeoPoint<T> Point { get; }

    /// <summary>
    /// Bearing in degrees relative to north, measured clockwise. Immutable.
    /// </summary>
    public T Bearing { get; }

    /// <summary>
    /// Latitude in degrees (immutable). Alias for <see cref="Point"/>'s latitude.
    /// </summary>
    public T Latitude => Point.Latitude;

    /// <summary>
    /// Longitude in degrees (immutable). Alias for <see cref="Point"/>'s longitude.
    /// </summary>
    public T Longitude => Point.Longitude;

    /// <summary>
    /// Alias for Latitude.
    /// </summary>
    public T φ => Point.Latitude;

    /// <summary>
    /// Alias for Longitude.
    /// </summary>
    public T λ => Point.Longitude;

    /// <summary>
    /// Bearing in degrees relative to north, measured clockwise.
    /// This is an alias for the Bearing property.
    /// </summary>
    public T θ => Bearing;

    #region Constructors

    /// <summary>
    /// Creates a <see cref="GeoVector{T}"/> from a single string containing latitude, longitude, and bearing.
    /// </summary>
    /// <param name="coordinates">A string with three parts (latitude, longitude, bearing) separated by the culture's list separator.</param>
    /// <param name="cultureInfos">Optional cultures to parse; defaults to <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.InvariantCulture"/>.</param>
    /// <exception cref="ArgumentException">Thrown if parsing fails or the string is invalid.</exception>
    public GeoVector(string coordinates, params CultureInfo[] cultureInfos)
        : this(ParseVectorTuple(coordinates, cultureInfos))
    {
    }

    /// <summary>
    /// Private constructor that consumes an already-parsed (latitude, longitude, bearing) tuple, so that
    /// the source string is parsed only once (see <see cref="ParseVectorTuple"/>).
    /// </summary>
    /// <param name="parsed">The parsed latitude, longitude, and bearing.</param>
    private GeoVector((T latitude, T longitude, T bearing) parsed)
    {
        Point = new GeoPoint<T>(parsed.latitude, parsed.longitude);
        Bearing = parsed.bearing;
    }

    /// <summary>
    /// Parses <paramref name="coordinates"/> exactly once and returns latitude, longitude, and bearing together.
    /// </summary>
    private static (T latitude, T longitude, T bearing) ParseVectorTuple(string coordinates, CultureInfo[] cultureInfos)
    {
        var (latitude, longitude) = ParseVectorString(coordinates, cultureInfos, out T bearing);
        return (latitude, longitude, bearing);
    }

    /// <summary>
    /// Creates a <see cref="GeoVector{T}"/> from a <paramref name="geoPoint"/> plus a given <paramref name="bearing"/>.
    /// </summary>
    /// <param name="geoPoint">Base geographic point (latitude/longitude).</param>
    /// <param name="bearing">Heading direction in degrees.</param>
    public GeoVector(GeoPoint<T> geoPoint, T bearing)
    {
        Point = geoPoint;
        Bearing = degree.Normalize0To2Max(bearing);
    }

    /// <summary>
    /// Creates a <see cref="GeoVector{T}"/> from two geographic points, calculating the bearing from <paramref name="geoPoint"/> to <paramref name="destination"/>.
    /// </summary>
    /// <param name="geoPoint">Start point.</param>
    /// <param name="destination">Destination point.</param>
    public GeoVector(GeoPoint<T> geoPoint, GeoPoint<T> destination)
    {
        Point = geoPoint;
        Bearing = ComputeBearing(geoPoint, destination);
    }

    /// <summary>
    /// Creates a <see cref="GeoVector{T}"/> from numeric latitude, longitude, and bearing.
    /// </summary>
    /// <param name="latitude">Latitude in degrees.</param>
    /// <param name="longitude">Longitude in degrees.</param>
    /// <param name="bearing">Heading direction in degrees.</param>
    public GeoVector(T latitude, T longitude, T bearing)
    {
        Point = new GeoPoint<T>(latitude, longitude);
        Bearing = degree.Normalize0To2Max(bearing);
    }

    /// <summary>
    /// Creates a <see cref="GeoVector{T}"/> from string representations of latitude, longitude, and a numeric bearing.
    /// </summary>
    /// <param name="latitudeString">Latitude string.</param>
    /// <param name="longitudeString">Longitude string.</param>
    /// <param name="bearing">Bearing direction in degrees.</param>
    /// <param name="cultureInfos">Culture information used to parse the coordinates.</param>
    public GeoVector(string latitudeString, string longitudeString, T bearing, params CultureInfo[] cultureInfos)
    {
        Point = new GeoPoint<T>(latitudeString, longitudeString, cultureInfos);
        Bearing = degree.Normalize0To2Max(bearing);
    }

    #endregion

    #region Private Parsing Helpers

    /// <summary>
    /// Attempts to parse a string containing (latitude, longitude, bearing). Returns latitude, longitude, and sets the out <paramref name="bearing"/>.
    /// </summary>
    /// <remarks>
    /// This method is used by the constructor <see cref="GeoVector(string, CultureInfo[])"/> to parse coordinates.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown if parsing fails or the string is invalid.</exception>
    private static (T latitude, T longitude) ParseVectorString(
        string coordinates,
        CultureInfo[] cultureInfos,
        out T bearing
    )
    {
        if (cultureInfos is null || cultureInfos.Length == 0)
        {
            cultureInfos = new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture };
        }

        foreach (var cultureInfo in cultureInfos)
        {
            var parts = coordinates.Split(
                [cultureInfo.TextInfo.ListSeparator],
                StringSplitOptions.None
            );

            if (parts.Length != 3) continue;

            // Attempt to parse bearing
            if (!T.TryParse(parts[2], NumberStyles.Float, cultureInfo, out T direction))
                continue;

            // Normalize bearing to [0, 360).
            var localBearing = MathEx.Mod(direction, degree.Perigon);

            // Reuse GeoPoint's existing parse approach:
            // We can re-use the static or protected logic from base if it’s accessible,
            // or replicate it here. For simplicity, let's do a quick manual parse:
            var regex = GeoPoint<T>.BuildRegexCoordinates(cultureInfo);

            // parse latitude
            var lat = GeoPoint<T>.ParseCoordinate(
                CoordinateDirection.Latitude,
                parts[0],
                GeoPoint<T>.PositiveLatitude,
                GeoPoint<T>.NegativeLatitude,
                cultureInfo,
                regex
            );
            if (T.IsNaN(lat)) continue; // failed parse

            // parse longitude
            var lon = GeoPoint<T>.ParseCoordinate(
                CoordinateDirection.Longitude,
                parts[1],
                GeoPoint<T>.PositiveLongitude,
                GeoPoint<T>.NegativeLongitude,
                cultureInfo,
                regex
            );
            if (T.IsNaN(lon)) continue; // failed parse

            bearing = localBearing;
            return (lat, lon);
        }

        throw new ArgumentException($"\"{coordinates}\" is not a valid vector (latitude, longitude, bearing).");
    }

    #endregion

    #region Deconstructor

    /// <summary>
    /// Deconstructs the <see cref="GeoVector{T}"/> into latitude, longitude, and bearing.
    /// </summary>
    /// <param name="latitude">Latitude.</param>
    /// <param name="longitude">Longitude.</param>
    /// <param name="bearing">Bearing, in degrees.</param>
    public void Deconstruct(out T latitude, out T longitude, out T bearing)
    {
        latitude = Point.Latitude;
        longitude = Point.Longitude;
        bearing = Bearing;
    }

    #endregion

    #region Bearing Computation

    /// <summary>
    /// Computes the bearing from point A to point B in degrees.
    /// </summary>
    /// <param name="A">Start point.</param>
    /// <param name="B">Destination point.</param>
    /// <returns>The bearing from A to B, normalized to [0..360).</returns>
    public static T ComputeBearing(GeoPoint<T> A, GeoPoint<T> B)
    {
        if (comparer.Compare(A.Longitude, B.Longitude) == 0)
        {
            // Vertical line test
            return A.Latitude > B.Latitude ? degree.StraightAngle : T.Zero;
        }
        if (comparer.Compare(A.Longitude, B.Longitude - degree.StraightAngle) == 0
            || comparer.Compare(A.Longitude, B.Longitude + degree.StraightAngle) == 0)
        {
            return A.Latitude > -B.Latitude ? degree.StraightAngle : T.Zero;
        }

        T y = degree.Sin(B.λ - A.λ) * degree.Cos(B.φ);
        T x = degree.Cos(A.φ) * degree.Sin(B.φ)
              - degree.Sin(A.φ) * degree.Cos(B.φ) * degree.Cos(B.λ - A.λ);

        return degree.Normalize0To2Max(degree.Atan2(y, x));
    }

    #endregion

    #region Travel

    /// <summary>
    /// Calculates the resulting vector after traveling a certain angular distance from this vector’s position.
    /// </summary>
    /// <param name="angle">Angular distance to travel (in degrees).</param>
    /// <returns>A new <see cref="GeoVector{T}"/> representing the position and bearing after traveling.</returns>
    public GeoVector<T> Travel(T angle)
    {
        bool negative = T.Sign(angle) == -1;

        // Force angle into [0, 360)
        angle = degree.Normalize0To2Max(angle);

        if (angle == T.Zero)
        {
            // No displacement
            return this;
        }
        if (angle == degree.StraightAngle)
        {
            return new GeoVector<T>(
                -this.Latitude,
                this.Longitude + degree.StraightAngle,
                this.Bearing + degree.StraightAngle
            );
        }

        T bearingCorrection = (angle <= degree.StraightAngle) ^ negative
            ? degree.StraightAngle
            : T.Zero;

        if (this.φ == degree.RightAngle)
            return new GeoVector<T>(
                degree.RightAngle - angle,
                bearingCorrection + this.λ,
                bearingCorrection
            );

        if (this.φ == -degree.RightAngle)
            return new GeoVector<T>(
                -degree.RightAngle + angle,
                bearingCorrection + this.λ,
                degree.StraightAngle + bearingCorrection
            );

        T φ2 = degree.Asin(
            degree.Sin(φ) * degree.Cos(angle)
            + degree.Cos(φ) * degree.Sin(angle) * degree.Cos(-Bearing)
        );

        if (φ2 == degree.RightAngle)
        {
            return new GeoVector<T>(
                degree.RightAngle,
                λ,
                MathEx.Mod(Bearing + degree.StraightAngle + bearingCorrection, degree.Perigon)
            );
        }
        if (φ2 == -degree.RightAngle)
        {
            return new GeoVector<T>(
                -degree.RightAngle,
                λ,
                MathEx.Mod(Bearing + degree.StraightAngle + bearingCorrection, degree.Perigon)
            );
        }

        T λ2 = λ + degree.Atan2(
            degree.Sin(Bearing) * degree.Sin(angle) * degree.Cos(φ),
            degree.Cos(angle) - degree.Sin(φ) * degree.Sin(φ2)
        );

        φ2 = T.Round(φ2, 5);
        λ2 = T.Round(λ2, 5);

        var arrival = new GeoPoint<T>(φ2, λ2);
        T newBearing = MathEx.Mod(bearingCorrection + ComputeBearing(arrival, this.Point), degree.Perigon);

        return new GeoVector<T>(arrival, newBearing);
    }

    #endregion

    #region recenter

    /// <summary>
    /// Returns a new GeoVector such that:
    /// - The current vector ("this") is mapped to (0°, 0°, 0°).
    /// - The new latitude = spherical distance (central angle) from "this" to "other".
    /// - The new longitude = difference in initial bearings, so that "this.Bearing" becomes 0°.
    /// - The new bearing = difference in heading from "this.Bearing".
    ///
    /// Change the reference so that the current vector is effectively the new origin with heading=0.
    /// </summary>
    /// <returns>The point recentered with the current vector as new reference</returns>
    public GeoVector<T> Recenter(GeoVector<T> other)
    {
        // If "other" IS the same vector as "this," map to (0,0,0).
        if (this == other)
        {
            return new GeoVector<T>(T.Zero, T.Zero, T.Zero);
        }

        // 1) New latitude = central angle between "reference" and "other".
        //    (This uses the sphere-based "AngleWith" method on GeoPoint.)
        T newLat = this.Point.AngleWith(other.Point);

        // 2) We define "new longitude" by the difference in initial bearings
        //    so that reference.Bearing becomes 0 in the new system.
        T bearingRefToOther = ComputeBearing(this.Point, other.Point);
        // We want to shift so that reference.Bearing => 0.
        T newLon = MathEx.Mod(bearingRefToOther - this.Bearing, degree.Perigon);

        // 3) We define the new bearing as the difference from reference.Bearing.
        //    So if "other" had bearing=someValue, we shift by -reference.Bearing.
        T newBearing = MathEx.Mod(other.Bearing - this.Bearing, degree.Perigon);

        // Now we have a new (lat, lon, bearing) in the recentered system.
        // By design, "this" -> (0,0,0).
        return new GeoVector<T>(newLat, newLon, newBearing);
    }

    #endregion

    #region Intersections

    /// <summary>
    /// Calculates intersection points between two great circles defined by this <see cref="GeoVector{T}"/>
    /// and the <paramref name="other"/> one.
    /// </summary>
    /// <param name="other">The second vector/great circle.</param>
    /// <returns>An array of 2 intersection points, or an empty array if the circles are identical.</returns>
    public GeoPoint<T>[] Intersections(GeoVector<T> other)
    {
        var temp = (
            a: Travel(this.Point.AngleWith(other.Point)),
            b: (-this).Travel(this.Point.AngleWith(other.Point))
        );

        if (other.In(temp.a, -temp.a, temp.b, -temp.b))
        {
            // No unique intersections (same great circle).
            return Array.Empty<GeoPoint<T>>();
        }

        // Poles & meridians
        if (MathEx.Mod(this.Bearing, degree.StraightAngle) == T.Zero
            && MathEx.Mod(other.Bearing, degree.StraightAngle) == T.Zero)
        {
            return
            [
                new GeoPoint<T>(degree.RightAngle, T.Zero),
                new GeoPoint<T>(-degree.RightAngle, degree.StraightAngle)
            ];
        }

        if (MathEx.Mod(this.Bearing, degree.StraightAngle) == T.Zero)
        {
            var result = other.Travel(this.λ - other.λ);
            return
            [
                new GeoPoint<T>(result.φ, result.λ),
                new GeoPoint<T>(-result.φ, result.λ + degree.StraightAngle),
            ];
        }

        if (MathEx.Mod(other.Bearing, degree.StraightAngle) == T.Zero)
        {
            var result = this.Travel(this.λ - other.λ);
            return
            [
                new GeoPoint<T>(result.φ, result.λ),
                new GeoPoint<T>(-result.φ, result.λ + degree.StraightAngle),
            ];
        }

        T Δφ = this.φ - other.φ;
        T Δλ = this.λ - other.λ;

        T δ12 = T.CreateChecked(2) * degree.Asin(
            T.Sqrt(
                T.Pow(degree.Sin(Δφ / T.CreateChecked(2)), T.CreateChecked(2))
                + degree.Cos(this.φ) * degree.Cos(other.φ) * T.Pow(degree.Sin(Δλ / T.CreateChecked(2)), T.CreateChecked(2))
            )
        );

        T θa = degree.Acos(
            (degree.Sin(other.φ) - degree.Sin(this.φ) * degree.Cos(δ12))
            / (degree.Sin(δ12) * degree.Cos(this.φ))
        );
        T θb = degree.Acos(
            (degree.Sin(this.φ) - degree.Sin(other.φ) * degree.Cos(δ12))
            / (degree.Sin(δ12) * degree.Cos(other.φ))
        );

        T θ12, θ21;
        if (degree.Sin(Δλ) <= T.Zero)
        {
            θ12 = θa;
            θ21 = degree.Perigon - θb;
        }
        else
        {
            θ12 = degree.Perigon - θa;
            θ21 = θb;
        }

        T α1 = this.Bearing - θ12;
        T α2 = θ21 - other.Bearing;

        T α3 = degree.Acos(
            -degree.Cos(α1) * degree.Cos(α2)
            + degree.Sin(α1) * degree.Sin(α2) * degree.Cos(δ12)
        );

        T δ13 = degree.Atan2(
            degree.Sin(δ12) * degree.Sin(α1) * degree.Sin(α2),
            degree.Cos(α2) + degree.Cos(α1) * degree.Cos(α3)
        );
        T φ3 = degree.Asin(
            degree.Sin(this.φ) * degree.Cos(δ13)
            + degree.Cos(this.φ) * degree.Sin(δ13) * degree.Cos(this.Bearing)
        );
        T Δλ13 = degree.Atan2(
            degree.Sin(this.Bearing) * degree.Sin(δ13) * degree.Cos(this.φ),
            degree.Cos(δ13) - degree.Sin(this.φ) * degree.Sin(φ3)
        );
        T λ3 = this.λ + Δλ13;

        φ3 = T.Round(φ3, 5);
        λ3 = T.Round(λ3, 5);

        return
        [
            new GeoPoint<T>(φ3, λ3),
            new GeoPoint<T>(-φ3, λ3 + degree.StraightAngle)
        ];
    }

    #endregion

    #region Equality & Overrides

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GeoVector<T> vector && Equals(vector);

    /// <summary>
    /// Determines whether the specified <see cref="GeoVector{T}"/> has the same position and bearing as
    /// this instance: latitude/longitude are compared via <see cref="GeoPoint{T}.Equals(GeoPoint{T})"/>
    /// (rounded to <see cref="GeoPoint{T}.EqualityPrecision"/> decimal places), and bearing is compared the
    /// same way longitude is: normalized and rounded to the same precision via
    /// <see cref="IAngleCalculator{T}.AreEqualRounded"/>, since bearing wraps around at 0°/360° just like
    /// longitude wraps around at the antimeridian.
    /// </summary>
    /// <param name="other">The vector to compare with this instance.</param>
    /// <returns>
    /// <see langword="true"/> if both vectors round to the same latitude, longitude, and bearing;
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This has the exact same known limitation as <see cref="GeoPoint{T}.Equals(GeoPoint{T})"/> (see its
    /// remarks for the full rationale and a worked example): rounding each coordinate before comparing
    /// keeps <see cref="Equals(GeoVector{T})"/> and <see cref="GetHashCode"/> always consistent with each
    /// other, but two bearings that are extremely close yet fall on opposite sides of a rounding boundary
    /// (e.g. <c>1.0000449999</c> vs <c>1.0000450001</c>, with <see cref="GeoPoint{T}.EqualityPrecision"/> = 5)
    /// are treated as unequal. See <c>GeoVectorTests.VectorsOnOppositeSidesOfARoundingBoundaryAreNotEqual</c>
    /// for a regression test pinning down this behavior.
    /// </remarks>
    public bool Equals(GeoVector<T> other)
        => degree.AreEqualRounded(Bearing, other.Bearing, GeoPoint<T>.EqualityPrecision)
           && Point.Equals(other.Point);

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(GeoVector{T})"/>: it combines <see cref="Point"/>'s
    /// own hash code (already rounded/normalized, see <see cref="GeoPoint{T}.GetHashCode"/>) with the bearing,
    /// normalized (to handle 0°/360° wraparound) and rounded to <see cref="GeoPoint{T}.EqualityPrecision"/>
    /// decimal places — the exact same values that <see cref="Equals(GeoVector{T})"/> compares on.
    /// </summary>
    public override int GetHashCode()
        => ObjectUtils.ComputeHash(
            Point.GetHashCode(),
            degree.NormalizeRounded(Bearing, GeoPoint<T>.EqualityPrecision));

    /// <summary>
    /// Determines whether this vector is within <paramref name="tolerance"/> of <paramref name="other"/>,
    /// using a raw angular-distance tolerance window rather than the rounding-based comparison used by
    /// <see cref="Equals(GeoVector{T})"/>. See <see cref="GeoPoint{T}.IsApproximately(GeoPoint{T}, T)"/>
    /// for the full rationale and usage guidance (do not use this for hashing/dictionary keys).
    /// </summary>
    /// <param name="other">The vector to compare with this instance.</param>
    /// <param name="tolerance">Maximum allowed angular distance, in degrees, for latitude, longitude, and bearing.</param>
    /// <returns>
    /// <see langword="true"/> if latitude, longitude, and bearing are all within <paramref name="tolerance"/>
    /// of each other; otherwise <see langword="false"/>.
    /// </returns>
    public bool IsApproximately(GeoVector<T> other, T tolerance)
        => degree.AreEqual(Bearing, other.Bearing, tolerance) && Point.IsApproximately(other.Point, tolerance);

    /// <summary>
    /// Determines whether this vector is within the default tolerance of <paramref name="other"/>.
    /// See <see cref="IsApproximately(GeoVector{T}, T)"/> for the full rationale and usage guidance.
    /// </summary>
    /// <param name="other">The vector to compare with this instance.</param>
    /// <returns><see langword="true"/> if both vectors are within the default tolerance; otherwise <see langword="false"/>.</returns>
    public bool IsApproximately(GeoVector<T> other) => IsApproximately(other, comparer.Interval);

    #endregion

    #region Operators

    /// <summary>
    /// Negation operator for <see cref="GeoVector{T}"/>.
    /// </summary>
    /// <param name="geoVector">The vector to negate.</param>
    /// <returns>A new <see cref="GeoVector{T}"/> whose bearing is reversed 180° from <paramref name="geoVector"/>.</returns>
    public static GeoVector<T> operator -(GeoVector<T> geoVector)
        => new GeoVector<T>(
            geoVector.φ,
            geoVector.λ,
            degree.StraightAngle + geoVector.θ
        );

    /// <summary>
    /// Determines whether two geographic vectors are equal.
    /// </summary>
    public static bool operator ==(GeoVector<T> left, GeoVector<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two geographic vectors are not equal.
    /// </summary>
    public static bool operator !=(GeoVector<T> left, GeoVector<T> right) => !left.Equals(right);

    /// <summary>
    /// Returns this geographic vector as a string in the format "Latitude, Longitude, Bearing"
    /// with five decimal places by default.
    /// </summary>
    public override string ToString() => ToString("0.#####", CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns this geographic vector as a string in the specified format and the invariant culture.
    /// </summary>
    public string ToString(string format) => ToString(format, null);

    /// <summary>
    /// Returns a string representation of the vector using the specified format and provider.
    /// </summary>
    /// <param name="format">Format string applied to latitude, longitude, and bearing.</param>
    /// <param name="formatProvider">Culture-specific format provider.</param>
    /// <returns>A formatted string representing the vector.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        formatProvider ??= CultureInfo.InvariantCulture;
        var textInfo = formatProvider.GetFormat(typeof(TextInfo)) as TextInfo;

        // Example: "Latitude, Longitude, Bearing"
        return $"{Point.ToString(format, formatProvider)}{textInfo?.ListSeparator ?? ","} {Bearing:##0.##}";
    }

    #endregion
}
