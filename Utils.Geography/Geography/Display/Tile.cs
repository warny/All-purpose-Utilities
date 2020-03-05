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
using System.Linq;
using Utils.Geography.Model;

namespace Utils.Geography.Display
{

	/**
	 * A tile represents a rectangular part of the world map. All tiles can be identified by their X and Y number together
	 * with their zoom level. The actual area that a tile covers on a map depends on the underlying map projection.
	 */
	public class Tile : IEquatable<Tile>, IFormattable
	{
		/// <summary>
		/// Width and height of a map tile in pixel.
		/// </summary>
		public int TileSize { get; private set; }

		private const long serialVersionUID = 1L;

		/// <summary>
		/// The X number of this tile.
		/// </summary>
		public long TileX { get; set; }

		/// <summary>
		/// The Y number of this tile.
		/// </summary>
		public long TileY { get; set; }

		/// <summary>
		/// The zoom level of this tile.
		/// </summary>
		public byte ZoomFactor { get; set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="tileX">the X number of the tile</param>
		/// <param name="tileY">the Y number of the tile</param>
		/// <param name="zoomFactor">the zoom level of the tile</param>
		/// <param name="tileSize">The size of the tile</param>
		public Tile ( long tileX, long tileY, byte zoomFactor, int tileSize )
		{
			this.TileX = tileX;
			this.TileY = tileY;
			this.ZoomFactor = zoomFactor;
			this.TileSize = tileSize;
		}

		public override bool Equals ( object obj )
		{
			if (ReferenceEquals(this, obj)) {
				return true;
			} else if (!(obj is Tile)) {
				return false;
			}
			Tile other = (Tile)obj;
			if (this.TileX != other.TileX) {
				return false;
			} else if (this.TileY != other.TileY) {
				return false;
			} else if (this.ZoomFactor != other.ZoomFactor) {
				return false;
			} else if (this.TileSize != other.TileSize) {
				return false;
			}
			return true;
		}

		/// <summary>
		/// Upper left point of this tile
		/// </summary>
		public MapPoint MapPoint1
		{
			get
			{
				return new MapPoint(
					this.TileX * TileSize,
					this.TileY * TileSize,
					ZoomFactor,
					TileSize
				);
			}
		}

		/// <summary>
		/// Lowerright point of this tile
		/// </summary>
		public MapPoint MapPoint2
		{
			get
			{
				return new MapPoint(
					(this.TileX + 1) * TileSize,
					(this.TileY + 1) * TileSize,
					ZoomFactor,
					TileSize
				);
			}
		}

		/// <summary>
		/// Test if a point is contained
		/// </summary>
		/// <param name="mappoint">point to test</param>
		/// <returns>True if contained</returns>
		public bool Contains ( ProjectedPoint mappoint )
		{
			if (mappoint.X < this.TileX * TileSize || mappoint.X > (this.TileX + 1) * TileSize) return false;
			if (mappoint.Y < this.TileY * TileSize || mappoint.Y > (this.TileY + 1) * TileSize) return false;
			return true;
		}

		public override int GetHashCode() => Utils.Objects.ObjectUtils.ComputeHash(this.TileX, this.TileY, this.ZoomFactor);

		public override string ToString() => $"tileX={this.TileX}, tileY={this.TileY}, zoomLevel={this.ZoomFactor}";
		public string ToString(string format) => $"tileX={this.TileX.ToString(format)}, tileY={this.TileY.ToString(format)}, zoomLevel={this.ZoomFactor}";
		public string ToString(string format, IFormatProvider formatProvider) => $"tileX={this.TileX.ToString(format, formatProvider)}, tileY={this.TileY.ToString(format, formatProvider)}, zoomLevel={this.ZoomFactor}";

		public bool Equals( Tile other )
		{
			if (this.TileX != other.TileX) {
				return false;
			} else if (this.TileY != other.TileY) {
				return false;
			} else if (this.ZoomFactor != other.ZoomFactor) {
				return false;
			} else if (this.TileSize != other.TileSize) {
				return false;
			}
			return true;
		}

		public static bool operator ==( Tile tile1, Tile tile2 )
		{
			return tile1.Equals(tile2);
		}

		public static bool operator !=( Tile tile1, Tile tile2 )
		{
			return !tile1.Equals(tile2);
		}
	}
}