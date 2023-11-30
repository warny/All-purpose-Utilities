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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Geography.Display;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Geography.Model
{
	public enum CoordinateDirectionEnum {
		Latitude,
		Longitude
	}


	/// <summary>
	/// A GeoPoint represents an immutable pair of latitude and longitude coordinates.
	/// </summary>
	public class GeoPoint<T> : IEquatable<GeoPoint<T>>, IFormattable,
		IEqualityOperators<GeoPoint<T>, GeoPoint<T>, bool>
		where T : struct, IFloatingPointIeee754<T>, IDivisionOperators<T, T, T>
    {
		private static readonly T MinutesInDegree = (T)Convert.ChangeType(60, typeof(T));
        private static readonly T SecondsInDegree = (T)Convert.ChangeType(3600, typeof(T));
        private static readonly T SecondsInMinute = (T)Convert.ChangeType(60, typeof(T));


        protected static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;
		protected static readonly FloatingPointComparer<T> comparer = new (5);

		public T MaxLatitude => degree.RightAngle;
        public T MinLatitude => -degree.RightAngle;

		protected static IReadOnlyList<string> PositiveLatitude = ["+", "N"];
		protected static IReadOnlyList<string> NegativeLatitude = ["-", "S"];
		protected static IReadOnlyList<string> PositiveLongitude = ["+", "E"];
		protected static IReadOnlyList<string> NegativeLongitude = ["-", "W"];
		private const long serialVersionUID = 1L;

		/// <summary>
		/// The latitude coordinate of this GeoPoint in degrees.
		/// <summary>
		public T Latitude { get; set; }
		/// <summary>
		/// The latitude coordinate of this GeoPoint in degrees.
		/// <summary>
		public T φ { get => Latitude; set => Latitude = value; }
		/// <summary>
		/// The longitude coordinate of this GeoPoint in degrees
		/// </summary>
		public T Longitude { get; set; }
		/// <summary>
		/// The longitude coordinate of this GeoPoint in degrees
		/// </summary>
		public T λ { get => Longitude; set => Longitude = value; }

		protected GeoPoint() { }

		/// <summary>
		/// Creates a GeoPoint from given coordinates
		/// </summary>
		/// <param name="latitude">the latitude coordinate in degrees.</param>
		/// <param name="longitude">the longitude coordinate in degrees.</param>
		public GeoPoint(GeoPoint<T> geoPoint)
		{
			Initialize(geoPoint.Latitude, geoPoint.Longitude);
		}

		/// <summary>
		/// Creates a GeoPoint from given coordinates
		/// </summary>
		/// <param name="latitude">the latitude coordinate in degrees.</param>
		/// <param name="longitude">the longitude coordinate in degrees.</param>
		public GeoPoint(T latitude, T longitude)
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
			T latitude = ParseCoordinate(CoordinateDirectionEnum.Latitude, latitudeString, PositiveLatitude, NegativeLatitude, cultureInfo, regExCoordinate);
			if (T.IsNaN(latitude)) return false;
			T longitude = ParseCoordinate(CoordinateDirectionEnum.Longitude, longitudeString, PositiveLongitude, NegativeLongitude, cultureInfo, regExCoordinate);
			if (T.IsNaN(longitude)) return false;
			Initialize(latitude, longitude);
			return true;
		}

		protected T ParseCoordinate(CoordinateDirectionEnum coordinateDirection, string coordinateValue, IReadOnlyList<string> positiveModifiers, IReadOnlyList<string> negativeModifiers, CultureInfo cultureInfo, Regex regexCoordinates)
		{
			var m = regexCoordinates.Match(coordinateValue);
			if (!m.Success) return T.NaN;

			T degrees = m.Groups["degrees"].Success ? T.Parse(m.Groups["degrees"].Value, NumberStyles.Float, cultureInfo) : T.Zero;
			T minutes = m.Groups["minutes"].Success ? T.Parse(m.Groups["minutes"].Value, NumberStyles.Float, cultureInfo) : T.Zero;
			T seconds = m.Groups["seconds"].Success ? T.Parse(m.Groups["seconds"].Value, NumberStyles.Float, cultureInfo) : T.Zero;

			T coordinate = degrees + minutes / MinutesInDegree + seconds / SecondsInDegree;

			string modifier = m.Groups["modifier"].Success ? m.Groups["modifier"].Value : positiveModifiers[0];
			if (positiveModifiers.Contains(modifier))
			{
				//les coordonnées sont positives, ne fait rien
			}
			else if (negativeModifiers.Contains(modifier))
			{
				coordinate = -coordinate;
			}
			else
			{
				throw new ArgumentException($"Invalid modifier {modifier} for {coordinateDirection}", coordinateValue);
			}
			return coordinate;
		}

		public void Deconstruct(out T latitude, out T longitude)
		{
			latitude = Latitude;
			longitude = Longitude;
		}

		protected void Initialize(T latitude, T longitude)
		{
			latitude.ArgMustBeANumber();
			latitude.ArgMustBeBetween(MinLatitude, MaxLatitude);

			longitude = degree.NormalizeMinToMax(longitude);

			this.Latitude = latitude;
			this.Longitude = longitude;
		}

        public T AngleWith(GeoPoint<T> other)
        {
            return degree.Acos(
                degree.Sin(this.Latitude) * degree.Sin(other.Latitude) +
                degree.Cos(this.Latitude) * degree.Cos(other.Latitude) * degree.Cos(this.Longitude - other.Longitude)
            );
        }

        public override bool Equals(object obj) =>
			obj switch
			{
				GeoPoint<T> other => Equals(other),
				_ => false
			};

		public bool Equals(GeoPoint<T> other) 
			=> (comparer.Equals(this.Latitude, other.Latitude) && comparer.Equals(this.Longitude, other.Longitude))
			|| (this.Latitude == MaxLatitude && other.Latitude == MaxLatitude)
			|| (this.Latitude == MinLatitude && other.Latitude == MinLatitude);

		public override int GetHashCode() => Objects.ObjectUtils.ComputeHash(this.Latitude, this.Longitude);

		private string FormatPosition(T position, string positiveMark, string negativeMark, string format, IFormatProvider formatProvider)
		{	 
			string mark = T.IsZero(position) ? ""
						: T.IsPositive(position) ? positiveMark : negativeMark;
			if (format == "d" || format == "D")
			{
				var temp = T.Abs(position);
				var degrees = T.Floor(temp);
				temp = (temp - degrees) * MinutesInDegree;
				var minutes = T.Floor(temp);
				temp = (temp - minutes) * SecondsInDegree;
				var seconds = T.Floor(temp);
				if (seconds != T.Zero || format == "D") return $"{mark}{degrees:##0}°{minutes:00}'{seconds:00}\"";
				if (minutes != T.Zero) return $"{mark}{degrees}°{minutes:00}'";
				return $"{mark}{degrees}°";
			}
			else
			{
				return mark + T.Abs(position).ToString(format, formatProvider);
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

			Regex regExCoordinate = new(@"(?<modifier>W|E|N|S|-|\+)?(?<degrees>number)(°(?<minutes>number))?('(?<seconds>number))?".Replace("number", number));
			return regExCoordinate;
		}

        public static bool operator ==(GeoPoint<T> left, GeoPoint<T> right) => left.Equals(right);

        public static bool operator !=(GeoPoint<T> left, GeoPoint<T> right) => !left.Equals(right);
    }
}
