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
    public class GeoVector : 
        GeoPoint, 
        IEquatable<GeoVector>,
        IUnaryNegationOperators<GeoVector, GeoVector>
    {
        /// <summary>
        /// Bearing in degrees relative to the north, clockwise
        /// </summary>
        public double Bearing { get; }

        /// <summary>
        /// Bearing in degrees relative to the north, clockwise
        /// </summary>
        public double θ => Bearing;

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
				if (!double.TryParse(coordinatesStrings[2], NumberStyles.Float, cultureInfo, out double direction)) continue;
				Bearing = MathEx.Mod(direction, 360);
				if (ParseCoordinates(coordinatesStrings[0], coordinatesStrings[1], cultureInfo, regExCoordinate)) return;
			}

			throw new ArgumentException($"\"{coordinates}\" n'est pas un vecteur valide");
		}

		/// <summary>
		/// Create geovector at <paramref name="geoPoint"/> heading to <paramref name="bearing"/>
		/// </summary>
		/// <param name="geoPoint">Point</param>
		/// <param name="bearing">bearing direction</param>
		public GeoVector(GeoPoint geoPoint, double bearing) : base(geoPoint)
		{
			Bearing = degree.Normalize0To2Max(bearing);
		}

		/// <summary>
		/// Compute geovector direction from <paramref name="geoPoint"/> to <paramref name="destination"/>
		/// </summary>
		/// <param name="geoPoint">start point</param>
		/// <param name="destination">destination point</param>
		public GeoVector(GeoPoint geoPoint, GeoPoint destination) : base(geoPoint)
		{
			Bearing = ComputeBearing(geoPoint, destination);
		}


		/// <summary>
		/// Create a geoVector at given coordinates heading to <paramref name="bearing"/>
		/// </summary>
		/// <param name="latitude">Latitude</param>
		/// <param name="longitude">Longitude</param>
		/// <param name="bearing">Heading direction</param>
		public GeoVector(double latitude, double longitude, double bearing) : base(latitude, longitude)
		{
			Bearing = degree.Normalize0To2Max(bearing);
		}

		/// <summary>
		/// Create a geoVector at given coordinates heading to <paramref name="bearing"/>
		/// </summary>
		/// <param name="latitude">Latitude</param>
		/// <param name="longitude">Longitude</param>
		/// <param name="bearing">Heading direction</param>
		public GeoVector(string latitudeString, string longitudeString, double bearing, params CultureInfo[] cultureInfos) : base(latitudeString, longitudeString, cultureInfos)
		{
			Bearing = degree.Normalize0To2Max(bearing);
		}

		public void Deconstruct(out double latitude, out double longitude, out double bearing) {
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
		public static double ComputeBearing(GeoPoint A, GeoPoint B)
		{
			if (comparer.Equals(A.Longitude, B.Longitude))
			{
				return A.Latitude > B.Latitude ? 180 : 0;
			}
			if (comparer.Equals(A.Longitude, B.Longitude - 180) || comparer.Equals(A.Longitude, B.Longitude + 180))
			{
				return A.Latitude > -B.Latitude ? 180 : 0;
			}

			double y = degree.Sin(B.λ - A.λ) * degree.Cos(B.φ);
			double x = degree.Cos(A.φ) * degree.Sin(B.φ) - degree.Sin(A.φ) * degree.Cos(B.φ) * degree.Cos(B.λ - A.λ);
			return degree.Normalize0To2Max(degree.Atan2(y, x));
		}

        /// <summary>
        /// Calculates the vector resulting from a displacement.
        /// </summary>
        /// <param name="angle">The angle of displacement.</param>
        /// <returns>The resulting GeoVector after the displacement.</returns>
        public GeoVector Travel(double angle)
        {
            // Determines if the angle is negative
            bool negative = Math.Sign(angle) == -1;

            // Normalizes the angle to be within the range of 0 to 360 degrees
            angle = degree.Normalize0To2Max(angle);

            // No displacement if angle is 0
            if (angle == 0) return this;

            // Reverse direction if angle is 180 degrees
            if (angle == 180) return new GeoVector(-this.Latitude, this.Longitude + 180, this.Bearing + 180);

            // Corrects bearing based on angle and direction
            double bearingCorrection = (angle <= 180) ^ negative ? 180 : 0;

            // Handling specific cases for poles
            if (this.φ == 90) return new GeoVector(90 - angle, bearingCorrection + this.λ, bearingCorrection);
            if (this.φ == -90) return new GeoVector(-90 + angle, bearingCorrection + this.λ, 180 + bearingCorrection);

            // Calculates new latitude (φ2) based on the given angle and current latitude and bearing
            double φ2 = degree.Asin(
                degree.Sin(φ) * degree.Cos(angle)
                + degree.Cos(φ) * degree.Sin(angle) * degree.Cos(-Bearing)
            );

            // Handling specific cases for poles after the calculation
            if (φ2 == 90) return new GeoVector(90, λ, MathEx.Mod(Bearing + 180 + bearingCorrection, 360));
            if (φ2 == -90) return new GeoVector(-90, λ, MathEx.Mod(Bearing + 180 + bearingCorrection, 360));

            // Calculates new longitude (λ2) based on the given angle, current longitude, latitude, and bearing
            double λ2 = λ + degree.Atan2(
                degree.Sin(Bearing) * degree.Sin(angle) * degree.Cos(φ),
                degree.Cos(angle) - degree.Sin(φ) * degree.Sin(φ2)
            );

            // Rounds latitude and longitude values
            φ2 = Math.Round(φ2, 5);
            λ2 = Math.Round(λ2, 5);

            // Creates a GeoPoint representing the arrival coordinates
            GeoPoint arrival = new GeoPoint(φ2, λ2);

            // Calculates the bearing for the resulting GeoVector
            double bearing = MathEx.Mod(bearingCorrection + ComputeBearing(arrival, this), 360);

            // Returns a new GeoVector representing the displacement
            return new GeoVector(arrival, bearing);
        }

        /// <summary>
        /// Calculates the intersections between 2 great circles.
        /// </summary>
        /// <param name="other">Great circle to compare.</param>
        /// <returns>An array of intersection points (<see cref="GeoPoint"/>) or <see cref="null"/> if the great circles are the same.</returns>
        public GeoPoint[] Intersections(GeoVector other)
        {
            // Calculate temporary vectors representing the intersection points
            var temp = (a: Travel(this.AngleWith(other)), b: (-this).Travel(this.AngleWith(other)));

            // If both vectors describe the same great circle, no intersection points can be returned
            if (other.In(temp.a, -temp.a, temp.b, -temp.b))
            {
                return [];
            }

            // If both vectors follow meridians, they intersect at the poles
            if (MathEx.Mod(this.Bearing, 180) == 0 && MathEx.Mod(other.Bearing, 180) == 0)
            {
                return
                [
                    new GeoPoint(90, 0),
                    new GeoPoint(-90, 180)
                ];
            }

            // If 'this' vector follows a meridian, calculate intersection points based on 'other' vector
            if (MathEx.Mod(this.Bearing, 180) == 0)
            {
                var result = other.Travel(this.λ - other.λ);

                return
                [
                    new GeoPoint(result.φ, result.λ),
                    new GeoPoint(-result.φ, result.λ + 180),
                ];
            }

            // If 'other' vector follows a meridian, calculate intersection points based on 'this' vector
            if (MathEx.Mod(other.Bearing, 180) == 0)
            {
                var result = this.Travel(this.λ - other.λ);

                return
                [
                    new GeoPoint(result.φ, result.λ),
                    new GeoPoint(-result.φ, result.λ + 180),
                ];
            }

            // Calculate various angles and distances to determine intersection points
            double Δφ = this.φ - other.φ;
            double Δλ = this.λ - other.λ;

            double δ12 = 2 * degree.Asin(Math.Sqrt(Math.Pow(degree.Sin(Δφ / 2), 2) + degree.Cos(this.φ) * degree.Cos(other.φ) * Math.Pow(degree.Sin(Δλ / 2), 2))); // Angular dist. p1–p2
            double θa = degree.Acos((degree.Sin(other.φ) - degree.Sin(this.φ) * degree.Cos(δ12)) / (degree.Sin(δ12) * degree.Cos(this.φ)));
            double θb = degree.Acos((degree.Sin(this.φ) - degree.Sin(other.φ) * degree.Cos(δ12)) / (degree.Sin(δ12) * degree.Cos(other.φ))); // Initial / final bearings between points 1 & 2

            double θ12, θ21;
            if (degree.Sin(Δλ) <= 0)
            {
                θ12 = θa;
                θ21 = 360 - θb;
            }
            else
            {
                θ12 = 360 - θa;
                θ21 = θb;
            }

            double α1 = this.Bearing - θ12; // Angle p2–p1–p3
            double α2 = θ21 - other.Bearing; // Angle p1–p2–p3

            double α3 = degree.Acos(-degree.Cos(α1) * degree.Cos(α2) + degree.Sin(α1) * degree.Sin(α2) * degree.Cos(δ12)); // Angle p1–p2–p3
            double δ13 = degree.Atan2(degree.Sin(δ12) * degree.Sin(α1) * degree.Sin(α2), degree.Cos(α2) + degree.Cos(α1) * degree.Cos(α3)); // Angular dist. p1–p3
            double φ3 = degree.Asin(degree.Sin(this.φ) * degree.Cos(δ13) + degree.Cos(this.φ) * degree.Sin(δ13) * degree.Cos(this.Bearing)); // p3 lat
            double Δλ13 = degree.Atan2(degree.Sin(this.Bearing) * degree.Sin(δ13) * degree.Cos(this.φ), degree.Cos(δ13) - degree.Sin(this.φ) * degree.Sin(φ3)); // Long p1–p3
            double λ3 = this.λ + Δλ13;

            // Round latitude and longitude values
            φ3 = Math.Round(φ3, 5);
            λ3 = Math.Round(λ3, 5);

            return
            [
                new GeoPoint(φ3, λ3),
                new GeoPoint(-φ3, λ3 + 180),
            ];
        }

        public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj)) return true;
			if (obj is GeoVector p) return Equals(p);
			return base.Equals(obj);
		}

		public override int GetHashCode()
			=> Objects.ObjectUtils.ComputeHash(Latitude, Longitude, Bearing);

		public bool Equals(GeoVector other) 
			=> comparer.Equals(Bearing, other.Bearing) && base.Equals(other);

		public static GeoVector operator -(GeoVector geoVector) => new GeoVector(geoVector.φ, geoVector.λ, 180 + geoVector.θ);

		public override string ToString(string format, IFormatProvider formatProvider)
		{
			formatProvider ??= CultureInfo.InvariantCulture;
			var textInfo = (TextInfo)formatProvider?.GetFormat(typeof(TextInfo));

			return base.ToString(format, formatProvider) + $"{textInfo?.ListSeparator ?? ","} {Bearing:##0.##}";
		}
	}
}
