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
using Utils.Geography.Model;
using System.Diagnostics;
using System.Numerics;
using Utils.Objects;

namespace Utils.Geography.Display;


/**
 * A tile represents a rectangular part of the world map. All tiles can be identified by their X and Y number together
 * with their zoom level. The actual area that a tile covers on a map depends on the underlying map projection.
 */
[DebuggerDisplay("Size={TileSize}px, X={TileX}, Y={TileY}, ZoomFactor={ZoomFactor}")]
public class Tile<T> : IEquatable<Tile<T>>, IFormattable, IEqualityOperators<Tile<T>, Tile<T>, bool>
        where T : struct, IFloatingPointIeee754<T>
    {
        /// <summary>
        /// Width and height of a map tile in pixel.
        /// </summary>
        public int TileSize { get; }

	private const long serialVersionUID = 1L;

	/// <summary>
	/// The X number of this tile.
	/// </summary>
	public long TileX { get; }

	/// <summary>
	/// The Y number of this tile.
	/// </summary>
	public long TileY { get; }

	/// <summary>
	/// The zoom level of this tile.
	/// </summary>
	public byte ZoomFactor { get; }

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

	/// <summary>
	/// Upper left point of this tile
	/// </summary>
	public MapPoint<T> MapPoint1
	{
		get
		{
			return new MapPoint<T>(
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
	public MapPoint<T> MapPoint2
	{
		get
		{
			return new MapPoint<T>(
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
	public bool Contains ( ProjectedPoint<T> mappoint )
	{
		if (mappoint.X < (T)Convert.ChangeType(this.TileX * TileSize, typeof(T)) || mappoint.X > (T)Convert.ChangeType((this.TileX + 1) * TileSize, typeof(T))) return false;
            if (mappoint.Y < (T)Convert.ChangeType(this.TileY * TileSize, typeof(T)) || mappoint.Y > (T)Convert.ChangeType((this.TileY + 1) * TileSize, typeof(T))) return false;
            return true;
	}

	public override int GetHashCode() => ObjectUtils.ComputeHash(this.TileX, this.TileY, this.ZoomFactor);

	public override string ToString() => $"tileX={this.TileX}, tileY={this.TileY}, zoomLevel={this.ZoomFactor}";
	public string ToString(string format) => $"tileX={this.TileX.ToString(format)}, tileY={this.TileY.ToString(format)}, zoomLevel={this.ZoomFactor}";
	public string ToString(string format, IFormatProvider formatProvider) => $"tileX={this.TileX.ToString(format, formatProvider)}, tileY={this.TileY.ToString(format, formatProvider)}, zoomLevel={this.ZoomFactor}";

	public override bool Equals(object obj)
		=> obj switch {
			Tile<T> tile => Equals(tile),
			_ => false
		};
	public bool Equals(Tile<T> other)
		=> this.TileX == other.TileX
		&& this.TileY == other.TileY
		&& this.ZoomFactor == other.ZoomFactor
		&& this.TileSize == other.TileSize;

	public static bool operator ==(Tile<T> tile1, Tile<T> tile2) => tile1.Equals(tile2);
	public static bool operator !=(Tile<T> tile1, Tile<T> tile2) => !tile1.Equals(tile2);
}