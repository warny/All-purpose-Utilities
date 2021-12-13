using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Geography.Model;

namespace Utils.Geography.Display
{
	[DebuggerDisplay("X={X}, Y={Y}, ZL={ZoomLevel}, TS={TileSize}")]
	public class MapPoint
	{
		public long X { get; set; }
		public long Y { get; set; }
		public byte ZoomLevel { get; set; }
		public int TileSize { get; set; }

		public int TileX => (int)(X % TileSize);
		public int TileY => (int)(Y % TileSize);

		public Tile Tile => new Tile((int)X / TileSize, (int)Y / TileSize, ZoomLevel, TileSize);

		public MapPoint( ProjectedPoint projectedPoint, byte zoomLevel, int tileSize )
		{
			int zoomFactor = 1 << zoomLevel;
			this.ZoomLevel = zoomLevel;
			this.X = (long)(projectedPoint.X * zoomFactor);
			this.Y = (long)(projectedPoint.X * zoomFactor);
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
