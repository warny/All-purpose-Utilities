using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Geography.Model;
using Utils.Geography.Projections;
using Utils.Mathematics;

namespace Utils.Geography.Display
{
    /// <summary>
    /// Represents a point on a map tile grid using projected coordinates and zoom metadata.
    /// </summary>
    /// <typeparam name="T">Floating-point type used for projection calculations.</typeparam>
    [DebuggerDisplay("X={X}, Y={Y}, ZL={ZoomLevel}, TS={TileSize}")]
    public class MapPoint<T>
        where T : struct, IFloatingPointIeee754<T>
    {
        /// <summary>
        /// Gets or sets the projected X coordinate in pixels.
        /// </summary>
        public long X { get; set; }

        /// <summary>
        /// Gets or sets the projected Y coordinate in pixels.
        /// </summary>
        public long Y { get; set; }

        /// <summary>
        /// Gets or sets the zoom level associated with the point.
        /// </summary>
        public byte ZoomLevel { get; set; }

        /// <summary>
        /// Gets or sets the tile size, in pixels, used for the grid.
        /// </summary>
        public int TileSize { get; set; }

        /// <summary>
        /// Gets the X coordinate within the tile, always in the range [0, <see cref="TileSize"/>).
        /// </summary>
        public int TileX => (int)(((X % TileSize) + TileSize) % TileSize);

        /// <summary>
        /// Gets the Y coordinate within the tile, always in the range [0, <see cref="TileSize"/>).
        /// </summary>
        public int TileY => (int)(((Y % TileSize) + TileSize) % TileSize);

        /// <summary>
        /// Gets the tile that contains this point.
        /// </summary>
        /// <remarks>
        /// Uses floored division (via <see cref="TileX"/>/<see cref="TileY"/>) rather than truncated
        /// integer division, so that negative coordinates resolve to the correct tile instead of
        /// rounding towards zero.
        /// </remarks>
        public Tile<T> Tile => new Tile<T>((X - TileX) / TileSize, (Y - TileY) / TileSize, ZoomLevel, TileSize);

        /// <summary>
        /// Initializes a new instance of the <see cref="MapPoint{T}"/> class from projected coordinates.
        /// </summary>
        /// <param name="projectedPoint">Projected map coordinates.</param>
        /// <param name="zoomLevel">Target zoom level.</param>
        /// <param name="tileSize">Tile size in pixels.</param>
        /// <remarks>
        /// Uses <see cref="IProjectionTransformation{T}.Normalize"/> (via <see cref="ProjectedPoint{T}.Projection"/>)
        /// to convert the projected point into <c>[0,1]</c> map-fraction coordinates before scaling by the
        /// total map size in pixels (<c>tileSize &lt;&lt; zoomLevel</c>, computed in <see langword="long"/>
        /// to avoid overflowing <see langword="int"/> at high zoom levels), matching
        /// <see cref="RepresentationConverter{T}.GetMapSize"/> and <see cref="RepresentationConverter{T}.MappointToTile"/>
        /// exactly, so both paths agree on which tile a given point falls into. The resulting pixel
        /// coordinates are clamped to <c>[0, mapSize - 1]</c>: a normalized value of exactly <c>1</c>
        /// (e.g. Equirectangular's latitude <c>90°</c>, or Mercator's own <see cref="MercatorProjection{T}.MaxLatitude"/>)
        /// would otherwise floor to <c>mapSize</c>, one pixel past the last valid one.
        /// </remarks>
        public MapPoint(ProjectedPoint<T> projectedPoint, byte zoomLevel, int tileSize)
        {
            ZoomLevel = zoomLevel;
            TileSize = tileSize;

            var (nx, ny) = projectedPoint.Projection.Normalize(projectedPoint);
            long mapSize = (long)tileSize << zoomLevel;
            T mapSizeT = T.CreateChecked(mapSize);
            T maxPixel = T.CreateChecked(mapSize - 1);

            X = long.CreateChecked(MathEx.Clamp(T.Floor(nx * mapSizeT), T.Zero, maxPixel));
            Y = long.CreateChecked(MathEx.Clamp(T.Floor(ny * mapSizeT), T.Zero, maxPixel));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MapPoint{T}"/> class from pixel coordinates.
        /// </summary>
        /// <param name="x">Projected X coordinate in pixels.</param>
        /// <param name="y">Projected Y coordinate in pixels.</param>
        /// <param name="zoomLevel">Target zoom level.</param>
        /// <param name="tileSize">Tile size in pixels.</param>
        public MapPoint(long x, long y, byte zoomLevel, int tileSize)
        {
            ZoomLevel = zoomLevel;
            X = x;
            Y = y;
            TileSize = tileSize;
        }
    }
}
