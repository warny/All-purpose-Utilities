using System;
using System.Globalization;
using System.Numerics;
using Utils.Geography.Display;

namespace Utils.Geography.Model
{
    /// <summary>
    /// A BoundingBox&lt;T&gt; represents an immutable set of two latitude and two longitude coordinates.
    /// </summary>
    /// <typeparam name="T">
    /// Numeric type implementing <see cref="IFloatingPointIeee754{T}"/> (e.g., <c>float</c>, <c>double</c>, <c>decimal</c>).
    /// You may optionally add <c>IDivisionOperators&lt;T, T, T&gt;</c> if you need advanced math capabilities.
    /// </typeparam>
    public sealed class BoundingBox<T> : IFormattable, IEquatable<BoundingBox<T>>, IEqualityOperators<BoundingBox<T>, BoundingBox<T>, bool>
        where T : struct, IFloatingPointIeee754<T>
    {
        /// <summary>
        /// Creates a new <see cref="BoundingBox{T}"/> from a comma-separated string of coordinates in the order
        /// <c>minLat, minLon, maxLat, maxLon</c>. All coordinate values must be in degrees.
        /// </summary>
        /// <param name="boundingBoxstring">The string that describes the BoundingBox.</param>
        /// <returns>A new BoundingBox&lt;T&gt; with the given coordinates.</returns>
        /// <exception cref="ArgumentException">If the string cannot be parsed or describes an invalid bounding box.</exception>
        public static BoundingBox<T> FromString(string boundingBoxstring)
        {
            // ParseCoordinatestring is presumably a utility that splits into T[] of length 4.
            T[] coordinates = CoordinatesUtil<T>.ParseCoordinatestring(boundingBoxstring, 4);
            return new BoundingBox<T>(coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
        }

        /// <summary>
        /// The lower-left point of this bounding box (stores min lat, min lon).
        /// </summary>
        private readonly GeoPoint<T> point1;

        /// <summary>
        /// The upper-right point of this bounding box (stores max lat, max lon).
        /// </summary>
        private readonly GeoPoint<T> point2;

        /// <summary>
        /// The maximum latitude coordinate of this bounding box in degrees.
        /// </summary>
        public T MaxLatitude => point2.Latitude;

        /// <summary>
        /// The maximum longitude coordinate of this bounding box in degrees.
        /// </summary>
        public T MaxLongitude => point2.Longitude;

        /// <summary>
        /// The minimum latitude coordinate of this bounding box in degrees.
        /// </summary>
        public T MinLatitude => point1.Latitude;

        /// <summary>
        /// The minimum longitude coordinate of this bounding box in degrees.
        /// </summary>
        public T MinLongitude => point1.Longitude;

        /// <summary>
        /// Creates a <see cref="BoundingBox{T}"/> from four latitude/longitude strings.
        /// </summary>
        /// <param name="minLatitude">Minimum latitude string.</param>
        /// <param name="minLongitude">Minimum longitude string.</param>
        /// <param name="maxLatitude">Maximum latitude string.</param>
        /// <param name="maxLongitude">Maximum longitude string.</param>
        /// <exception cref="ArgumentException">If any coordinate is invalid or the bounding box is invalid.</exception>
        public BoundingBox(string minLatitude, string minLongitude, string maxLatitude, string maxLongitude)
        {
            // Construct the points
            var p1 = new GeoPoint<T>(minLatitude, minLongitude);
            var p2 = new GeoPoint<T>(maxLatitude, maxLongitude);

            // Ensure p1 is "lower-left" and p2 is "upper-right"
            (point1, point2) = ValidateBoundingBox(p1, p2);
        }

        /// <summary>
        /// Creates a <see cref="BoundingBox{T}"/> from four numeric coordinates.
        /// </summary>
        /// <param name="minLatitude">The minimum latitude coordinate in degrees.</param>
        /// <param name="minLongitude">The minimum longitude coordinate in degrees.</param>
        /// <param name="maxLatitude">The maximum latitude coordinate in degrees.</param>
        /// <param name="maxLongitude">The maximum longitude coordinate in degrees.</param>
        /// <exception cref="ArgumentException">If the coordinates describe an invalid bounding box.</exception>
        public BoundingBox(T minLatitude, T minLongitude, T maxLatitude, T maxLongitude)
        {
            var p1 = new GeoPoint<T>(minLatitude, minLongitude);
            var p2 = new GeoPoint<T>(maxLatitude, maxLongitude);

            (point1, point2) = ValidateBoundingBox(p1, p2);
        }

        /// <summary>
        /// Ensures that point1 is always the lower-left corner and point2 is always the upper-right corner.
        /// </summary>
        /// <param name="p1">Candidate first point.</param>
        /// <param name="p2">Candidate second point.</param>
        /// <returns>A tuple containing the corrected <c>(point1, point2)</c> order.</returns>
        /// <exception cref="ArgumentException">If the bounding box is invalid (e.g., minLat > maxLat if they can't be swapped).</exception>
        private static (GeoPoint<T> lowerLeft, GeoPoint<T> upperRight) ValidateBoundingBox(
            GeoPoint<T> p1,
            GeoPoint<T> p2
        )
        {
            // We want to reorder if p1 is not actually the min corner.
            T minLat = T.Min(p1.Latitude, p2.Latitude);
            T maxLat = T.Max(p1.Latitude, p2.Latitude);
            T minLon = T.Min(p1.Longitude, p2.Longitude);
            T maxLon = T.Max(p1.Longitude, p2.Longitude);

            // Recreate points in the correct order
            var lowerLeft = new GeoPoint<T>(minLat, minLon);
            var upperRight = new GeoPoint<T>(maxLat, maxLon);

            // If needed, add any logic to check for degenerate bounding boxes.
            // For now, we simply allow min == max as a degenerate bounding box.
            return (lowerLeft, upperRight);
        }

        /// <summary>
        /// Determines if this bounding box contains the given <paramref name="geoPoint"/>.
        /// </summary>
        /// <param name="geoPoint">A geographic point.</param>
        /// <returns><see langword="true"/> if the bounding box contains the point, otherwise <see langword="false"/>.</returns>
        public bool Contains(GeoPoint<T> geoPoint)
        {
            return (MinLatitude <= geoPoint.Latitude && MaxLatitude >= geoPoint.Latitude)
                && (MinLongitude <= geoPoint.Longitude && MaxLongitude >= geoPoint.Longitude);
        }

        /// <summary>
        /// Checks if this <see cref="BoundingBox{T}"/> intersects with another one.
        /// </summary>
        /// <param name="boundingBox">The bounding box to test.</param>
        /// <returns><see langword="true"/> if the two bounding boxes intersect; otherwise <see langword="false"/>.</returns>
        public bool Intersects(BoundingBox<T> boundingBox)
        {
            // Quick reference to boundaries
            return (this.MaxLatitude >= boundingBox.MinLatitude)
                && (this.MaxLongitude >= boundingBox.MinLongitude)
                && (this.MinLatitude <= boundingBox.MaxLatitude)
                && (this.MinLongitude <= boundingBox.MaxLongitude);
        }

        /// <summary>
        /// Gets a new <see cref="GeoPoint{T}"/> at the center of this bounding box.
        /// </summary>
        /// <returns>A <see cref="GeoPoint{T}"/> representing the center.</returns>
        public GeoPoint<T> GetCenterpoint()
        {
            // (minLat + maxLat)/2, (minLon + maxLon)/2
            T latOffset = (MaxLatitude - MinLatitude) / T.CreateChecked(2);
            T lonOffset = (MaxLongitude - MinLongitude) / T.CreateChecked(2);
            return new GeoPoint<T>(MinLatitude + latOffset, MinLongitude + lonOffset);
        }

        /// <summary>
        /// Returns the latitude span of this bounding box in degrees.
        /// </summary>
        public T LatitudeSpan => MaxLatitude - MinLatitude;

        /// <summary>
        /// Returns the longitude span of this bounding box in degrees.
        /// </summary>
        public T LongitudeSpan => MaxLongitude - MinLongitude;

        /// <summary>
        /// Determines whether the specified bounding box has the same boundaries as the current instance.
        /// </summary>
        /// <param name="other">Bounding box to compare.</param>
        /// <returns><see langword="true"/> if both bounding boxes have identical coordinates; otherwise <see langword="false"/>.</returns>
        public bool Equals(BoundingBox<T> other)
        {
            return this.MaxLatitude.Equals(other.MaxLatitude)
                    && this.MaxLongitude.Equals(other.MaxLongitude)
                    && this.MinLatitude.Equals(other.MinLatitude)
                    && this.MinLongitude.Equals(other.MinLongitude);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current bounding box.
        /// </summary>
        /// <param name="obj">Object to compare with the current bounding box.</param>
        /// <returns><see langword="true"/> if the specified object is equal to the current bounding box; otherwise <see langword="false"/>.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            return obj is BoundingBox<T> other && Equals(other);
        }

        /// <summary>
        /// Returns a hash code for the current bounding box.
        /// </summary>
        /// <returns>A hash code for the current bounding box.</returns>
        public override int GetHashCode()
        {
            return Objects.ObjectUtils.ComputeHash(MaxLatitude, MaxLongitude, MinLatitude, MinLongitude);
        }

        /// <summary>
        /// Returns a string that represents this bounding box using default formatting.
        /// </summary>
        /// <returns>A string representation of the bounding box.</returns>
        public override string ToString() => $"minLatitude={MinLatitude}, minLongitude={MinLongitude}, maxLatitude={MaxLatitude}, maxLongitude={MaxLongitude}";

        /// <summary>
        /// Returns a string that represents this bounding box in a given format.
        /// </summary>
        /// <param name="format">Format string for numeric values.</param>
        /// <returns>A string representing this bounding box.</returns>
        public string ToString(string format) => ToString(format, CultureInfo.CurrentCulture);

        /// <summary>
        /// Returns a string that represents this bounding box in a given format and culture.
        /// </summary>
        /// <param name="format">Format string for numeric values.</param>
        /// <param name="formatProvider">Culture-specific format provider.</param>
        /// <returns>A string representing this bounding box.</returns>
        public string ToString(string format, IFormatProvider formatProvider)
        {
            return string.Format(
                    formatProvider,
                    "minLatitude={0}, minLongitude={1}, maxLatitude={2}, maxLongitude={3}",
                    MinLatitude.ToString(format, formatProvider),
                    MinLongitude.ToString(format, formatProvider),
                    MaxLatitude.ToString(format, formatProvider),
                    MaxLongitude.ToString(format, formatProvider)
            );
        }

        /// <summary>
        /// Determines whether two bounding boxes are equal.
        /// </summary>
        /// <param name="left">First bounding box to compare.</param>
        /// <param name="right">Second bounding box to compare.</param>
        /// <returns><see langword="true"/> if both bounding boxes are equal; otherwise <see langword="false"/>.</returns>
        public static bool operator ==(BoundingBox<T> left, BoundingBox<T> right) => left.Equals(right);

        /// <summary>
        /// Determines whether two bounding boxes are not equal.
        /// </summary>
        /// <param name="left">First bounding box to compare.</param>
        /// <param name="right">Second bounding box to compare.</param>
        /// <returns><see langword="true"/> if the bounding boxes differ; otherwise <see langword="false"/>.</returns>
        public static bool operator !=(BoundingBox<T> left, BoundingBox<T> right) => !left.Equals(right);
    }
}
