using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Geography.Model;
using Utils.Geography.Projections;
using Utils.Mathematics;

namespace Utils.Geography.Display
{
	public class RepresentationConverter<T>
            where T : struct, IFloatingPointIeee754<T>
    {
        private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

        /// <summary>
        /// Planète de référence
        /// </summary>
        Planet<T> Planet { get; }

		/// <summary>
		/// Taille des tuiles cibles
		/// </summary>
		public int TileSize { get; private set; }

		/// <summary>
		/// Projection de carte
		/// </summary>
		public IProjectionTransformation<T> Projection { get; private set; }
		
		/// <summary>
		/// Créé une nouvelle projection pour la planète passée en paramètre
		/// </summary>
		/// <param name="planet">Planète</param>
		/// <param name="projection">Projection</param>
		/// <param name="tileSize">Taille de la tuile</param>
		public RepresentationConverter (Planet<T> planet, IProjectionTransformation<T> projection, int tileSize = 256 )
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
		public RepresentationConverter( IProjectionTransformation<T> projection, int tileSize = 256 )
		{
			this.Planet = Planets<T>.Earth;
			Projection = projection;
			TileSize = tileSize;
		}

		/// <summary>
		/// Converti un point géographique en point projeté sur une carte
		/// </summary>
		/// <param name="GeoPoint">Point géographique</param>
		/// <param name="zoomFactor">Facteur de grossissement</param>
		/// <returns></returns>
		public ProjectedPoint<T> GeoPointToMappoint ( GeoPoint<T> GeoPoint, byte zoomFactor )
		{
			return Projection.GeoPointToMapPoint(GeoPoint);

		}

		/// <summary>
		/// Converti un point sur une carte en point géographique
		/// </summary>
		/// <param name="projectedPoint">Point sur carte</param>
		/// <returns>point géographique</returns>
		public GeoPoint<T> MappointToGeoPoint ( ProjectedPoint<T> projectedPoint )
		{
			return Projection.MapPointToGeoPoint(projectedPoint);
		}

		/// <summary>
		/// Renvoie les coordonnées de la tuile correspondant au 
		/// </summary>
		/// <param name="projectedPoint">Point sur carte</param>
		/// <param name="zoomLevel"></param>
		/// <returns>tuile</returns>
		public Tile<T> MappointToTile ( ProjectedPoint<T> projectedPoint, byte zoomLevel )
		{
			long zoom = 1 << zoomLevel;
			return new Tile<T>(
				MathEx.Clamp((long)Convert.ChangeType(projectedPoint.X, typeof(long)) / TileSize, 0, zoom - 1),
				MathEx.Clamp((long)Convert.ChangeType(projectedPoint.Y, typeof(long)) / TileSize, 0, zoom - 1),
				zoomLevel,
				TileSize);
		}

		/// <summary>
		/// Récupère la taille totale de la carte
		/// </summary>
		/// <param name="zoomLevel">Niveau de zoom</param>
		/// <returns>Taille de la carte en pixels</returns>
		public int GetMapSize ( byte zoomLevel )
		{
			return TileSize << zoomLevel;
		}

		/// <summary>
		/// Renvoie la résolution à la latitude donnée
		/// </summary>
		/// <param name="latitude">Latitude</param>
		/// <param name="zoomLevel">Niveau de zoom</param>
		/// <returns></returns>
		public T ComputeGroundResolution ( T latitude, byte zoomLevel )
		{
			var mapSize = (T)Convert.ChangeType(GetMapSize(zoomLevel), typeof(T));
			return degree.Cos(latitude) * Planet.EquatorialCircumference / mapSize;
		}

	}
}
