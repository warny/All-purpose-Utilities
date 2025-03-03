using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Geography.Display;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Geography.Model
{
	/// <summary>
	/// Represents a vector of displacement on a spherical geodesic with a bearing (heading direction).
	/// </summary>
	/// <typeparam name="T">The numeric type used for calculations, typically a floating point.</typeparam>
	/// <remarks>
	/// This class is fully ported from the JavaScript implementation presented in
	/// https://www.movable-type.co.uk/scripts/latlong.html
	/// </remarks>
	public class GeoVector<T> : GeoPoint<T>, IEquatable<GeoVector<T>>, IUnaryNegationOperators<GeoVector<T>, GeoVector<T>>
		where T : struct, IFloatingPointIeee754<T>, IDivisionOperators<T, T, T>
	{
		/// <summary>
		/// Bearing in degrees relative to north, measured clockwise. Immutable.
		/// </summary>
		public T Bearing { get; }

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
			: base(ParseVectorString(coordinates, cultureInfos, out T bearing).latitude,
				   ParseVectorString(coordinates, cultureInfos, out bearing).longitude)
		{
			Bearing = bearing;
		}

		/// <summary>
		/// Creates a <see cref="GeoVector{T}"/> from a <paramref name="geoPoint"/> plus a given <paramref name="bearing"/>.
		/// </summary>
		/// <param name="geoPoint">Base geographic point (latitude/longitude).</param>
		/// <param name="bearing">Heading direction in degrees.</param>
		public GeoVector(GeoPoint<T> geoPoint, T bearing)
			: base(geoPoint.Latitude, geoPoint.Longitude)
		{
			Bearing = degree.Normalize0To2Max(bearing);
		}

		/// <summary>
		/// Creates a <see cref="GeoVector{T}"/> from two geographic points, calculating the bearing from <paramref name="geoPoint"/> to <paramref name="destination"/>.
		/// </summary>
		/// <param name="geoPoint">Start point.</param>
		/// <param name="destination">Destination point.</param>
		public GeoVector(GeoPoint<T> geoPoint, GeoPoint<T> destination)
			: base(geoPoint.Latitude, geoPoint.Longitude)
		{
			Bearing = ComputeBearing(geoPoint, destination);
		}

		/// <summary>
		/// Creates a <see cref="GeoVector{T}"/> from numeric latitude, longitude, and bearing.
		/// </summary>
		/// <param name="latitude">Latitude in degrees.</param>
		/// <param name="longitude">Longitude in degrees.</param>
		/// <param name="bearing">Heading direction in degrees.</param>
		public GeoVector(T latitude, T longitude, T bearing)
			: base(latitude, longitude)
		{
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
			: base(latitudeString, longitudeString, cultureInfos)
		{
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
					new[] { cultureInfo.TextInfo.ListSeparator },
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
				var regex = BuildRegexCoordinates(cultureInfo);

				// parse latitude
				var lat = ParseCoordinate(
					CoordinateDirection.Latitude,
					parts[0],
					PositiveLatitude,
					NegativeLatitude,
					cultureInfo,
					regex
				);
				if (T.IsNaN(lat)) continue; // failed parse

				// parse longitude
				var lon = ParseCoordinate(
					CoordinateDirection.Longitude,
					parts[1],
					PositiveLongitude,
					NegativeLongitude,
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
			latitude = Latitude;
			longitude = Longitude;
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
			if (comparer.Equals(A.Longitude, B.Longitude))
			{
				// Vertical line test
				return A.Latitude > B.Latitude ? degree.StraightAngle : T.Zero;
			}
			if (comparer.Equals(A.Longitude, B.Longitude - degree.StraightAngle)
				|| comparer.Equals(A.Longitude, B.Longitude + degree.StraightAngle))
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
			// (Unchanged logic from your snippet—just referencing the new base's immutability.)
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
			T newBearing = MathEx.Mod(bearingCorrection + ComputeBearing(arrival, this), degree.Perigon);

			return new GeoVector<T>(arrival, newBearing);
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
			// Same logic from your snippet, with minimal changes.

			var temp = (
				a: Travel(this.AngleWith(other)),
				b: (-this).Travel(this.AngleWith(other))
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
				return new[]
				{
					new GeoPoint<T>(degree.RightAngle, T.Zero),
					new GeoPoint<T>(-degree.RightAngle, degree.StraightAngle)
				};
			}

			if (MathEx.Mod(this.Bearing, degree.StraightAngle) == T.Zero)
			{
				var result = other.Travel(this.λ - other.λ);
				return new[]
				{
					new GeoPoint<T>(result.φ, result.λ),
					new GeoPoint<T>(-result.φ, result.λ + degree.StraightAngle),
				};
			}

			if (MathEx.Mod(other.Bearing, degree.StraightAngle) == T.Zero)
			{
				var result = this.Travel(this.λ - other.λ);
				return new[]
				{
					new GeoPoint<T>(result.φ, result.λ),
					new GeoPoint<T>(-result.φ, result.λ + degree.StraightAngle),
				};
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

			return new[]
			{
				new GeoPoint<T>(φ3, λ3),
				new GeoPoint<T>(-φ3, λ3 + degree.StraightAngle)
			};
		}

		#endregion

		#region Equality & Overrides

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj)) return true;
			if (obj is GeoVector<T> vector) return Equals(vector);
			return base.Equals(obj);
		}

		/// <inheritdoc />
		public bool Equals(GeoVector<T> other)
			=> comparer.Equals(Bearing, other.Bearing) && base.Equals(other);

		/// <inheritdoc />
		public override int GetHashCode()
			=> ObjectUtils.ComputeHash(Latitude, Longitude, Bearing);

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

		/// <inheritdoc />
		public override string ToString(string format, IFormatProvider formatProvider)
		{
			formatProvider ??= CultureInfo.InvariantCulture;
			var textInfo = (TextInfo)formatProvider.GetFormat(typeof(TextInfo));

			// Example: "Latitude, Longitude, Bearing"
			return $"{base.ToString(format, formatProvider)}{textInfo?.ListSeparator ?? ","} {Bearing:##0.##}";
		}

		#endregion
	}
}
