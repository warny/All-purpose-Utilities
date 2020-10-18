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

namespace Utils.Geography.Display
{

	/**
	 * A utility class to convert, parse and validate geographical coordinates.
	 */
	public static class CoordinatesUtil
	{
		/// <summary>
		/// Maximum possible latitude coordinate
		/// </summary>
		public const double LATITUDE_MAX = 90;

		/// <summary>
		/// Minimum possible latitude coordinate
		/// </summary>
		public const double LATITUDE_MIN = -LATITUDE_MAX;

		/// <summary>
		/// Maximum possible longitude coordinate
		/// </summary>
		public const double LONGITUDE_MAX = 180;

		/// <summary>
		/// Minimum possible longitude coordinate
		/// </summary>
		public const double LONGITUDE_MIN = -LONGITUDE_MAX;

		/// <summary>
		/// Conversion factor from degrees to microdegrees
		/// </summary>
		private const double CONVERSION_FACTOR = 1000000.0;

		private static readonly string[] DELIMITER = new [] { "," };

		/// <summary>
		/// Converts a coordinate from degrees to microdegrees (degrees * 10^6). No validation is performed
		/// </summary>
		/// <param name="coordinate">the coordinate in degrees</param>
		/// <returns>the coordinate in microdegrees (degrees * 10^6)</returns>
		public static int DegreesToMicrodegrees ( double coordinate )
		{
			return (int)(coordinate * CONVERSION_FACTOR);
		}

		/// <summary>
		/// Converts a coordinate from microdegrees (degrees * 10^6) to degrees. No validation is performed
		/// </summary>
		/// <param name="coordinate">the coordinate in microdegrees (degrees * 10^6)</param>
		/// <returns>the coordinate in degrees</returns>
		public static double MicrodegreesToDegrees ( int coordinate )
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
		public static double[] ParseCoordinatestring ( string coordinatesstring, int numberOfCoordinates )
		{
			string[] tokens = coordinatesstring.Split(DELIMITER, StringSplitOptions.RemoveEmptyEntries);

			if (tokens.Length != numberOfCoordinates) {
				throw new ArgumentException("invalid number of coordinate values: " + coordinatesstring, nameof(numberOfCoordinates));
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
		public static void ValidateLatitude ( double latitude )
		{
			if (double.IsNaN(latitude) || latitude < LATITUDE_MIN || latitude > LATITUDE_MAX) {
				throw new ArgumentException("invalid latitude: " + latitude, nameof(latitude));
			}
		}
	}
}