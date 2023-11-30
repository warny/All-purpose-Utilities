using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Geography.Model
{
    /// <summary>
    /// Vector of displacement on a spherical geodesic
    /// </summary>
    /// <remarks>This class has been fully ported from JavaScript presented in https://www.movable-type.co.uk/scripts/latlong.html</remarks>
    public class GeoVector<T> : 
        GeoPoint<T>, 
        IEquatable<GeoVector<T>>,
        IUnaryNegationOperators<GeoVector<T>, GeoVector<T>>
    where T : struct, IFloatingPointIeee754<T>
    {
        /// <summary>
        /// Bearing in degrees relative to the north, clockwise
        /// </summary>
        public T Bearing { get; }

        /// <summary>
        /// Bearing in degrees relative to the north, clockwise
        /// </summary>
        public T θ => Bearing;

        /// <summary>
        /// Create a geoVector at given <paramref name="coordinates"/> 
        /// </summary>
        /// <param name="coordinates">Coordinates including bearing</param>
        /// <param name="cultureInfos">culture used to parse coordinates</param>
        public GeoVector(string coordinates, params CultureInfo[] cultureInfos) {
			if (cultureInfos.Length == 0) cultureInfos = new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture };

			foreach (var cultureInfo in cultureInfos)
			{
				var coordinatesStrings = coordinates.Split(new[] { cultureInfo.TextInfo.ListSeparator }, StringSplitOptions.None);
				if (coordinatesStrings.Length != 3) continue;
				Regex regExCoordinate = BuildRegexCoordinates(cultureInfo);
				if (!T.TryParse(coordinatesStrings[2], NumberStyles.Float, cultureInfo, out T direction)) continue;
				Bearing = MathEx.Mod(direction, degree.Perigon);
				if (ParseCoordinates(coordinatesStrings[0], coordinatesStrings[1], cultureInfo, regExCoordinate)) return;
			}

			throw new ArgumentException($"\"{coordinates}\" n'est pas un vecteur valide");
		}

		/// <summary>
		/// Create geovector at <paramref name="geoPoint"/> heading to <paramref name="bearing"/>
		/// </summary>
		/// <param name="geoPoint">Point</param>
		/// <param name="bearing">bearing direction</param>
		public GeoVector(GeoPoint<T> geoPoint, T bearing) : base(geoPoint)
		{
			Bearing = degree.Normalize0To2Max(bearing);
		}

		/// <summary>
		/// Compute geovector direction from <paramref name="geoPoint"/> to <paramref name="destination"/>
		/// </summary>
		/// <param name="geoPoint">start point</param>
		/// <param name="destination">destination point</param>
		public GeoVector(GeoPoint<T> geoPoint, GeoPoint<T> destination) : base(geoPoint)
		{
			Bearing = ComputeBearing(geoPoint, destination);
		}


		/// <summary>
		/// Create a geoVector at given coordinates heading to <paramref name="bearing"/>
		/// </summary>
		/// <param name="latitude">Latitude</param>
		/// <param name="longitude">Longitude</param>
		/// <param name="bearing">Heading direction</param>
		public GeoVector(T latitude, T longitude, T bearing) : base(latitude, longitude)
		{
			Bearing = degree.Normalize0To2Max(bearing);
		}

		/// <summary>
		/// Create a geoVector at given coordinates heading to <paramref name="bearing"/>
		/// </summary>
		/// <param name="latitude">Latitude</param>
		/// <param name="longitude">Longitude</param>
		/// <param name="bearing">Heading direction</param>
		public GeoVector(string latitudeString, string longitudeString, T bearing, params CultureInfo[] cultureInfos) : base(latitudeString, longitudeString, cultureInfos)
		{
			Bearing = degree.Normalize0To2Max(bearing);
		}

		public void Deconstruct(out T latitude, out T longitude, out T bearing) {
			latitude = Latitude;
			longitude = Longitude;
			bearing = Bearing;
		}

		/// <summary>
		/// Compute bearing from point A to point B
		/// </summary>
		/// <param name="A">Start point</param>
		/// <param name="B">End point</param>
		/// <returns>Bearing</returns>
		public static T ComputeBearing(GeoPoint<T> A, GeoPoint<T> B)
		{
			if (comparer.Equals(A.Longitude, B.Longitude))
			{
				return A.Latitude > B.Latitude ? degree.StraightAngle : T.Zero;
			}
			if (comparer.Equals(A.Longitude, B.Longitude - degree.StraightAngle) || comparer.Equals(A.Longitude, B.Longitude + degree.StraightAngle))
			{
				return A.Latitude > -B.Latitude ? degree.StraightAngle : T.Zero;
			}

			T y = degree.Sin(B.λ - A.λ) * degree.Cos(B.φ);
			T x = degree.Cos(A.φ) * degree.Sin(B.φ) - degree.Sin(A.φ) * degree.Cos(B.φ) * degree.Cos(B.λ - A.λ);
			return degree.Normalize0To2Max(degree.Atan2(y, x));
		}

        /// <summary>
        /// Calculates the vector resulting from a displacement.
        /// </summary>
        /// <param name="angle">The angle of displacement.</param>
        /// <returns>The resulting GeoVector<T> after the displacement.</returns>
        public GeoVector<T> Travel(T angle)
        {
            // Determines if the angle is negative
            bool negative = T.Sign(angle) == -1;

            // Normalizes the angle to be within the range of 0 to 360 degrees
            angle = degree.Normalize0To2Max(angle);

            // No displacement if angle is 0
            if (angle == T.Zero) return this;

            // Reverse direction if angle is 180 degrees
            if (angle == degree.StraightAngle) return new GeoVector<T>(-this.Latitude, this.Longitude + degree.StraightAngle, this.Bearing + degree.StraightAngle);

            // Corrects bearing based on angle and direction
            T bearingCorrection = (angle <= degree.StraightAngle) ^ negative ? degree.StraightAngle : T.Zero;

            // Handling specific cases for poles
            if (this.φ == degree.RightAngle) return new GeoVector<T>(degree.RightAngle - angle, bearingCorrection + this.λ, bearingCorrection);
            if (this.φ == -degree.RightAngle) return new GeoVector<T>(-degree.RightAngle + angle, bearingCorrection + this.λ, degree.StraightAngle + bearingCorrection);

            // Calculates new latitude (φ2) based on the given angle and current latitude and bearing
            T φ2 = degree.Asin(
                degree.Sin(φ) * degree.Cos(angle)
                + degree.Cos(φ) * degree.Sin(angle) * degree.Cos(-Bearing)
            );

            // Handling specific cases for poles after the calculation
            if (φ2 == degree.RightAngle) return new GeoVector<T>(degree.RightAngle, λ, MathEx.Mod(Bearing + degree.StraightAngle + bearingCorrection, degree.Perigon));
            if (φ2 == -degree.RightAngle) return new GeoVector<T>(-degree.RightAngle, λ, MathEx.Mod(Bearing + degree.StraightAngle + bearingCorrection, degree.Perigon));

            // Calculates new longitude (λ2) based on the given angle, current longitude, latitude, and bearing
            T λ2 = λ + degree.Atan2(
                degree.Sin(Bearing) * degree.Sin(angle) * degree.Cos(φ),
                degree.Cos(angle) - degree.Sin(φ) * degree.Sin(φ2)
            );

            // Rounds latitude and longitude values
            φ2 = T.Round(φ2, 5);
            λ2 = T.Round(λ2, 5);

            // Creates a GeoPoint<T> representing the arrival coordinates
            GeoPoint<T> arrival = new GeoPoint<T>(φ2, λ2);

            // Calculates the bearing for the resulting GeoVector<T>
            T bearing = MathEx.Mod(bearingCorrection + ComputeBearing(arrival, this), degree.Perigon);

            // Returns a new GeoVector<T> representing the displacement
            return new GeoVector<T>(arrival, bearing);
        }

        /// <summary>
        /// Calculates the intersections between 2 great circles.
        /// </summary>
        /// <param name="other">Great circle to compare.</param>
        /// <returns>An array of intersection points (<see cref="GeoPoint<T>"/>) or <see cref="null"/> if the great circles are the same.</returns>
        public GeoPoint<T>[] Intersections(GeoVector<T> other)
        {
            // Calculate temporary vectors representing the intersection points
            var temp = (a: Travel(this.AngleWith(other)), b: (-this).Travel(this.AngleWith(other)));

            // If both vectors describe the same great circle, no intersection points can be returned
            if (other.In(temp.a, -temp.a, temp.b, -temp.b))
            {
                return [];
            }

            // If both vectors follow meridians, they intersect at the poles
            if (MathEx.Mod(this.Bearing, degree.StraightAngle) == T.Zero && MathEx.Mod(other.Bearing, degree.StraightAngle) == T.Zero)
            {
                return
                [
                    new GeoPoint<T>(degree.RightAngle, T.Zero),
                    new GeoPoint<T>(-degree.RightAngle, degree.StraightAngle)
                ];
            }

            // If 'this' vector follows a meridian, calculate intersection points based on 'other' vector
            if (MathEx.Mod(this.Bearing, degree.StraightAngle) == T.Zero)
            {
                var result = other.Travel(this.λ - other.λ);

                return
                [
                    new GeoPoint<T>(result.φ, result.λ),
                    new GeoPoint<T>(-result.φ, result.λ + degree.StraightAngle),
                ];
            }

            // If 'other' vector follows a meridian, calculate intersection points based on 'this' vector
            if (MathEx.Mod(other.Bearing, degree.StraightAngle) == T.Zero)
            {
                var result = this.Travel(this.λ - other.λ);

                return
                [
                    new GeoPoint<T>(result.φ, result.λ),
                    new GeoPoint<T>(-result.φ, result.λ + degree.StraightAngle),
                ];
            }

            // Calculate various angles and distances to determine intersection points
            T Δφ = this.φ - other.φ;
            T Δλ = this.λ - other.λ;

            T δ12 = (T.One + T.One) * degree.Asin(T.Sqrt(T.Pow(degree.Sin(Δφ / (T.One+ T.One)), (T.One + T.One)) + degree.Cos(this.φ) * degree.Cos(other.φ) * T.Pow(degree.Sin(Δλ / (T.One + T.One)), (T.One + T.One)))); // Angular dist. p1–p2
            T θa = degree.Acos((degree.Sin(other.φ) - degree.Sin(this.φ) * degree.Cos(δ12)) / (degree.Sin(δ12) * degree.Cos(this.φ)));
            T θb = degree.Acos((degree.Sin(this.φ) - degree.Sin(other.φ) * degree.Cos(δ12)) / (degree.Sin(δ12) * degree.Cos(other.φ))); // Initial / final bearings between points 1 & 2

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

            T α1 = this.Bearing - θ12; // Angle p2–p1–p3
            T α2 = θ21 - other.Bearing; // Angle p1–p2–p3

            T α3 = degree.Acos(-degree.Cos(α1) * degree.Cos(α2) + degree.Sin(α1) * degree.Sin(α2) * degree.Cos(δ12)); // Angle p1–p2–p3
            T δ13 = degree.Atan2(degree.Sin(δ12) * degree.Sin(α1) * degree.Sin(α2), degree.Cos(α2) + degree.Cos(α1) * degree.Cos(α3)); // Angular dist. p1–p3
            T φ3 = degree.Asin(degree.Sin(this.φ) * degree.Cos(δ13) + degree.Cos(this.φ) * degree.Sin(δ13) * degree.Cos(this.Bearing)); // p3 lat
            T Δλ13 = degree.Atan2(degree.Sin(this.Bearing) * degree.Sin(δ13) * degree.Cos(this.φ), degree.Cos(δ13) - degree.Sin(this.φ) * degree.Sin(φ3)); // Long p1–p3
            T λ3 = this.λ + Δλ13;

            // Round latitude and longitude values
            φ3 = T.Round(φ3, 5);
            λ3 = T.Round(λ3, 5);

            return
            [
                new GeoPoint<T>(φ3, λ3),
                new GeoPoint<T>(-φ3, λ3 + degree.StraightAngle),
            ];
        }

        public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj)) return true;
			if (obj is GeoVector<T> p) return Equals(p);
			return base.Equals(obj);
		}

		public override int GetHashCode()
			=> Objects.ObjectUtils.ComputeHash(Latitude, Longitude, Bearing);

		public bool Equals(GeoVector<T> other) 
			=> comparer.Equals(Bearing, other.Bearing) && base.Equals(other);

		public static GeoVector<T> operator -(GeoVector<T> geoVector) => new GeoVector<T>(geoVector.φ, geoVector.λ, degree.StraightAngle + geoVector.θ);

		public override string ToString(string format, IFormatProvider formatProvider)
		{
			formatProvider ??= CultureInfo.InvariantCulture;
			var textInfo = (TextInfo)formatProvider?.GetFormat(typeof(TextInfo));

			return base.ToString(format, formatProvider) + $"{textInfo?.ListSeparator ?? ","} {Bearing:##0.##}";
		}
	}
}
