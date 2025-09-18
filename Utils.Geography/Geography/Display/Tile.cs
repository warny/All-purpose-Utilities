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

/// <summary>
/// Represents a rectangular portion of a map at a specific zoom level.
/// </summary>
/// <typeparam name="T">Floating-point type used for map projection calculations.</typeparam>
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
    /// The X index of this tile within the map grid.
    /// </summary>
    public long TileX { get; }

    /// <summary>
    /// The Y index of this tile within the map grid.
    /// </summary>
    public long TileY { get; }

    /// <summary>
    /// The zoom level of this tile.
    /// </summary>
    public byte ZoomFactor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tile{T}"/> class.
    /// </summary>
    /// <param name="tileX">The X index of the tile.</param>
    /// <param name="tileY">The Y index of the tile.</param>
    /// <param name="zoomFactor">The zoom level associated with the tile.</param>
    /// <param name="tileSize">The size of the tile in pixels.</param>
    public Tile(long tileX, long tileY, byte zoomFactor, int tileSize)
    {
        TileX = tileX;
        TileY = tileY;
        ZoomFactor = zoomFactor;
        TileSize = tileSize;
    }

    /// <summary>
    /// Gets the upper-left corner of the tile in map coordinates.
    /// </summary>
    public MapPoint<T> MapPoint1 => new(TileX * TileSize, TileY * TileSize, ZoomFactor, TileSize);

    /// <summary>
    /// Gets the lower-right corner of the tile in map coordinates.
    /// </summary>
    public MapPoint<T> MapPoint2 => new((TileX + 1) * TileSize, (TileY + 1) * TileSize, ZoomFactor, TileSize);

    /// <summary>
    /// Determines whether the specified projected point lies within this tile.
    /// </summary>
    /// <param name="mappoint">Projected point to test.</param>
    /// <returns><see langword="true"/> if the point lies within the tile; otherwise <see langword="false"/>.</returns>
    public bool Contains(ProjectedPoint<T> mappoint)
    {
        T minX = (T)Convert.ChangeType(TileX * TileSize, typeof(T));
        T maxX = (T)Convert.ChangeType((TileX + 1) * TileSize, typeof(T));
        if (mappoint.X < minX || mappoint.X > maxX)
        {
            return false;
        }

        T minY = (T)Convert.ChangeType(TileY * TileSize, typeof(T));
        T maxY = (T)Convert.ChangeType((TileY + 1) * TileSize, typeof(T));
        if (mappoint.Y < minY || mappoint.Y > maxY)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a hash code for the current tile.
    /// </summary>
    /// <returns>A hash code for the current tile.</returns>
    public override int GetHashCode() => ObjectUtils.ComputeHash(TileX, TileY, ZoomFactor, TileSize);

    /// <summary>
    /// Returns a string that represents the current tile.
    /// </summary>
    /// <returns>A string representation of the tile coordinates and zoom level.</returns>
    public override string ToString() => $"tileX={TileX}, tileY={TileY}, zoomLevel={ZoomFactor}";

    /// <summary>
    /// Returns a formatted string that represents the current tile.
    /// </summary>
    /// <param name="format">Format string to apply to the X and Y coordinates.</param>
    /// <returns>A formatted string representation of the tile.</returns>
    public string ToString(string format) =>
        $"tileX={TileX.ToString(format)}, tileY={TileY.ToString(format)}, zoomLevel={ZoomFactor}";

    /// <summary>
    /// Returns a formatted string that represents the current tile using the specified format provider.
    /// </summary>
    /// <param name="format">Format string to apply to the X and Y coordinates.</param>
    /// <param name="formatProvider">Format provider for culture-specific formatting.</param>
    /// <returns>A formatted string representation of the tile.</returns>
    public string ToString(string format, IFormatProvider formatProvider) =>
        $"tileX={TileX.ToString(format, formatProvider)}, tileY={TileY.ToString(format, formatProvider)}, zoomLevel={ZoomFactor}";

    /// <summary>
    /// Determines whether the specified object is equal to the current tile.
    /// </summary>
    /// <param name="obj">Object to compare with the current tile.</param>
    /// <returns><see langword="true"/> if the specified object is equal to the current tile; otherwise <see langword="false"/>.</returns>
    public override bool Equals(object obj) =>
        obj is Tile<T> tile && Equals(tile);

    /// <summary>
    /// Determines whether the specified tile has the same coordinates and zoom level.
    /// </summary>
    /// <param name="other">Tile to compare with the current instance.</param>
    /// <returns><see langword="true"/> if both tiles are equal; otherwise <see langword="false"/>.</returns>
    public bool Equals(Tile<T> other)
        => TileX == other.TileX
           && TileY == other.TileY
           && ZoomFactor == other.ZoomFactor
           && TileSize == other.TileSize;

    /// <summary>
    /// Determines whether two tiles are equal.
    /// </summary>
    /// <param name="tile1">First tile to compare.</param>
    /// <param name="tile2">Second tile to compare.</param>
    /// <returns><see langword="true"/> if both tiles are equal; otherwise <see langword="false"/>.</returns>
    public static bool operator ==(Tile<T> tile1, Tile<T> tile2) => tile1.Equals(tile2);

    /// <summary>
    /// Determines whether two tiles are not equal.
    /// </summary>
    /// <param name="tile1">First tile to compare.</param>
    /// <param name="tile2">Second tile to compare.</param>
    /// <returns><see langword="true"/> if the tiles differ; otherwise <see langword="false"/>.</returns>
    public static bool operator !=(Tile<T> tile1, Tile<T> tile2) => !tile1.Equals(tile2);
}