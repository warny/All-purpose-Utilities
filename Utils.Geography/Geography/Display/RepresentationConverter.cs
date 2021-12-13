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
		/// <summary>
		/// Planète de référence
		/// </summary>
		Planet Planet { get; }

		/// <summary>
		/// Taille des tuiles cibles
		/// </summary>
		public int TileSize { get; private set; }

		/// <summary>
		/// Projection de carte
		/// </summary>
		public IProjectionTransformation Projection { get; private set; }
		
		/// <summary>
		/// Créé une nouvelle projection pour la planète passée en paramètre
		/// </summary>
		/// <param name="planet">Planète</param>
		/// <param name="projection">Projection</param>
		/// <param name="tileSize">Taille de la tuile</param>
		public RepresentationConverter (Planet planet, IProjectionTransformation projection, int tileSize = 256 )
		{
			this.Planet = planet;
			Projection = projection;
			TileSize = tileSize;
		}

		/// <summary>
		/// Créé une nouvelle projection pour la planète terre
		/// </summary>
		/// <param name="projection">Projection</param>
		/// <param name="tileSize">Taille de la tuile</param>
		public RepresentationConverter( IProjectionTransformation projection, int tileSize = 256 )
		{
			this.Planet = Planets.Earth;
			Projection = projection;
			TileSize = tileSize;
		}

		/// <summary>
		/// Converti un point géographique en point projeté sur une carte
		/// </summary>
		/// <param name="GeoPoint">Point géographique</param>
		/// <param name="zoomFactor">Facteur de grossissement</param>
		/// <returns></returns>
		public ProjectedPoint GeoPointToMappoint ( GeoPoint GeoPoint, byte zoomFactor )
		{
			return Projection.GeopointToMappoint(GeoPoint);

		}

		/// <summary>
		/// Converti un point sur une carte en point géographique
		/// </summary>
		/// <param name="projectedPoint">Point sur carte</param>
		/// <returns>point géographique</returns>
		public GeoPoint MappointToGeoPoint ( ProjectedPoint projectedPoint )
		{
			return Projection.MappointToGeopoint(projectedPoint);
		}

		/// <summary>
		/// Renvoie les coordonnées de la tuile correspondant au 
		/// </summary>
		/// <param name="projectedPoint">Point sur carte</param>
		/// <param name="zoomLevel"></param>
		/// <returns>tuile</returns>
		public Tile MappointToTile ( ProjectedPoint projectedPoint, byte zoomLevel )
		{
			long zoom = 1 << zoomLevel;
			return new Tile(
				(long)Math.Min(Math.Max(projectedPoint.X / TileSize, 0), zoom - 1),
				(long)Math.Min(Math.Max(projectedPoint.Y / TileSize, 0), zoom - 1),
				zoomLevel,
				TileSize);
		}

		/// <summary>
		/// Récupère la taille totale de la carte
		/// </summary>
		/// <param name="zoomLevel">Niveau de zoom</param>
		/// <returns>Taille de la carte en pixels</returns>
		public long GetMapSize ( byte zoomLevel )
		{
			return (long)TileSize << zoomLevel;
		}

		/// <summary>
		/// Renvoie la résolution à la latitude donnée
		/// </summary>
		/// <param name="latitude">Latitude</param>
		/// <param name="zoomLevel">Niveau de zoom</param>
		/// <returns></returns>
		public double ComputeGroundResolution ( double latitude, byte zoomLevel )
		{
			long mapSize = GetMapSize(zoomLevel);
			return Math.Cos(latitude * (Math.PI / 180)) * Planet.EquatorialCircumference / mapSize;
		}

	}
}
