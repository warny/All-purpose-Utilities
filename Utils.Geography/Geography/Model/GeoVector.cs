using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Geography.Model;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Geography.Model;

/// <summary>
/// Represents a vector of displacement on a spherical geodesic with a bearing (heading direction).
/// </summary>
/// <typeparam name="T">The numeric type used for calculations, typically a floating point.</typeparam>
/// <remarks>
/// This class is fully ported from the JavaScript implementation presented in 
/// https://www.movable-type.co.uk/scripts/latlong.html
/// </remarks>
public class GeoVector<T> : GeoPoint<T>, IEquatable<GeoVector<T>>, IUnaryNegationOperators<GeoVector<T>, GeoVector<T>>
	where T : struct, IFloatingPointIeee754<T>
{
	/// <summary>
	/// Bearing in degrees relative to the north, measured clockwise.
	/// </summary>
	public T Bearing { get; }

	/// <summary>
	/// Bearing in degrees relative to the north, measured clockwise.
	/// This is an alias for the Bearing property.
	/// </summary>
	public T θ => Bearing;

	#region Constructors

	/// <summary>
	/// Creates a GeoVector from a string of coordinates and a bearing.
	/// </summary>
	/// <param name="coordinates">String containing the latitude, longitude, and bearing.</param>
	/// <param name="cultureInfos">Culture information used to parse the coordinates.</param>
	public GeoVector(string coordinates, params CultureInfo[] cultureInfos)
	{
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

		throw new ArgumentException($"\"{coordinates}\" is not a valid vector");
	}

	/// <summary>
	/// Creates a GeoVector from a <paramref name="geoPoint"/> and a given <paramref name="bearing"/>.
	/// </summary>
	/// <param name="geoPoint">Geographical point.</param>
	/// <param name="bearing">Heading direction in degrees.</param>
	public GeoVector(GeoPoint<T> geoPoint, T bearing) : base(geoPoint)
	{
		Bearing = degree.Normalize0To2Max(bearing);
	}

	/// <summary>
	/// Creates a GeoVector from two geographical points, calculating the bearing between them.
	/// </summary>
	/// <param name="geoPoint">Start point.</param>
	/// <param name="destination">Destination point.</param>
	public GeoVector(GeoPoint<T> geoPoint, GeoPoint<T> destination) : base(geoPoint)
	{
		Bearing = ComputeBearing(geoPoint, destination);
	}

	/// <summary>
	/// Creates a GeoVector with the given coordinates and heading direction.
	/// </summary>
	/// <param name="latitude">Latitude in degrees.</param>
	/// <param name="longitude">Longitude in degrees.</param>
	/// <param name="bearing">Heading direction in degrees.</param>
	public GeoVector(T latitude, T longitude, T bearing) : base(latitude, longitude)
	{
		Bearing = degree.Normalize0To2Max(bearing);
	}

	/// <summary>
	/// Creates a GeoVector from string representations of latitude, longitude, and bearing.
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

	/// <summary>
	/// Deconstructs the GeoVector into latitude, longitude, and bearing.
	/// </summary>
	/// <param name="latitude">Latitude output.</param>
	/// <param name="longitude">Longitude output.</param>
	/// <param name="bearing">Bearing output.</param>
	public void Deconstruct(out T latitude, out T longitude, out T bearing)
	{
		latitude = Latitude;
		longitude = Longitude;
		bearing = Bearing;
	}

	/// <summary>
	/// Computes the bearing from point A to point B.
	/// </summary>
	/// <param name="A">Start point.</param>
	/// <param name="B">End point.</param>
	/// <returns>Bearing from point A to point B in degrees.</returns>
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
	/// Calculates the resulting vector after traveling a certain angular distance.
	/// </summary>
	/// <param name="angle">Angular distance to travel.</param>
	/// <returns>A new GeoVector representing the position and bearing after traveling.</returns>
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

		// Correct bearing based on angle and direction
		T bearingCorrection = (angle <= degree.StraightAngle) ^ negative ? degree.StraightAngle : T.Zero;

		// Special case: if the current point is at the poles, handle specific cases
		if (this.φ == degree.RightAngle) return new GeoVector<T>(degree.RightAngle - angle, bearingCorrection + this.λ, bearingCorrection);
		if (this.φ == -degree.RightAngle) return new GeoVector<T>(-degree.RightAngle + angle, bearingCorrection + this.λ, degree.StraightAngle + bearingCorrection);

		// Calculate new latitude and longitude based on the given angle and current bearing
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

		φ2 = T.Round(φ2, 5);
		λ2 = T.Round(λ2, 5);

		GeoPoint<T> arrival = new GeoPoint<T>(φ2, λ2);
		T newBearing = MathEx.Mod(bearingCorrection + ComputeBearing(arrival, this), degree.Perigon);

		return new GeoVector<T>(arrival, newBearing);
	}

	/// <summary>
	/// Calculates the intersection points between two great circles defined by this and another GeoVector.
	/// </summary>
	/// <param name="other">The other GeoVector representing the second great circle.</param>
	/// <returns>An array of intersection points or <c>null</c> if the great circles are identical.</returns>
	public GeoPoint<T>[] Intersections(GeoVector<T> other)
	{
		// Use temporary vectors to determine the intersections
		var temp = (a: Travel(this.AngleWith(other)), b: (-this).Travel(this.AngleWith(other)));

		// If the vectors describe the same great circle, return null (no intersections)
		if (other.In(temp.a, -temp.a, temp.b, -temp.b)) return Array.Empty<GeoPoint<T>>();

		// Handle special cases such as poles and meridian crossings
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

		T δ12 = T.CreateChecked(2) * degree.Asin(T.Sqrt(T.Pow(degree.Sin(Δφ / T.CreateChecked(2)), T.CreateChecked(2)) + degree.Cos(this.φ) * degree.Cos(other.φ) * T.Pow(degree.Sin(Δλ / T.CreateChecked(2)), T.CreateChecked(2)))); // Angular dist. p1–p2
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

	#region Equality and Overrides

	/// <inheritdoc />
	public override bool Equals(object obj)
	{
		if (ReferenceEquals(this, obj)) return true;
		if (obj is GeoVector<T> vector) return Equals(vector);
		return base.Equals(obj);
	}

	/// <inheritdoc />
	public override int GetHashCode() 
		=> Objects.ObjectUtils.ComputeHash(Latitude, Longitude, Bearing);

	/// <inheritdoc />
	public bool Equals(GeoVector<T> other) => comparer.Equals(Bearing, other.Bearing) && base.Equals(other);

	#endregion

	#region Operators

	/// <summary>
	/// Negation operator for GeoVector.
	/// </summary>
	/// <param name="geoVector">GeoVector to negate.</param>
	/// <returns>A new GeoVector with reversed bearing.</returns>
	public static GeoVector<T> operator -(GeoVector<T> geoVector) => new GeoVector<T>(geoVector.φ, geoVector.λ, degree.StraightAngle + geoVector.θ);

	/// <inheritdoc />
	public override string ToString(string format, IFormatProvider formatProvider)
	{
		formatProvider ??= CultureInfo.InvariantCulture;
		var textInfo = (TextInfo)formatProvider?.GetFormat(typeof(TextInfo));

		return $"{base.ToString(format, formatProvider)}{textInfo?.ListSeparator ?? ","} {Bearing:##0.##}";
	}

	#endregion
}
