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
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using Utils.Objects;

namespace Utils.Geography.Model
{

	/**
	 * A MapPosition represents an immutable pair of {@link GeoPoint} and zoom level.
	 */
	public class MapPosition<T> : IEquatable<MapPosition<T>>, IEqualityOperators<MapPosition<T>, MapPosition<T>, bool>
        where T : struct, IFloatingPointIeee754<T>
    {
		/**
		 * The geographical coordinates of the map center.
		 */
		public GeoPoint<T> GeoPoint { get; set; }

		/**
		 * The zoom level of the map.
		 */
		public byte ZoomLevel { get; set; }

		/**
		 * @param GeoPoint
		 *            the geographical coordinates of the map center.
		 * @param zoomLevel
		 *            the zoom level of the map.
		 * @throws IllegalArgumentException
		 *             if {@code GeoPoint} is null or {@code zoomLevel} is negative.
		 */
		public MapPosition ( GeoPoint<T> geoPoint, byte zoomLevel )
		{
			geoPoint.ArgMustNotBeNull();
			zoomLevel.ArgMustBeGreaterThan((byte)0);
			this.GeoPoint = geoPoint;
			this.ZoomLevel = zoomLevel;
		}

		public override bool Equals(object obj)
			=> obj switch
			{
				MapPosition<T> other => Equals(other as MapPosition<T>),
				_ => false
			};
        public bool Equals(MapPosition<T> other)
        {
			if (other is null) return false;
            if (this.GeoPoint is null)
            {
				return other.GeoPoint is null;
            }

			return this.GeoPoint.Equals(other.GeoPoint) && this.ZoomLevel == other.ZoomLevel;
        }

        public override int GetHashCode() => Objects.ObjectUtils.ComputeHash(this.GeoPoint?.GetHashCode() ?? 0, this.ZoomLevel);

		public override string ToString() => $"GeoPoint={this.GeoPoint}, zoomLevel={this.ZoomLevel}";

        public static bool operator ==(MapPosition<T> left, MapPosition<T> right) => left?.Equals(right) ?? right is null;

		public static bool operator !=(MapPosition<T> left, MapPosition<T> right) => !(left == right);
    }
}
