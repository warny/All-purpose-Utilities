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
using System.Runtime.Serialization;
using System.Text;
using Utils.Geography.Display;

namespace Utils.Geography.Model
{

	/**
	 * A BoundingBox represents an immutable set of two latitude and two longitude coordinates.
	 */
	public class BoundingBox: IFormattable {
		private const long serialVersionUID = 1L;

		/**
		 * Creates a new BoundingBox from a comma-separated string of coordinates in the order minLat, minLon, maxLat,
		 * maxLon. All coordinate values must be in degrees.
		 * 
		 * @param boundingBoxstring
		 *            the string that describes the BoundingBox.
		 * @return a new BoundingBox with the given coordinates.
		 * @throws IllegalArgumentException
		 *             if the string cannot be parsed or describes an invalid BoundingBox.
		 */
		public static BoundingBox FromString ( string boundingBoxstring )
		{
			double[] coordinates = CoordinatesUtil.ParseCoordinatestring(boundingBoxstring, 4);
			return new BoundingBox(coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
		}

		public GeoPoint point1 { get; private set; }
		public GeoPoint point2 { get; private set; }


		/**
		 * The maximum latitude coordinate of this BoundingBox in degrees.
		 */
		public double MaxLatitude
		{
			get { return point2.Latitude; }
			set
			{
				if (value < point1.Latitude) {
					point2.Latitude = point1.Latitude;
					point1.Latitude = value;
				} else {
					point2.Latitude = value;
				}
			}
		}

		/**
		 * The maximum longitude coordinate of this BoundingBox in degrees.
		 */
		public double MaxLongitude
		{
			get { return point2.Longitude; }
			set
			{
				if (value < point1.Longitude) {
					point2.Longitude = point1.Longitude;
					point1.Longitude = value;
				} else {
					point2.Longitude = value;
				}
			}
		}


		/**
		 * The minimum latitude coordinate of this BoundingBox in degrees.
		 */
		public double MinLatitude
		{
			get { return point1.Latitude; }
			set
			{
				if (value > point2.Latitude) {
					point2.Latitude = point1.Latitude;
					point1.Latitude = value;
				} else {
					point1.Latitude = value;
				}
			}
		}

		/**
		 * The minimum longitude coordinate of this BoundingBox in degrees.
		 */
		public double MinLongitude
		{
			get { return point1.Longitude; }
			set
			{
				if (value > point2.Longitude) {
					point2.Longitude = point1.Longitude;
					point1.Longitude = value;
				} else {
					point1.Longitude = value;
				}
			}
		}

		public BoundingBox ( string minLatitude, string minLongitude, string maxLatitude, string maxLongitude )
		{
			this.point1 = new GeoPoint(minLatitude, minLongitude);
			this.point2 = new GeoPoint(maxLatitude, maxLongitude);
			ValidateBoundingBox();
		}


		/**
		 * @param minLatitude
		 *            the minimum latitude coordinate in degrees.
		 * @param minLongitude
		 *            the minimum longitude coordinate in degrees.
		 * @param maxLatitude
		 *            the maximum latitude coordinate in degrees.
		 * @param maxLongitude
		 *            the maximum longitude coordinate in degrees.
		 * @throws IllegalArgumentException
		 *             if a coordinate is invalid.
		 */
		public BoundingBox ( double minLatitude, double minLongitude, double maxLatitude, double maxLongitude )
		{
			this.point1 = new GeoPoint(minLatitude,minLongitude);
			this.point2 = new GeoPoint(maxLatitude,maxLongitude);
			ValidateBoundingBox();
		}

		private void ValidateBoundingBox ()
		{
			if (point1.Latitude > point2.Latitude) {
				double temp = point1.Latitude;
				point1.Latitude = point2.Latitude;
				point2.Latitude = temp;
			}
			if (point1.Longitude > point2.Longitude) {
				double temp = point1.Longitude;
				point1.Longitude = point2.Longitude;
				point2.Longitude = temp;
			}
		}

		/**
		 * @param GeoPoint
		 *            the GeoPoint whose coordinates should be checked.
		 * @return true if this BoundingBox contains the given GeoPoint, false otherwise.
		 */
		public bool Contains ( GeoPoint GeoPoint )
		{
			return this.MinLatitude <= GeoPoint.Latitude && this.MaxLatitude >= GeoPoint.Latitude
					&& this.MinLongitude <= GeoPoint.Longitude && this.MaxLongitude >= GeoPoint.Longitude;
		}

		public override bool Equals ( object obj )
		{
			if (this == obj) {
				return true;
			} else if (!(obj is BoundingBox)) {
				return false;
			}
			BoundingBox other = (BoundingBox)obj;
			if (this.MaxLatitude != other.MaxLatitude) {
				return false;
			} else if (this.MaxLongitude != other.MaxLongitude) {
				return false;
			} else if (this.MinLatitude != other.MinLatitude) {
				return false;
			} else if (this.MinLongitude != other.MinLongitude) {
				return false;
			}
			return true;
		}

		/**
		 * @return a new GeoPoint at the horizontal and vertical center of this BoundingBox.
		 */
		public GeoPoint getCenterpoint ()
		{
			double latitudeOffset = (this.MaxLatitude - this.MinLatitude) / 2;
			double longitudeOffset = (this.MaxLongitude - this.MinLongitude) / 2;
			return new GeoPoint(this.MinLatitude + latitudeOffset, this.MinLongitude + longitudeOffset);
		}

		/**
		 * @return the latitude span of this BoundingBox in degrees.
		 */
		public double LatitudeSpan { get { return this.MaxLatitude - this.MinLatitude; } }

		/**
		 * @return the longitude span of this BoundingBox in degrees.
		 */
		public double LongitudeSpan { get { return this.MaxLongitude - this.MinLongitude; } }

		public override int GetHashCode() => Objects.ObjectUtils.ComputeHash(this.MaxLatitude, this.MaxLongitude, this.MinLatitude, this.MinLongitude);

		/**
		 * @param boundingBox
		 *            the BoundingBox which should be checked for intersection with this BoundingBox.
		 * @return true if this BoundingBox intersects with the given BoundingBox, false otherwise.
		 */
		public bool Intersects ( BoundingBox boundingBox )
		{
			if (this == boundingBox) {
				return true;
			}

			return this.MaxLatitude >= boundingBox.MinLatitude && this.MaxLongitude >= boundingBox.MinLongitude
					&& this.MinLatitude <= boundingBox.MaxLatitude && this.MinLongitude <= boundingBox.MaxLongitude;
		}

		public override string ToString() => $"minLatitude={this.MinLatitude}, minLongitude={this.MinLongitude}, maxLatitude={this.MaxLatitude}, maxLongitude={this.MaxLongitude}";
		public string ToString(string format) => $"minLatitude={this.MinLatitude.ToString(format)}, minLongitude={this.MinLongitude.ToString(format)}, maxLatitude={this.MaxLatitude.ToString(format)}, maxLongitude={this.MaxLongitude.ToString(format)}";
		public string ToString(string format, IFormatProvider formatProvider) => $"minLatitude={this.MinLatitude.ToString(format, formatProvider)}, minLongitude={this.MinLongitude.ToString(format, formatProvider)}, maxLatitude={this.MaxLatitude.ToString(format, formatProvider)}, maxLongitude={this.MaxLongitude.ToString(format, formatProvider)}";
	}
}