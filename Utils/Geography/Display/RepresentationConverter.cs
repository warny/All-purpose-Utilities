using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Geography.Model;
using Utils.Geography.Projections;

namespace Utils.Geography.Display
{
	public class RepresentationConverter
	{

		Planet Planet { get; }

		public int TileSize { get; private set; }

		public IProjectionTransformation Projection { get; private set; }

		public RepresentationConverter (Planet planet, IProjectionTransformation projection, int tileSize = 256 )
		{
			this.Planet = planet;
			Projection = projection;
			TileSize = tileSize;
		}

		public RepresentationConverter( IProjectionTransformation projection, int tileSize = 256 )
		{
			this.Planet = Planets.Earth;
			Projection = projection;
			TileSize = tileSize;
		}

		public ProjectedPoint GeoPointToMappoint ( GeoPoint GeoPoint, byte zoomFactor )
		{
			return Projection.GeopointToMappoint(GeoPoint);

		}

		public GeoPoint MappointToGeoPoint ( ProjectedPoint point )
		{
			return Projection.MappointToGeopoint(point);
		}

		public Tile MappointToTile ( ProjectedPoint mappoint, byte zoomLevel )
		{
			long zoom = 1 << zoomLevel;
			return new Tile(
				(long)Math.Min(Math.Max(mappoint.X / TileSize, 0), zoom - 1),
				(long)Math.Min(Math.Max(mappoint.Y / TileSize, 0), zoom - 1),
				zoomLevel,
				TileSize);
		}

		public long GetMapSize ( byte zoomLevel )
		{
			if (zoomLevel < 0) {
				throw new ArgumentException("zoom level must not be negative: " + zoomLevel, "zoomLevel");
			}
			return (long)TileSize << zoomLevel;
		}

		public double ComputeGroundResolution ( double latitude, byte zoomLevel )
		{
			long mapSize = GetMapSize(zoomLevel);
			return Math.Cos(latitude * (Math.PI / 180)) * Planet.EquatorialCircumference / mapSize;
		}

	}
}
