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
using System.Linq;
//import java.util.ArrayList;
//import java.util.List;
//import java.util.stringTokenizer;

namespace Utils.Geography.Display
{

	/**
	 * A utility class to convert, parse and validate geographical coordinates.
	 */
	public static class CoordinatesUtil
	{
		/**
		 * Maximum possible latitude coordinate.
		 */
		public const double LATITUDE_MAX = 90;

		/**
		 * Minimum possible latitude coordinate.
		 */
		public const double LATITUDE_MIN = -LATITUDE_MAX;

		/**
		 * Maximum possible longitude coordinate.
		 */
		public const double LONGITUDE_MAX = 180;

		/**
		 * Minimum possible longitude coordinate.
		 */
		public const double LONGITUDE_MIN = -LONGITUDE_MAX;

		/**
		 * Conversion factor from degrees to microdegrees.
		 */
		private const double CONVERSION_FACTOR = 1000000.0;

		private static readonly string[] DELIMITER = new [] { "," };

		/**
		 * Converts a coordinate from degrees to microdegrees (degrees * 10^6). No validation is performed.
		 * 
		 * @param coordinate
		 *            the coordinate in degrees.
		 * @return the coordinate in microdegrees (degrees * 10^6).
		 */
		public static int degreesToMicrodegrees ( double coordinate )
		{
			return (int)(coordinate * CONVERSION_FACTOR);
		}

		/**
		 * Converts a coordinate from microdegrees (degrees * 10^6) to degrees. No validation is performed.
		 * 
		 * @param coordinate
		 *            the coordinate in microdegrees (degrees * 10^6).
		 * @return the coordinate in degrees.
		 */
		public static double microdegreesToDegrees ( int coordinate )
		{
			return coordinate / CONVERSION_FACTOR;
		}

		/**
		 * Parses a given number of comma-separated coordinate values from the supplied string.
		 * 
		 * @param coordinatesstring
		 *            a comma-separated string of coordinate values.
		 * @param numberOfCoordinates
		 *            the expected number of coordinate values in the string.
		 * @return the coordinate values in the order they have been parsed from the string.
		 * @throws IllegalArgumentException
		 *             if the string is invalid or does not contain the given number of coordinate values.
		 */
		public static double[] parseCoordinatestring ( string coordinatesstring, int numberOfCoordinates )
		{
			string[] tokens = coordinatesstring.Split(DELIMITER, StringSplitOptions.RemoveEmptyEntries);

			if (tokens.Length != numberOfCoordinates) {
				throw new ArgumentException("invalid number of coordinate values: " + coordinatesstring, "numberOfCoordinates");
			}

			double[] coordinates = tokens.Select(t => double.Parse(t)).ToArray();

			return coordinates;
		}

		/**
		 * @param latitude
		 *            the latitude coordinate in degrees which should be validated.
		 * @throws IllegalArgumentException
		 *             if the latitude coordinate is invalid or {@link Double#NaN}.
		 */
		public static void validateLatitude ( double latitude )
		{
			if (Double.IsNaN(latitude) || latitude < LATITUDE_MIN || latitude > LATITUDE_MAX) {
				throw new ArgumentException("invalid latitude: " + latitude, nameof(latitude));
			}
		}

		/**
		 * @param longitude
		 *            the longitude coordinate in degrees which should be validated.
		 * @throws IllegalArgumentException
		 *             if the longitude coordinate is invalid or {@link Double#NaN}.
		 */
		public static void validateLongitude ( double longitude )
		{
			if (Double.IsNaN(longitude) || longitude <= LONGITUDE_MIN || longitude >= LONGITUDE_MAX) {
				throw new ArgumentException("invalid longitude: " + longitude, nameof(longitude));
			}
		}

	}
}