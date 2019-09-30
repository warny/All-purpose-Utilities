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
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Geography.Display;

namespace Utils.Geography.Model
{

	/// <summary>
	/// A GeoPoint represents an immutable pair of latitude and longitude coordinates.
	/// </summary>
	public class GeoPoint : IComparable<GeoPoint>, IFormattable
	{
		public static Regex RegExCoordinate = new Regex(@"(?<modifier>W|E|N|S|-|\+)?(?<deegres>\d+(\.\d+)?)(°(?<minutes>\d+(\.\d+)?))?('(?<seconds>\d+(\.\d+)?))?");

		private static string[] PositiveLatitude = new[] { "+", "N" };
		private static string[] NegativeLatitude = new[] { "-", "S" };
		private static string[] PositiveLongitude = new[] { "+", "E" };
		private static string[] NegativeLongitude = new[] { "-", "W" };

		private const long serialVersionUID = 1L;


		/// <summary>
		/// Creates a new GeoPoint from a comma-separated string of coordinates in the order latitude, longitude. All coordinate values must be in degrees.
		/// </summary>
		/// <param name="GeoPointstring">the string that describes the GeoPoint</param>
		/// <returns>a new GeoPoint with the given coordinates</returns>
		/// <exception cref="ArgumentException">if the string cannot be parsed or describes an invalid GeoPoint</exception>
		public static GeoPoint Parse ( string GeoPointstring )
		{
			double[] coordinates = CoordinatesUtil.ParseCoordinatestring(GeoPointstring, 2);
			return new GeoPoint(coordinates[0], coordinates[1]);
		}

		/**
		 * The latitude coordinate of this GeoPoint in degrees.
		 */
		public double Latitude { get; set; }

		/**
		 * The longitude coordinate of this GeoPoint in degrees.
		 */
		public double Longitude { get; set; }

		/// <summary>
		/// Creates a GeoPoint from given coordinates
		/// </summary>
		/// <param name="latitude">the latitude coordinate in degrees.</param>
		/// <param name="longitude">the longitude coordinate in degrees.</param>
		public GeoPoint ( double latitude, double longitude )
		{
			Initialize(latitude, longitude);
		}

		public GeoPoint ( string coordinates )
		{
			var m = RegExCoordinate.Matches(coordinates);
			if (m.Count != 2) throw new ArgumentException("Invalid or incomplete coordinates", coordinates);
			double latitude = ParseCoordinate("latitude", m[0].Value, PositiveLatitude, NegativeLatitude);
			double longitude = ParseCoordinate("longitude", m[1].Value, PositiveLongitude, NegativeLongitude);
			Initialize(latitude, longitude);
		}

		/// <summary>
		/// Creates a GeoPoint from given coordinates
		/// </summary>
		/// <param name="latitudeString">Latitude</param>
		/// <param name="longitudeString">Longitude</param>
		public GeoPoint ( string latitudeString, string longitudeString )
		{
			double latitude = ParseCoordinate("latitude", latitudeString, PositiveLatitude, NegativeLatitude);
			double longitude = ParseCoordinate("longitude", longitudeString, PositiveLongitude, NegativeLongitude);
			Initialize(latitude, longitude);
		}

		private double ParseCoordinate ( string coordinateName, string coordinateValue, string[] positiveModifiers, string[] negativeModifiers )
		{
			var m = RegExCoordinate.Match(coordinateValue);
			if (!m.Success)
				throw new ArgumentException(string.Format("Invalid value {0}", coordinateValue), coordinateName);

			double degrees = m.Groups["deegres"].Success ? double.Parse(m.Groups["deegres"].Value, CultureInfo.InvariantCulture) : 0D;
			double minutes = m.Groups["minutes"].Success ? double.Parse(m.Groups["minutes"].Value, CultureInfo.InvariantCulture) : 0D;
			double seconds = m.Groups["seconds"].Success ? double.Parse(m.Groups["seconds"].Value, CultureInfo.InvariantCulture) : 0D;

			double coordinate = degrees + minutes / 60 + seconds / 3600;

			string modifier = m.Groups["modifier"].Success ? m.Groups["modifier"].Value : positiveModifiers[0];
			if (Array.IndexOf(positiveModifiers, modifier) > -1) {
				//les coordonées sont positives, ne fait rien
			} else if (Array.IndexOf(negativeModifiers, modifier) > -1) {
				coordinate = -coordinate;
			} else {
				throw new ArgumentException(string.Format("Invalid modifier {0}", modifier), coordinateName);
			}
			return coordinate;
		}

		private void Initialize ( double latitude, double longitude )
		{
			CoordinatesUtil.ValidateLatitude(latitude);
			CoordinatesUtil.ValidateLongitude(longitude);

			this.Latitude = latitude;
			this.Longitude = longitude;
		}

		public int CompareTo ( GeoPoint GeoPoint )
		{
			if (this.Longitude > GeoPoint.Longitude) {
				return 1;
			} else if (this.Longitude < GeoPoint.Longitude) {
				return -1;
			} else if (this.Latitude > GeoPoint.Latitude) {
				return 1;
			} else if (this.Latitude < GeoPoint.Latitude) {
				return -1;
			}
			return 0;
		}

		public override bool Equals ( object obj )
		{
			if (this == obj) {
				return true;
			} else if (!(obj is GeoPoint)) {
				return false;
			}
			GeoPoint other = (GeoPoint)obj;
			if (this.Latitude != other.Latitude) {
				return false;
			} else if (this.Longitude != other.Longitude) {
				return false;
			}
			return true;
		}

		public override int GetHashCode() => Objects.ObjectUtils.ComputeHash(this.Latitude, this.Longitude);

		public override string ToString() => $"latitude={this.Latitude}, longitude={this.Longitude}";
		public string ToString(string format) => $"latitude={this.Latitude.ToString(format)}, longitude={this.Longitude.ToString(format)}";
		public string ToString(string format, IFormatProvider formatProvider) => $"latitude={this.Latitude.ToString(format, formatProvider)}, longitude={this.Longitude.ToString(format, formatProvider)}";
	}
}
