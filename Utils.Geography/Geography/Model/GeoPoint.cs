using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Utils.Geography.Display;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Geography.Model;


/// <summary>
/// Enum to represent the type of geographic coordinate direction: Latitude or Longitude.
/// </summary>
public enum CoordinateDirectionEnum
{
	Latitude,
	Longitude
}

/// <summary>
/// Represents an immutable geographic point with latitude and longitude.
/// This class supports parsing, formatting, and mathematical operations between geographic points.
/// </summary>
public class GeoPoint<T> : IEquatable<GeoPoint<T>>, IFormattable
	where T : struct, IFloatingPointIeee754<T>, IDivisionOperators<T, T, T>
{
	// Constants for degrees, minutes, and seconds conversions
	private static readonly T MinutesInDegree = (T)Convert.ChangeType(60, typeof(T));
	private static readonly T SecondsInDegree = (T)Convert.ChangeType(3600, typeof(T));
	private static readonly T SecondsInMinute = (T)Convert.ChangeType(60, typeof(T));

	protected static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;
	protected static readonly FloatingPointComparer<T> comparer = new(5);

	// Latitude bounds
	public T MaxLatitude => degree.RightAngle;
	public T MinLatitude => -degree.RightAngle;

	// Regex modifier collections for positive/negative latitude/longitude
	protected static IReadOnlyList<string> PositiveLatitude = ["+", "N"];
	protected static IReadOnlyList<string> NegativeLatitude = ["-", "S"];
	protected static IReadOnlyList<string> PositiveLongitude = ["+", "E"];
	protected static IReadOnlyList<string> NegativeLongitude = ["-", "W"];

	/// <summary>
	/// Latitude in degrees.
	/// </summary>
	public T Latitude { get; set; }

	/// <summary>
	/// Longitude in degrees.
	/// </summary>
	public T Longitude { get; set; }

	/// <summary>
	/// Alias for Latitude.
	/// </summary>
	public T φ { get => Latitude; set => Latitude = value; }

	/// <summary>
	/// Alias for Longitude.
	/// </summary>
	public T λ { get => Longitude; set => Longitude = value; }

	/// <summary>
	/// Default constructor for internal use.
	/// </summary>
	protected GeoPoint() { }

	/// <summary>
	/// Copy constructor for creating a new GeoPoint from an existing one.
	/// </summary>
	public GeoPoint(GeoPoint<T> geoPoint)
	{
		Initialize(geoPoint.Latitude, geoPoint.Longitude);
	}

	/// <summary>
	/// Creates a GeoPoint from given coordinates.
	/// </summary>
	/// <param name="latitude">Latitude coordinate in degrees.</param>
	/// <param name="longitude">Longitude coordinate in degrees.</param>
	public GeoPoint(T latitude, T longitude)
	{
		Initialize(latitude, longitude);
	}

	/// <summary>
	/// Creates a GeoPoint by parsing coordinate strings in various cultures.
	/// </summary>
	/// <param name="coordinates">A string representing both latitude and longitude separated by a culture-specific separator.</param>
	/// <param name="cultureInfos">Optional cultures to use for parsing (defaults to CurrentCulture and InvariantCulture).</param>
	public GeoPoint(string coordinates, params CultureInfo[] cultureInfos)
	{
		if (cultureInfos.Length == 0)
			cultureInfos = [CultureInfo.CurrentCulture, CultureInfo.InvariantCulture];

		foreach (var cultureInfo in cultureInfos)
		{
			var parts = coordinates.Split(new[] { cultureInfo.TextInfo.ListSeparator }, StringSplitOptions.None);
			if (parts.Length != 2) continue;

			var regex = BuildRegexCoordinates(cultureInfo);
			if (ParseCoordinates(parts[0], parts[1], cultureInfo, regex)) return;
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
			cultureInfos = new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture };

		foreach (var cultureInfo in cultureInfos)
		{
			var regex = BuildRegexCoordinates(cultureInfo);
			if (ParseCoordinates(latitudeString, longitudeString, cultureInfo, regex)) return;
		}

		throw new ArgumentException("Invalid coordinates");
	}

	protected bool ParseCoordinates(string latitudeString, string longitudeString, CultureInfo cultureInfo, Regex regExCoordinate)
	{
		T latitude = ParseCoordinate(CoordinateDirectionEnum.Latitude, latitudeString, PositiveLatitude, NegativeLatitude, cultureInfo, regExCoordinate);
		if (T.IsNaN(latitude)) return false;
		T longitude = ParseCoordinate(CoordinateDirectionEnum.Longitude, longitudeString, PositiveLongitude, NegativeLongitude, cultureInfo, regExCoordinate);
		if (T.IsNaN(longitude)) return false;
		Initialize(latitude, longitude);
		return true;
	}


	/// <summary>
	/// Parses a string coordinate value based on its direction (Latitude/Longitude).
	/// </summary>
	protected T ParseCoordinate(CoordinateDirectionEnum direction, string coordinateValue, IReadOnlyList<string> positiveModifiers, IReadOnlyList<string> negativeModifiers, CultureInfo culture, Regex regex)
	{
		var match = regex.Match(coordinateValue);
		if (!match.Success) return T.NaN;

		// Parse degrees, minutes, and seconds
		T degrees = match.Groups["degrees"].Success ? T.Parse(match.Groups["degrees"].Value, NumberStyles.Float, culture) : T.Zero;
		T minutes = match.Groups["minutes"].Success ? T.Parse(match.Groups["minutes"].Value, NumberStyles.Float, culture) : T.Zero;
		T seconds = match.Groups["seconds"].Success ? T.Parse(match.Groups["seconds"].Value, NumberStyles.Float, culture) : T.Zero;

		T coordinate = degrees + minutes / MinutesInDegree + seconds / SecondsInDegree;
		string modifier = match.Groups["modifier"].Success ? match.Groups["modifier"].Value : positiveModifiers[0];

		if (negativeModifiers.Contains(modifier))
			coordinate = -coordinate;
		else if (!positiveModifiers.Contains(modifier))
			throw new ArgumentException($"Invalid modifier for {direction}", coordinateValue);

		return coordinate;
	}

	/// <summary>
	/// Helper function to initialize the latitude and longitude.
	/// </summary>
	protected void Initialize(T latitude, T longitude)
	{
		latitude.ArgMustBeANumber();
		latitude.ArgMustBeBetween(MinLatitude, MaxLatitude);

		longitude = degree.NormalizeMinToMax(longitude);
		Latitude = latitude;
		Longitude = longitude;
	}

	/// <summary>
	/// Computes the angular distance between this point and another.
	/// </summary>
	public T AngleWith(GeoPoint<T> other)
	{
		return degree.Acos(
			degree.Sin(Latitude) * degree.Sin(other.Latitude) +
			degree.Cos(Latitude) * degree.Cos(other.Latitude) * degree.Cos(Longitude - other.Longitude)
		);
	}

	#region Equality & Formatting

	public override bool Equals(object obj) =>
		obj is GeoPoint<T> other && Equals(other);

	public bool Equals(GeoPoint<T> other)
		=> (comparer.Equals(Latitude, other.Latitude) && comparer.Equals(Longitude, other.Longitude))
		   || (Latitude == MaxLatitude && other.Latitude == MaxLatitude)
		   || (Latitude == MinLatitude && other.Latitude == MinLatitude);

	public override int GetHashCode() => ObjectUtils.ComputeHash(Latitude, Longitude);

	public override string ToString() => ToString("0.#####");

	public string ToString(string format) => ToString(format, null);

	public virtual string ToString(string format, IFormatProvider formatProvider)
	{
		formatProvider ??= CultureInfo.InvariantCulture;
		var textInfo = (TextInfo)formatProvider.GetFormat(typeof(TextInfo));
		return $"{FormatPosition(Latitude, "N", "S", format, formatProvider)}{textInfo?.ListSeparator ?? ","} {FormatPosition(Longitude, "E", "W", format, formatProvider)}";
	}

	private string FormatPosition(T position, string positiveMark, string negativeMark, string format, IFormatProvider formatProvider)
	{
		string mark = T.IsZero(position) ? "" : T.IsPositive(position) ? positiveMark : negativeMark;

		if (format == "d" || format == "D")
		{
			T temp = T.Abs(position);
			var degrees = T.Floor(temp);
			temp = (temp - degrees) * MinutesInDegree;
			var minutes = T.Floor(temp);
			temp = (temp - minutes) * SecondsInDegree;
			var seconds = T.Floor(temp);

			if (seconds != T.Zero || format == "D") return $"{mark}{degrees}°{minutes:00}'{seconds:00}\"";
			if (minutes != T.Zero) return $"{mark}{degrees}°{minutes:00}'";
			return $"{mark}{degrees}°";
		}

		return mark + T.Abs(position).ToString(format, formatProvider);
	}

	#endregion

	#region Operators

	public static bool operator ==(GeoPoint<T> left, GeoPoint<T> right) => left.Equals(right);
	public static bool operator !=(GeoPoint<T> left, GeoPoint<T> right) => !left.Equals(right);

	#endregion

	#region Utility

	/// <summary>
	/// Builds the regex used to parse coordinate values from strings.
	/// </summary>
	protected static Regex BuildRegexCoordinates(CultureInfo culture)
	{
		string digits = $"[{string.Join("", culture.NumberFormat.NativeDigits)}]+";
		string number = $"{digits}([{culture.NumberFormat.NumberDecimalSeparator}]{digits})?";

		return new Regex($@"(?<modifier>W|E|N|S|-|\+)?(?<degrees>{number})(°(?<minutes>{number}))?('(?<seconds>{number}))?");
	}

	#endregion
}
