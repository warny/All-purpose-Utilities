using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Geography.Model
{
    /// <summary>
    /// Enum to represent the type of geographic coordinate direction: Latitude or Longitude.
    /// </summary>
    public enum CoordinateDirection
    {
        /// <summary>
        /// Indicates that the coordinate value represents a latitude.
        /// </summary>
        Latitude,

        /// <summary>
        /// Indicates that the coordinate value represents a longitude.
        /// </summary>
        Longitude
    }

    /// <summary>
    /// Represents an immutable geographic point with latitude and longitude.
    /// This class supports parsing, formatting, and mathematical operations between geographic points.
    /// </summary>
    /// <typeparam name="T">
    /// Numeric type implementing IFloatingPointIeee754 (e.g., float, double, decimal).
    /// </typeparam>
    public class GeoPoint<T> : IEquatable<GeoPoint<T>>, IFormattable, IEqualityOperators<GeoPoint<T>, GeoPoint<T>, bool>
        where T : struct, IFloatingPointIeee754<T>, IDivisionOperators<T, T, T>
    {
        // Constants for degrees, minutes, and seconds conversions
        /// <summary>
        /// Number of minutes in a degree.
        /// </summary>
        protected static readonly T MinutesInDegree = T.CreateChecked(60);

        /// <summary>
        /// Number of seconds in a degree.
        /// </summary>
        protected static readonly T SecondsInDegree = T.CreateChecked(3600);

        /// <summary>
        /// Number of seconds in a minute.
        /// </summary>
        protected static readonly T SecondsInMinute = T.CreateChecked(60);

        /// <summary>
        /// Angle calculator configured to work in degrees.
        /// </summary>
        protected static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

        /// <summary>
        /// Number of decimal places latitude/longitude (and bearing, for <see cref="GeoVector{T}"/>) are
        /// rounded to before being compared in <see cref="Equals(GeoPoint{T})"/> and hashed in
        /// <see cref="GetHashCode"/>. Rounding both sides the same way keeps the two members consistent
        /// with each other by construction, at the cost of treating two values that are extremely close
        /// but land on opposite sides of a rounding boundary as unequal. See the remarks on
        /// <see cref="Equals(GeoPoint{T})"/> for the full rationale and a worked example.
        /// </summary>
        protected const int EqualityPrecision = 5;

        /// <summary>
        /// Floating-point comparer used for tolerance-based domain checks that are not part of the
        /// <see cref="Equals(GeoPoint{T})"/>/<see cref="GetHashCode"/> contract (e.g. detecting degenerate
        /// meridians in <see cref="GeoVector{T}.ComputeBearing"/>, or the default tolerance used by
        /// <see cref="IsApproximately(GeoPoint{T})"/>).
        /// </summary>
        protected static readonly FloatingPointComparer<T> comparer = new(EqualityPrecision);

        /// <summary>
        /// Maximum valid latitude in degrees.
        /// </summary>
        public T MaxLatitude => degree.RightAngle;

        /// <summary>
        /// Minimum valid latitude in degrees.
        /// </summary>
        public T MinLatitude => -degree.RightAngle;

        /// <summary>
        /// Modifiers that indicate a positive latitude value.
        /// </summary>
        protected static readonly IReadOnlyList<string> PositiveLatitude = ["+", "N"];

        /// <summary>
        /// Modifiers that indicate a negative latitude value.
        /// </summary>
        protected static readonly IReadOnlyList<string> NegativeLatitude = ["-", "S"];

        /// <summary>
        /// Modifiers that indicate a positive longitude value.
        /// </summary>
        protected static readonly IReadOnlyList<string> PositiveLongitude = ["+", "E"];

        /// <summary>
        /// Modifiers that indicate a negative longitude value.
        /// </summary>
        protected static readonly IReadOnlyList<string> NegativeLongitude = ["-", "W"];

        /// <summary>
        /// Latitude in degrees (immutable).
        /// </summary>
        public T Latitude { get; }

        /// <summary>
        /// Longitude in degrees (immutable).
        /// </summary>
        public T Longitude { get; }

        /// <summary>
        /// Alias for Latitude.
        /// </summary>
        public T φ => Latitude;

        /// <summary>
        /// Alias for Longitude.
        /// </summary>
        public T λ => Longitude;

        /// <summary>
        /// Protected default constructor used for inheritance scenarios or reflection.
        /// </summary>
        protected GeoPoint() { }

        /// <summary>
        /// Copy constructor for creating a new GeoPoint from an existing one.
        /// </summary>
        /// <param name="geoPoint">Existing point from which to copy Latitude and Longitude.</param>
        public GeoPoint(GeoPoint<T> geoPoint)
        {
            Initialize(geoPoint.Latitude, geoPoint.Longitude, out T lat, out T lon);
            Latitude = lat;
            Longitude = lon;
        }

        /// <summary>
        /// Creates a GeoPoint from given coordinates.
        /// </summary>
        /// <param name="latitude">Latitude coordinate in degrees.</param>
        /// <param name="longitude">Longitude coordinate in degrees.</param>
        public GeoPoint(T latitude, T longitude)
        {
            Initialize(latitude, longitude, out T lat, out T lon);
            Latitude = lat;
            Longitude = lon;
        }

        /// <summary>
        /// Creates a GeoPoint by parsing coordinate strings in various cultures.
        /// </summary>
        /// <param name="coordinates">
        /// A string representing both latitude and longitude, separated by the culture-specific list separator.
        /// </param>
        /// <param name="cultureInfos">
        /// Optional cultures to use for parsing (defaults to CurrentCulture and InvariantCulture).
        /// </param>
        public GeoPoint(string coordinates, params CultureInfo[] cultureInfos)
        {
            if (cultureInfos.Length == 0)
                cultureInfos = new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture };

            foreach (var cultureInfo in cultureInfos)
            {
                var parts = coordinates.Split(
                    new[] { cultureInfo.TextInfo.ListSeparator },
                    StringSplitOptions.None
                );

                if (parts.Length != 2) continue;

                var regex = BuildRegexCoordinates(cultureInfo);
                if (ParseCoordinates(parts[0], parts[1], cultureInfo, regex, out T lat, out T lon))
                {
                    Latitude = lat;
                    Longitude = lon;
                    return;
                }
            }

            throw new ArgumentException($"Invalid coordinate format: \"{coordinates}\"");
        }

        /// <summary>
        /// Parses latitude and longitude strings and creates a GeoPoint.
        /// </summary>
        /// <param name="latitudeString">Latitude string.</param>
        /// <param name="longitudeString">Longitude string.</param>
        /// <param name="cultureInfos">Optional cultures for parsing.</param>
        public GeoPoint(string latitudeString, string longitudeString, params CultureInfo[] cultureInfos)
        {
            if (cultureInfos.Length == 0)
                cultureInfos = [CultureInfo.CurrentCulture, CultureInfo.InvariantCulture];

            foreach (var cultureInfo in cultureInfos)
            {
                var regex = BuildRegexCoordinates(cultureInfo);
                if (ParseCoordinates(latitudeString, longitudeString, cultureInfo, regex, out T lat, out T lon))
                {
                    Latitude = lat;
                    Longitude = lon;
                    return;
                }
            }

            throw new ArgumentException("Invalid coordinates");
        }

        /// <summary>
        /// Attempts to parse the provided latitude and longitude strings using the specified culture and regex.
        /// If successful, lat/lon will be set and <see langword="true"/> will be returned.
        /// </summary>
        protected bool ParseCoordinates(
            string latitudeString,
            string longitudeString,
            CultureInfo cultureInfo,
            Regex regExCoordinate,
            out T latitude,
            out T longitude
        )
        {
            latitude = ParseCoordinate(
                CoordinateDirection.Latitude,
                latitudeString,
                PositiveLatitude,
                NegativeLatitude,
                cultureInfo,
                regExCoordinate
            );

            longitude = ParseCoordinate(
                CoordinateDirection.Longitude,
                longitudeString,
                PositiveLongitude,
                NegativeLongitude,
                cultureInfo,
                regExCoordinate
            );

            if (T.IsNaN(latitude) || T.IsNaN(longitude))
                return false;

            Initialize(latitude, longitude, out latitude, out longitude);
            return true;
        }

        /// <summary>
        /// Parses a single string coordinate value based on its direction (Latitude/Longitude).
        /// </summary>
        protected static T ParseCoordinate(
            CoordinateDirection direction,
            string coordinateValue,
            IReadOnlyList<string> positiveModifiers,
            IReadOnlyList<string> negativeModifiers,
            CultureInfo culture,
            Regex regex
        )
        {
            var match = regex.Match(coordinateValue);
            if (!match.Success) return T.NaN;

            // Parse degrees, minutes, and seconds
            T degrees = match.Groups["degrees"].Success
                ? T.Parse(match.Groups["degrees"].Value, NumberStyles.Float, culture)
                : T.Zero;
            T minutes = match.Groups["minutes"].Success
                ? T.Parse(match.Groups["minutes"].Value, NumberStyles.Float, culture)
                : T.Zero;
            T seconds = match.Groups["seconds"].Success
                ? T.Parse(match.Groups["seconds"].Value, NumberStyles.Float, culture)
                : T.Zero;

            // Combine into a single degrees value
            T coordinate = degrees + (minutes / MinutesInDegree) + (seconds / SecondsInDegree);

            // Check coordinate modifier for sign
            string modifier = match.Groups["modifier"].Success
                ? match.Groups["modifier"].Value
                : positiveModifiers[0]; // defaults to positive if none

            if (negativeModifiers.Contains(modifier))
            {
                coordinate = -coordinate;
            }
            else if (!positiveModifiers.Contains(modifier))
            {
                throw new ArgumentException($"Invalid modifier for {direction}", coordinateValue);
            }

            return coordinate;
        }

        /// <summary>
        /// Helper function to initialize latitude and longitude with bounds checking and normalization.
        /// </summary>
        private void Initialize(T latitude, T longitude, out T lat, out T lon)
        {
            latitude.ArgMustBeANumber();
            latitude.ArgMustBeBetween(MinLatitude, MaxLatitude);

            lon = degree.NormalizeMinToMax(longitude);
            lat = latitude;
        }

        /// <summary>
        /// Computes the angular distance between this point and another (central angle on a sphere).
        /// </summary>
        /// <param name="other">Another geographic point.</param>
        /// <returns>
        /// The angular distance in degrees (if your angle calculator uses degrees).
        /// Multiply by Earth radius (in the same units as your angle measure) for a distance on Earth’s surface.
        /// </returns>
        public T AngleWith(GeoPoint<T> other)
        {
            return degree.Acos(
                degree.Sin(Latitude) * degree.Sin(other.Latitude)
                + degree.Cos(Latitude) * degree.Cos(other.Latitude)
                * degree.Cos(Longitude - other.Longitude)
            );
        }


        #region Equality & Formatting

        /// <inheritdoc/>
        public override bool Equals(object? obj) =>
            obj is GeoPoint<T> other && Equals(other);

        /// <summary>
        /// Determines whether the specified <see cref="GeoPoint{T}"/> represents the same location as this
        /// instance, comparing latitude and longitude after rounding both to <see cref="EqualityPrecision"/>
        /// decimal places.
        /// </summary>
        /// <param name="other">The point to compare with this instance.</param>
        /// <returns>
        /// <see langword="true"/> if both points round to the same latitude and longitude (or are both at
        /// the same pole, regardless of longitude); otherwise <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>Why rounding, and not a tolerance window:</b> latitude/longitude are floating-point values
        /// produced by trigonometric computations (<see cref="GeoVector{T}.Travel"/>,
        /// <see cref="GeoVector{T}.Intersections"/>, ...), which never land on exact values. An earlier
        /// implementation compared raw values with a tolerance window (<c>|a - b| &lt;= 1e-5</c>) via
        /// <see cref="FloatingPointComparer{T}"/>, but that comparer is <em>intentionally non-transitive</em>
        /// (see its own documentation) and cannot be paired with a correct <see cref="GetHashCode"/>: two
        /// values considered equal by the tolerance window could still hash differently, breaking
        /// <see cref="Dictionary{TKey, TValue}"/>/<see cref="HashSet{T}"/> usage. Rounding both operands to
        /// the same precision before comparing removes that risk entirely, because <see cref="Equals(GeoPoint{T})"/>
        /// and <see cref="GetHashCode"/> then always operate on the exact same rounded values.
        /// </para>
        /// <para>
        /// <b>Known limitation — rounding-boundary straddling:</b> this trades away the "equal within a
        /// tolerance window" behavior. Two values that are extremely close to each other, but fall on
        /// opposite sides of a rounding boundary, are now treated as <em>unequal</em>, even though a raw
        /// tolerance comparison would have called them equal. For example, with
        /// <see cref="EqualityPrecision"/> = 5, <c>1.0000449999</c> rounds down to <c>1.00004</c> while
        /// <c>1.0000450001</c> rounds up to <c>1.00005</c> — a difference of about <c>2e-7</c>, far below
        /// the old <c>1e-5</c> tolerance, yet the two values are no longer equal. This is a deliberate,
        /// accepted trade-off (values this close in practice only arise from floating-point noise around a
        /// rounding boundary, and rounding-consistency was judged more valuable than tolerance-window
        /// equality for this type). See <c>GeoPointTests.PointsOnOppositeSidesOfARoundingBoundaryAreNotEqual</c>
        /// for a regression test that pins down this exact behavior.
        /// </para>
        /// <para>
        /// <b>Longitude wraps around, latitude doesn't:</b> longitude is compared via
        /// <see cref="IAngleCalculator{T}.AreEqualRounded"/> rather than a plain rounded comparison, so that
        /// values on opposite sides of the antimeridian (e.g. <c>179.9999951°</c> and <c>-179.9999999°</c>,
        /// which both refer to almost the same point near 180°) round to the same normalized value instead
        /// of comparing as ~360° apart. Latitude never wraps (it is clamped to [-90°, 90°] and the poles are
        /// handled separately above), so it only needs a plain rounded comparison.
        /// </para>
        /// </remarks>
        public bool Equals(GeoPoint<T>? other)
        {
            if (other is null) return false;

            T roundedLatitude = T.Round(Latitude, EqualityPrecision);
            T otherRoundedLatitude = T.Round(other.Latitude, EqualityPrecision);

            // Any longitude at a pole refers to the same point.
            if (roundedLatitude == MaxLatitude && otherRoundedLatitude == MaxLatitude) return true;
            if (roundedLatitude == MinLatitude && otherRoundedLatitude == MinLatitude) return true;

            return roundedLatitude == otherRoundedLatitude
                && degree.AreEqualRounded(Longitude, other.Longitude, EqualityPrecision);
        }

        /// <summary>
        /// Returns a hash code consistent with <see cref="Equals(GeoPoint{T})"/>: latitude is rounded, and
        /// longitude is normalized (to handle antimeridian wraparound) and rounded, to
        /// <see cref="EqualityPrecision"/> decimal places before hashing — the exact same values that
        /// <see cref="Equals(GeoPoint{T})"/> compares on, so equal points always hash equally.
        /// </summary>
        public override int GetHashCode()
        {
            return ObjectUtils.ComputeHash(T.Round(Latitude, EqualityPrecision), degree.NormalizeRounded(Longitude, EqualityPrecision));
        }

        /// <summary>
        /// Determines whether this point is within <paramref name="tolerance"/> of <paramref name="other"/>,
        /// using a raw angular-distance tolerance window rather than the rounding-based comparison used by
        /// <see cref="Equals(GeoPoint{T})"/>.
        /// </summary>
        /// <param name="other">The point to compare with this instance.</param>
        /// <param name="tolerance">Maximum allowed angular distance, in degrees, for latitude and longitude.</param>
        /// <returns>
        /// <see langword="true"/> if both latitude and longitude are within <paramref name="tolerance"/> of
        /// each other (or both points are at the same pole); otherwise <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <see cref="Equals(GeoPoint{T})"/> intentionally does <em>not</em> offer this behavior anymore
        /// (see its remarks): a tolerance window cannot be paired with a correct <see cref="GetHashCode"/>
        /// because it is non-transitive, so points must not be considered equal, hashed, or stored in a
        /// <see cref="Dictionary{TKey, TValue}"/>/<see cref="HashSet{T}"/> based on this method. Use it only
        /// for one-off comparisons (e.g. "is this GPS fix close enough to the target waypoint?"), never as
        /// a substitute for <see cref="Equals(GeoPoint{T})"/> in code that also relies on hashing.
        /// </para>
        /// <para>
        /// Longitude comparison correctly handles the antimeridian wraparound via
        /// <see cref="IAngleCalculator{T}.AreEqual"/> (e.g. <c>179.9999999°</c> and <c>-179.9999999°</c> are
        /// about <c>2e-7°</c> apart, not ~360° apart).
        /// </para>
        /// </remarks>
        public bool IsApproximately(GeoPoint<T> other, T tolerance)
        {
            other.Arg().MustNotBeNull();

            if (degree.AreEqual(Latitude, MaxLatitude, tolerance) && degree.AreEqual(other.Latitude, MaxLatitude, tolerance)) return true;
            if (degree.AreEqual(Latitude, MinLatitude, tolerance) && degree.AreEqual(other.Latitude, MinLatitude, tolerance)) return true;

            return degree.AreEqual(Latitude, other.Latitude, tolerance)
                && degree.AreEqual(Longitude, other.Longitude, tolerance);
        }

        /// <summary>
        /// Determines whether this point is within the default tolerance (see <see cref="comparer"/>) of
        /// <paramref name="other"/>. See <see cref="IsApproximately(GeoPoint{T}, T)"/> for the full
        /// rationale and usage guidance.
        /// </summary>
        /// <param name="other">The point to compare with this instance.</param>
        /// <returns><see langword="true"/> if both points are within the default tolerance; otherwise <see langword="false"/>.</returns>
        public bool IsApproximately(GeoPoint<T> other) => IsApproximately(other, comparer.Interval);

        /// <summary>
        /// Returns this geographic point as a string in the format "Latitude, Longitude"
        /// with five decimal places by default.
        /// </summary>
        public override string ToString() => ToString("0.#####", CultureInfo.InvariantCulture);

        /// <summary>
        /// Returns this geographic point as a string in the specified format and culture.
        /// </summary>
        public string ToString(string format) => ToString(format, null);

        /// <summary>
        /// Returns this geographic point as a string in the specified format and culture.
        /// Supported formats: "0.#####", "d" and "D" (for degree-minute-second).
        /// </summary>
        public virtual string ToString(string? format, IFormatProvider? formatProvider)
        {
            formatProvider ??= CultureInfo.InvariantCulture;
            format ??= "0.#####";
            var textInfo = formatProvider.GetFormat(typeof(TextInfo)) as TextInfo;
            var separator = textInfo?.ListSeparator ?? ",";

            return $"{FormatPosition(Latitude, "N", "S", format, formatProvider)}{separator} " +
                   $"{FormatPosition(Longitude, "E", "W", format, formatProvider)}";
        }

        /// <summary>
        /// Formats a single latitude or longitude value using the requested format options.
        /// </summary>
        /// <param name="position">Latitude or longitude value.</param>
        /// <param name="positiveMark">Marker appended for positive values.</param>
        /// <param name="negativeMark">Marker appended for negative values.</param>
        /// <param name="format">Desired numeric or degree format.</param>
        /// <param name="formatProvider">Culture-specific format provider.</param>
        /// <returns>Formatted coordinate string.</returns>
        private static string FormatPosition(
                T position,
                string positiveMark,
                string negativeMark,
                string format,
                IFormatProvider formatProvider
)
        {
            // Determine direction mark
            string mark = T.IsZero(position)
                ? ""
                : (T.IsPositive(position) ? positiveMark : negativeMark);

            if (format is "d" or "D")
            {
                // Perform degrees-minutes-seconds breakdown
                T absPos = T.Abs(position);
                var degrees = T.Floor(absPos);
                absPos = (absPos - degrees) * MinutesInDegree;
                var minutes = T.Floor(absPos);
                absPos = (absPos - minutes) * SecondsInDegree;
                var seconds = T.Floor(absPos);

                // If seconds non-zero or format == "D", include all
                if (!seconds.Equals(T.Zero) || format == "D")
                    return $"{mark}{degrees}°{minutes:00}'{seconds:00}\"";

                // If minutes non-zero, show degrees + minutes
                if (!minutes.Equals(T.Zero))
                    return $"{mark}{degrees}°{minutes:00}'";

                // Otherwise just degrees
                return $"{mark}{degrees}°";
            }

            // Default numeric format
            return mark + T.Abs(position).ToString(format, formatProvider);
        }

        #endregion

        #region Parse / TryParse

        /// <summary>
        /// Attempts to parse a combined coordinate string (e.g. <c>"48.8566, 2.3522"</c>) using the
        /// current culture then the invariant culture.
        /// </summary>
        /// <param name="coordinates">The string to parse.</param>
        /// <param name="result">The parsed point, or <see langword="null"/> on failure.</param>
        /// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string coordinates, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out GeoPoint<T>? result)
            => TryParse(coordinates, [], out result);

        /// <summary>
        /// Attempts to parse a combined coordinate string using the specified cultures.
        /// </summary>
        public static bool TryParse(string coordinates, CultureInfo[] cultureInfos, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out GeoPoint<T>? result)
        {
            try
            {
                result = new GeoPoint<T>(coordinates, cultureInfos);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Attempts to parse separate latitude and longitude strings using the current culture then
        /// the invariant culture.
        /// </summary>
        /// <param name="latitudeString">Latitude string (e.g. <c>"N48°51'"</c> or <c>"48.8566"</c>).</param>
        /// <param name="longitudeString">Longitude string (e.g. <c>"E2°21'"</c> or <c>"2.3522"</c>).</param>
        /// <param name="result">The parsed point, or <see langword="null"/> on failure.</param>
        /// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string latitudeString, string longitudeString, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out GeoPoint<T>? result)
            => TryParse(latitudeString, longitudeString, [], out result);

        /// <summary>
        /// Attempts to parse separate latitude and longitude strings using the specified cultures.
        /// </summary>
        public static bool TryParse(string latitudeString, string longitudeString, CultureInfo[] cultureInfos, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out GeoPoint<T>? result)
        {
            try
            {
                result = new GeoPoint<T>(latitudeString, longitudeString, cultureInfos);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        #endregion

        #region Operators

        /// <summary>
        /// Determines whether two geographic points are equal.
        /// </summary>
        public static bool operator ==(GeoPoint<T>? left, GeoPoint<T>? right) => left?.Equals(right) ?? right is null;

        /// <summary>
        /// Determines whether two geographic points are not equal.
        /// </summary>
        public static bool operator !=(GeoPoint<T>? left, GeoPoint<T>? right) => !(left == right);

        #endregion

        #region Utility

        // Regex instances are expensive to compile; cache one per unique (nativeDigits, decimalSeparator) pair.
        private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();

        /// <summary>
        /// Builds or retrieves from cache the regex used to parse coordinate values from strings.
        /// </summary>
        protected static Regex BuildRegexCoordinates(CultureInfo culture)
        {
            string nativeDigits = string.Join("", culture.NumberFormat.NativeDigits);
            string decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
            string cacheKey = $"{nativeDigits}|{decimalSeparator}";

            return _regexCache.GetOrAdd(cacheKey, static key =>
            {
                int sep = key.IndexOf('|');
                string digits = $"[{key[..sep]}]+";
                string dec = key[(sep + 1)..];
                string number = $"{digits}({Regex.Escape(dec)}{digits})?";
                return new Regex(
                    @$"(?<modifier>W|E|N|S|\-|\+)?(?<degrees>{number})(°(?<minutes>{number}))?('(?<seconds>{number}))?",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase
                );
            });
        }

        #endregion

        #region Deconstructor

        /// <summary>
        /// Deconstructs the current <see cref="GeoPoint{T}"/> into its latitude and longitude.
        /// </summary>
        /// <param name="latitude">The latitude, in degrees.</param>
        /// <param name="longitude">The longitude, in degrees.</param>
        public void Deconstruct(out T latitude, out T longitude)
        {
            latitude = Latitude;
            longitude = Longitude;
        }

        #endregion
    }
}
