/*
 * Copyright 2010, 2011, 2012 mapsforge.org
 *
 * This program is free software: you can redistribute it and/or modify it under the
 * terms of the GNU Lesser General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY
 * WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
 * PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License along with
 * this program. If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Geography.Display;
using Utils.Mathematics;

namespace Utils.Geography.Model
{
	public enum CoordinateDirectionEnum {
		Latitude,
		Longitude
	}


	/// <summary>
	/// A GeoPoint represents an immutable pair of latitude and longitude coordinates.
	/// </summary>
	public class GeoPoint : IEquatable<GeoPoint>, IFormattable
	{
		protected static readonly IAngleCalculator degree = Trigonometry.Degree;
		protected static readonly DoubleComparer comparer = new DoubleComparer(10);

		protected static string[] PositiveLatitude = new[] { "+", "N" };
		protected static string[] NegativeLatitude = new[] { "-", "S" };
		protected static string[] PositiveLongitude = new[] { "+", "E" };
		protected static string[] NegativeLongitude = new[] { "-", "W" };
		private const long serialVersionUID = 1L;

		/// <summary>
		/// The latitude coordinate of this GeoPoint in degrees.
		/// <summary>
		public double Latitude { get; set; }
		/// <summary>
		/// The latitude coordinate of this GeoPoint in degrees.
		/// <summary>
		public double φ { get => Latitude; set => Latitude = value; }
		/// <summary>
		/// The longitude coordinate of this GeoPoint in degrees
		/// </summary>
		public double Longitude { get; set; }
		/// <summary>
		/// The longitude coordinate of this GeoPoint in degrees
		/// </summary>
		public double λ { get => Longitude; set => Longitude = value; }

		protected GeoPoint() { }

		/// <summary>
		/// Creates a GeoPoint from given coordinates
		/// </summary>
		/// <param name="latitude">the latitude coordinate in degrees.</param>
		/// <param name="longitude">the longitude coordinate in degrees.</param>
		public GeoPoint(GeoPoint geoPoint)
		{
			Initialize(geoPoint.Latitude, geoPoint.Longitude);
		}

		/// <summary>
		/// Creates a GeoPoint from given coordinates
		/// </summary>
		/// <param name="latitude">the latitude coordinate in degrees.</param>
		/// <param name="longitude">the longitude coordinate in degrees.</param>
		public GeoPoint(double latitude, double longitude)
		{
			Initialize(latitude, longitude);
		}

		public GeoPoint(string coordinates, params CultureInfo[] cultureInfos)
		{
			if (cultureInfos.Length == 0) cultureInfos = new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture };

			foreach (var cultureInfo in cultureInfos)
			{
				var coordinatesStrings = coordinates.Split(new[] { cultureInfo.TextInfo.ListSeparator }, StringSplitOptions.None);
				if (coordinatesStrings.Length != 2) continue;
				Regex regExCoordinate = BuildRegexCoordinates(cultureInfo);

				if (ParseCoordinates(coordinatesStrings[0], coordinatesStrings[1], cultureInfo, regExCoordinate)) return;

			}

			throw new ArgumentException($"\"{coordinates}\" n'est pas une position valide");
		}

		/// <summary>
		/// Creates a GeoPoint from given coordinates
		/// </summary>
		/// <param name="latitudeString">Latitude</param>
		/// <param name="longitudeString">Longitude</param>
		public GeoPoint(string latitudeString, string longitudeString, params CultureInfo[] cultureInfos)
		{
			if (cultureInfos.Length == 0) cultureInfos = new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture };
			foreach (var cultureInfo in cultureInfos)
			{
				Regex regExCoordinate = BuildRegexCoordinates(cultureInfo);
				if (ParseCoordinates(latitudeString, longitudeString, cultureInfo, regExCoordinate)) return;
			}
		}

		protected bool ParseCoordinates(string latitudeString, string longitudeString, CultureInfo cultureInfo, Regex regExCoordinate)
		{
			double latitude = ParseCoordinate(CoordinateDirectionEnum.Latitude, latitudeString, PositiveLatitude, NegativeLatitude, cultureInfo, regExCoordinate);
			if (double.IsNaN(latitude)) return false;
			double longitude = ParseCoordinate(CoordinateDirectionEnum.Longitude, longitudeString, PositiveLongitude, NegativeLongitude, cultureInfo, regExCoordinate);
			if (double.IsNaN(longitude)) return false;
			Initialize(latitude, longitude);
			return true;
		}

		protected double ParseCoordinate(CoordinateDirectionEnum coordinateDirection, string coordinateValue, string[] positiveModifiers, string[] negativeModifiers, CultureInfo cultureInfo, Regex regexCoordinates)
		{
			var m = regexCoordinates.Match(coordinateValue);
			if (!m.Success) return double.NaN;

			double degrees = m.Groups["deegres"].Success ? double.Parse(m.Groups["deegres"].Value, NumberStyles.Float, cultureInfo) : 0D;
			double minutes = m.Groups["minutes"].Success ? double.Parse(m.Groups["minutes"].Value, NumberStyles.Float, cultureInfo) : 0D;
			double seconds = m.Groups["seconds"].Success ? double.Parse(m.Groups["seconds"].Value, NumberStyles.Float, cultureInfo) : 0D;

			double coordinate = degrees + minutes / 60 + seconds / 3600;

			string modifier = m.Groups["modifier"].Success ? m.Groups["modifier"].Value : positiveModifiers[0];
			if (Array.IndexOf(positiveModifiers, modifier) > -1)
			{
				//les coordonées sont positives, ne fait rien
			}
			else if (Array.IndexOf(negativeModifiers, modifier) > -1)
			{
				coordinate = -coordinate;
			}
			else
			{
				throw new ArgumentException($"Invalid modifier {modifier} for {coordinateDirection}", coordinateValue);
			}
			return coordinate;
		}

		protected void Initialize(double latitude, double longitude)
		{
			CoordinatesUtil.ValidateLatitude(latitude);
			longitude = degree.NormalizeMinToMax(longitude);

			this.Latitude = latitude;
			this.Longitude = longitude;
		}

		public override bool Equals(object obj)
		{
			if (this == obj) return true;
			if (obj is GeoPoint p) return Equals(p);
			return false;
		}
		public bool Equals(GeoPoint other) 
			=> comparer.Equals(this.Latitude, other.Latitude)
				&& comparer.Equals(this.Longitude, other.Longitude);

		public override int GetHashCode() => Objects.ObjectUtils.ComputeHash(this.Latitude, this.Longitude);

		private string FormatPosition(double position, string positiveMark, string negativeMark, string format, IFormatProvider formatProvider)
		{
			string mark = position == 0 ? ""
						: position > 0 ? positiveMark : negativeMark;
			if (format == "d" || format == "D")
			{
				var temp = Math.Abs(position);
				var degrees = Math.Floor(temp);
				temp = (temp - degrees) * 60;
				var minutes = Math.Floor(temp);
				temp = (temp - minutes) * 60;
				var seconds = Math.Floor(temp);
				if (seconds != 0 || format == "D") return $"{mark}{degrees:##0}°{minutes:00}'{seconds:00}\"";
				if (minutes != 0) return $"{mark}{degrees}°{minutes:00}'";
				return $"{mark}{degrees}°";
			}
			else
			{
				return mark + Math.Abs(position).ToString(format, formatProvider);
			}
		}

		public sealed override string ToString() => ToString("0.#####");
		public string ToString(string format) => ToString(format, null);
		public virtual string ToString(string format, IFormatProvider formatProvider)
		{
			formatProvider ??= CultureInfo.InvariantCulture;
			var textInfo = (TextInfo)formatProvider?.GetFormat(typeof(TextInfo));
			return $"{FormatPosition(Latitude, "N", "S", format, formatProvider)}{textInfo?.ListSeparator ?? ","} {FormatPosition(Longitude, "E", "W", format, formatProvider)}";
		}

		protected static Regex BuildRegexCoordinates(CultureInfo cultureInfo)
		{
			string digits = "[" + string.Join("", cultureInfo.NumberFormat.NativeDigits) + "]+";
			string number = digits + "([" + cultureInfo.NumberFormat.NumberDecimalSeparator + "]" + digits + ")?";

			Regex regExCoordinate = new Regex(@"(?<modifier>W|E|N|S|-|\+)?(?<deegres>number)(°(?<minutes>number))?('(?<seconds>number))?".Replace("number", number));
			return regExCoordinate;
		}

	}
}
