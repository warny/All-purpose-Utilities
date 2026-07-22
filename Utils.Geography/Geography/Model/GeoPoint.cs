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
    /// This type supports parsing, formatting, and mathematical operations between geographic points.
    /// </summary>
    /// <typeparam name="T">
    /// Numeric type implementing IFloatingPointIeee754 (e.g., float, double, decimal).
    /// </typeparam>
    public readonly struct GeoPoint<T> : IEquatable<GeoPoint<T>>, IFormattable, IEqualityOperators<GeoPoint<T>, GeoPoint<T>, bool>
        where T : struct, IFloatingPointIeee754<T>, IDivisionOperators<T, T, T>
    {
        // Constants for degrees, minutes, and seconds conversions
        /// <summary>
        /// Number of minutes in a degree.
        /// </summary>
        private static readonly T MinutesInDegree = T.CreateChecked(60);

        /// <summary>
        /// Number of seconds in a degree.
        /// </summary>
        private static readonly T SecondsInDegree = T.CreateChecked(3600);

        /// <summary>
        /// Number of seconds in a minute.
        /// </summary>
        private static readonly T SecondsInMinute = T.CreateChecked(60);

        /// <summary>
        /// Angle calculator configured to work in degrees.
        /// </summary>
        private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

        /// <summary>
        /// Equality grid step for <see cref="Equals(GeoPoint{T})"/> and <see cref="GetHashCode"/>.
        /// Computed as <c>max(2⁻³³, 2⁷ × ε_machine)</c> where <c>ε_machine = T.BitIncrement(1) − 1</c>
        /// (the ULP at 1.0, i.e. the true machine epsilon — a negative power of 2).
        /// Both terms are exact powers of 2, so dividing a coordinate value (|v| &lt; 2¹⁹ degrees) by
        /// this step is equivalent to multiplying by a power of 2, which is IEEE 754 exact with no
        /// base-10 rounding artifacts.
        /// For <c>double</c> (ε_m ≈ 2⁻⁵²): max(2⁻³³, 2⁻⁴⁵) = 2⁻³³ ≈ 1.16 × 10⁻¹⁰ degrees.
        /// For <c>float</c>  (ε_m ≈ 2⁻²³): max(2⁻³³, 2⁻¹⁶) = 2⁻¹⁶ ≈ 1.53 × 10⁻⁵ degrees.
        /// </summary>
        internal static readonly T EqualityStep = T.Max(
            T.ScaleB(T.One, -33),
            T.ScaleB(T.BitIncrement(T.One) - T.One, 7));

        /// <summary>
        /// Floating-point comparer used for tolerance-based domain checks that are not part of the
        /// <see cref="Equals(GeoPoint{T})"/>/<see cref="GetHashCode"/> contract (e.g. detecting degenerate
        /// meridians in <see cref="GeoVector{T}.ComputeBearing"/>, or the default tolerance used by
        /// <see cref="IsApproximately(GeoPoint{T})"/>).
        /// </summary>
        internal static readonly FloatingPointComparer<T> comparer = new(EqualityStep);

        /// <summary>
        /// Returns the grid index of <paramref name="value"/> for <see cref="EqualityStep"/>-based
        /// comparison. When <see cref="EqualityStep"/> is a power of 2 and |<paramref name="value"/>|
        /// &lt; 2¹⁹, the division is IEEE 754 exact (no rounding in the grid computation itself).
        /// </summary>
        private static T SnapIndex(T value) => T.Round(value / EqualityStep);

        /// <summary>
        /// Returns the grid index of <paramref name="angle"/> after normalizing it to
        /// <c>[0, Perigon)</c>, wrapping the index back to zero when it would equal the perigon
        /// index (e.g. 360° and 0° map to the same index). Used for circular quantities such as
        /// longitude and bearing.
        /// </summary>
        internal static T SnapCircleIndex(T angle)
        {
            T normalized = degree.Normalize0To2Max(angle);
            T idx = T.Round(normalized / EqualityStep);
            T perigonIdx = T.Round(degree.Perigon / EqualityStep);
            return idx >= perigonIdx ? T.Zero : idx;
        }

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
        internal static readonly IReadOnlyList<string> PositiveLatitude = ["+", "N"];

        /// <summary>
        /// Modifiers that indicate a negative latitude value.
        /// </summary>
        internal static readonly IReadOnlyList<string> NegativeLatitude = ["-", "S"];

        /// <summary>
        /// Modifiers that indicate a positive longitude value.
        /// </summary>
        internal static readonly IReadOnlyList<string> PositiveLongitude = ["+", "E"];

        /// <summary>
        /// Modifiers that indicate a negative longitude value.
        /// </summary>
        internal static readonly IReadOnlyList<string> NegativeLongitude = ["-", "W"];

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
        private bool ParseCoordinates(
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
        internal static T ParseCoordinate(
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
            // Clamp to [-1, 1] before Acos: the dot product is mathematically in that range, but
            // floating-point rounding can produce values just outside it, yielding NaN for
            // identical or nearly identical points.
            T dot = degree.Sin(Latitude) * degree.Sin(other.Latitude)
                + degree.Cos(Latitude) * degree.Cos(other.Latitude)
                * degree.Cos(Longitude - other.Longitude);
            return degree.Acos(T.Clamp(dot, -T.One, T.One));
        }


        #region Equality & Formatting

        /// <inheritdoc/>
        public override bool Equals(object? obj) =>
            obj is GeoPoint<T> other && Equals(other);

        /// <summary>
        /// Determines whether the specified <see cref="GeoPoint{T}"/> represents the same location as this
        /// instance, by snapping each coordinate to the nearest <see cref="EqualityStep"/> multiple and
        /// comparing the resulting integer grid indices.
        /// </summary>
        /// <param name="other">The point to compare with this instance.</param>
        /// <returns>
        /// <see langword="true"/> if both points snap to the same grid cell (or are both at the same pole,
        /// regardless of longitude); otherwise <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>Why grid-snapping, not a tolerance window:</b> a tolerance window (<c>|a − b| ≤ ε</c>) is
        /// intentionally non-transitive and cannot be paired with a correct <see cref="GetHashCode"/>.
        /// Snapping both operands to the same <see cref="EqualityStep"/> grid before comparing removes that
        /// risk entirely: <see cref="Equals(GeoPoint{T})"/> and <see cref="GetHashCode"/> always operate on
        /// the same integer indices, so equal points are guaranteed to hash equally.
        /// </para>
        /// <para>
        /// <b>Grid step and precision:</b> <see cref="EqualityStep"/> is <c>max(2⁻³³, 2⁷ × ε_machine)</c>
        /// — pure powers of 2, so dividing a coordinate by the step is IEEE 754 exact for |coord| &lt; 2¹⁹.
        /// For <c>double</c> this gives 2⁻³³ ≈ 1.16 × 10⁻¹⁰ degrees (≈ 13 μm on Earth's surface); for
        /// <c>float</c>, 2⁻¹⁶ ≈ 1.53 × 10⁻⁵ degrees, which matches <c>float</c>'s natural precision at
        /// coordinate scale.
        /// Coordinates produced by <see cref="GeoVector{T}.Travel"/> or
        /// <see cref="GeoVector{T}.Intersections"/> are pre-quantized to a grid ≥ <see cref="EqualityStep"/>,
        /// so two identical trig computations always land on the same grid point and compare equal.
        /// </para>
        /// <para>
        /// <b>Known limitation — grid-boundary straddling:</b> two values that differ by less than half a
        /// step but straddle a grid boundary compare as <em>unequal</em>. This is unavoidable for any
        /// snap-to-grid comparison; the step size is chosen small enough that this only affects values
        /// within ~6 × 10⁻¹¹ degrees of a boundary, which cannot arise from normal trig computation paths.
        /// </para>
        /// <para>
        /// <b>Longitude wraps around, latitude doesn't:</b> longitude is compared via
        /// <see cref="SnapCircleIndex"/> which normalizes to <c>[0°, 360°)</c> before snapping, so values
        /// on opposite sides of the antimeridian map to the same index.
        /// </para>
        /// </remarks>
        public bool Equals(GeoPoint<T> other)
        {
            T latIdx = SnapIndex(Latitude);
            T otherLatIdx = SnapIndex(other.Latitude);

            // Any longitude at a pole refers to the same physical point.
            if (latIdx == SnapIndex(MaxLatitude) && otherLatIdx == SnapIndex(MaxLatitude)) return true;
            if (latIdx == SnapIndex(MinLatitude) && otherLatIdx == SnapIndex(MinLatitude)) return true;

            return latIdx == otherLatIdx
                && SnapCircleIndex(Longitude) == SnapCircleIndex(other.Longitude);
        }

        /// <summary>
        /// Returns a hash code consistent with <see cref="Equals(GeoPoint{T})"/>: both latitude and
        /// longitude are snapped to <see cref="EqualityStep"/> grid indices (the exact same values
        /// <see cref="Equals(GeoPoint{T})"/> compares on), so equal points always hash equally.
        /// At either pole all longitudes refer to the same geographic point, so longitude is excluded
        /// from the hash when the latitude index equals the pole index (matching
        /// <see cref="Equals(GeoPoint{T})"/>'s pole handling).
        /// </summary>
        public override int GetHashCode()
        {
            T latIdx = SnapIndex(Latitude);
            // At a pole every longitude is the same physical point; longitude must not affect the hash.
            if (latIdx == SnapIndex(MaxLatitude) || latIdx == SnapIndex(MinLatitude))
                return latIdx.GetHashCode();
            return ObjectUtils.ComputeHash(latIdx, SnapCircleIndex(Longitude));
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
        public string ToString(string? format, IFormatProvider? formatProvider)
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
                // absPos is now a fractional minute; multiply by 60 (SecondsInMinute) to get seconds.
                absPos = (absPos - minutes) * SecondsInMinute;
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
        /// <param name="result">The parsed point, or <c>default</c> on failure.</param>
        /// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string coordinates, out GeoPoint<T> result)
            => TryParse(coordinates, [], out result);

        /// <summary>
        /// Attempts to parse a combined coordinate string using the specified cultures.
        /// </summary>
        public static bool TryParse(string coordinates, CultureInfo[] cultureInfos, out GeoPoint<T> result)
        {
            try
            {
                result = new GeoPoint<T>(coordinates, cultureInfos);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        /// <summary>
        /// Attempts to parse separate latitude and longitude strings using the current culture then
        /// the invariant culture.
        /// </summary>
        /// <param name="latitudeString">Latitude string (e.g. <c>"N48°51'"</c> or <c>"48.8566"</c>).</param>
        /// <param name="longitudeString">Longitude string (e.g. <c>"E2°21'"</c> or <c>"2.3522"</c>).</param>
        /// <param name="result">The parsed point, or <c>default</c> on failure.</param>
        /// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string latitudeString, string longitudeString, out GeoPoint<T> result)
            => TryParse(latitudeString, longitudeString, [], out result);

        /// <summary>
        /// Attempts to parse separate latitude and longitude strings using the specified cultures.
        /// </summary>
        public static bool TryParse(string latitudeString, string longitudeString, CultureInfo[] cultureInfos, out GeoPoint<T> result)
        {
            try
            {
                result = new GeoPoint<T>(latitudeString, longitudeString, cultureInfos);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        #endregion

        #region Operators

        /// <summary>
        /// Determines whether two geographic points are equal.
        /// </summary>
        public static bool operator ==(GeoPoint<T> left, GeoPoint<T> right) => left.Equals(right);

        /// <summary>
        /// Determines whether two geographic points are not equal.
        /// </summary>
        public static bool operator !=(GeoPoint<T> left, GeoPoint<T> right) => !left.Equals(right);

        #endregion

        #region Utility

        // Regex instances are expensive to compile; cache one per unique (nativeDigits, decimalSeparator) pair.
        private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();

        /// <summary>
        /// Builds or retrieves from cache the regex used to parse coordinate values from strings.
        /// </summary>
        internal static Regex BuildRegexCoordinates(CultureInfo culture)
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
                // Anchors (^ / $) ensure the entire input is consumed, not just a valid substring;
                // \s* allows optional surrounding whitespace while rejecting embedded garbage.
                // Structure: degrees [° [minutes [' [seconds] ["]]]]
                // The apostrophe (') and double-quote (") DMS separators are consumed when present.
                // Seconds can only appear after minutes (outer optional group requires minutes first).
                return new Regex(
                    @$"^\s*(?<modifier>W|E|N|S|\-|\+)?(?<degrees>{number})(?:°(?:(?<minutes>{number})(?:'(?<seconds>{number})?""?)?)?)?\s*$",
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
