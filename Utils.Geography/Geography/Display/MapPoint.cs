using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Geography.Model;

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
        /// Gets the X coordinate within the tile.
        /// </summary>
        public int TileX => (int)(X % TileSize);

        /// <summary>
        /// Gets the Y coordinate within the tile.
        /// </summary>
        public int TileY => (int)(Y % TileSize);

        /// <summary>
        /// Gets the tile that contains this point.
        /// </summary>
        public Tile<T> Tile => new Tile<T>(X / TileSize, Y / TileSize, ZoomLevel, TileSize);

        /// <summary>
        /// Initializes a new instance of the <see cref="MapPoint{T}"/> class from projected coordinates.
        /// </summary>
        /// <param name="projectedPoint">Projected map coordinates.</param>
        /// <param name="zoomLevel">Target zoom level.</param>
        /// <param name="tileSize">Tile size in pixels.</param>
        public MapPoint(ProjectedPoint<T> projectedPoint, byte zoomLevel, int tileSize)
        {
            T zoomFactor = (T)Convert.ChangeType(1 << zoomLevel, typeof(T));
            ZoomLevel = zoomLevel;
            X = (long)Convert.ChangeType(T.Floor(projectedPoint.X * zoomFactor), typeof(long));
            Y = (long)Convert.ChangeType(T.Floor(projectedPoint.Y * zoomFactor), typeof(long));
            TileSize = tileSize;
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
