﻿using System;
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
		Latitude,
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
		protected static readonly T MinutesInDegree = T.CreateChecked(60);
		protected static readonly T SecondsInDegree = T.CreateChecked(3600);
		protected static readonly T SecondsInMinute = T.CreateChecked(60);

		protected static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;
		protected static readonly FloatingPointComparer<T> comparer = new(5);

		// Latitude bounds
		public T MaxLatitude => degree.RightAngle;
		public T MinLatitude => -degree.RightAngle;

		// Regex modifier collections for positive/negative latitude/longitude
		protected static readonly IReadOnlyList<string> PositiveLatitude = ["+", "N"];
		protected static readonly IReadOnlyList<string> NegativeLatitude = ["-", "S"];
		protected static readonly IReadOnlyList<string> PositiveLongitude = ["+", "E"];
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
		/// If successful, lat/lon will be set and <c>true</c> will be returned.
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
		public override bool Equals(object obj) =>
			obj is GeoPoint<T> other && Equals(other);

		/// <inheritdoc/>
		public bool Equals(GeoPoint<T> other)
		{
			if (comparer.Equals(Latitude, MaxLatitude) && comparer.Equals(other.Latitude, MaxLatitude)) return true;
			if (comparer.Equals(Latitude, MinLatitude) && comparer.Equals(other.Latitude, MinLatitude)) return true;

			return comparer.Equals(Latitude, other.Latitude)
				&& comparer.Equals(Longitude, other.Longitude);
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return ObjectUtils.ComputeHash(Latitude, Longitude);
		}

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
		public virtual string ToString(string format, IFormatProvider formatProvider)
		{
			formatProvider ??= CultureInfo.InvariantCulture;
			var textInfo = (TextInfo)formatProvider.GetFormat(typeof(TextInfo));
			var separator = textInfo?.ListSeparator ?? ",";

			return $"{FormatPosition(Latitude, "N", "S", format, formatProvider)}{separator} " +
				   $"{FormatPosition(Longitude, "E", "W", format, formatProvider)}";
		}

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
			// Build a pattern that accommodates the culture's digits and decimal separator
			// This pattern picks up optional modifiers (W/E/N/S/+/-), degrees, optional minutes, optional seconds.
			string digits = $"[{string.Join("", culture.NumberFormat.NativeDigits)}]+";
			string decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
			string number = $"{digits}({Regex.Escape(decimalSeparator)}{digits})?";

			return new Regex(
				@$"(?<modifier>W|E|N|S|\-|\+)?(?<degrees>{number})(°(?<minutes>{number}))?('(?<seconds>{number}))?",
				RegexOptions.Compiled | RegexOptions.IgnoreCase
			);
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
