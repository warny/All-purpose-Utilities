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
	[DebuggerDisplay("X={X}, Y={Y}, ZL={ZoomLevel}, TS={TileSize}")]
	public class MapPoint<T>
        where T : struct, IFloatingPointIeee754<T>
    {
        public long X { get; set; }
		public long Y { get; set; }
		public byte ZoomLevel { get; set; }
		public int TileSize { get; set; }

		public int TileX => (int)(X % TileSize);
		public int TileY => (int)(Y % TileSize);

		public Tile<T> Tile => new Tile<T>((int)X / TileSize, (int)Y / TileSize, ZoomLevel, TileSize);

		public MapPoint( ProjectedPoint<T> projectedPoint, byte zoomLevel, int tileSize )
		{
			T zoomFactor = (T)Convert.ChangeType(1 << zoomLevel, typeof(T)) ;
			this.ZoomLevel = zoomLevel;
			this.X = (long)Convert.ChangeType(T.Floor(projectedPoint.X * zoomFactor), typeof(long));
			this.Y = (long)Convert.ChangeType(T.Floor(projectedPoint.X * zoomFactor), typeof(long));
			this.TileSize = tileSize;

		}

		public MapPoint( long X, long Y, byte zoomLevel, int tileSize )
		{
			this.ZoomLevel = zoomLevel;
			this.X = X;
			this.Y = Y;
			this.TileSize = tileSize;
		}

	}
}
