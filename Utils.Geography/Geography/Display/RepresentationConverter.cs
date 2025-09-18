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
    /// <summary>
    /// Converts between geographic coordinates, projected map points, and tile coordinates.
    /// </summary>
    /// <typeparam name="T">Floating-point type used for projections.</typeparam>
    public class RepresentationConverter<T>
        where T : struct, IFloatingPointIeee754<T>
    {
        private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

        /// <summary>
        /// Gets the reference planet used for distance and circumference calculations.
        /// </summary>
        private Planet<T> Planet { get; }

        /// <summary>
        /// Gets the target tile size, in pixels.
        /// </summary>
        public int TileSize { get; private set; }

        /// <summary>
        /// Gets the projection transformation used to convert coordinates.
        /// </summary>
        public IProjectionTransformation<T> Projection { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RepresentationConverter{T}"/> class for a given planet.
        /// </summary>
        /// <param name="planet">Planet describing the ellipsoid characteristics.</param>
        /// <param name="projection">Projection transformation implementation.</param>
        /// <param name="tileSize">Desired tile size in pixels.</param>
        public RepresentationConverter(Planet<T> planet, IProjectionTransformation<T> projection, int tileSize = 256)
        {
            Planet = planet;
            Projection = projection;
            TileSize = tileSize;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RepresentationConverter{T}"/> class for Earth.
        /// </summary>
        /// <param name="projection">Projection transformation implementation.</param>
        /// <param name="tileSize">Desired tile size in pixels.</param>
        public RepresentationConverter(IProjectionTransformation<T> projection, int tileSize = 256)
        {
            Planet = Planets<T>.Earth;
            Projection = projection;
            TileSize = tileSize;
        }

        /// <summary>
        /// Converts a geographic point to its projected representation.
        /// </summary>
        /// <param name="geoPoint">Geographic coordinates.</param>
        /// <param name="zoomFactor">Zoom factor (ignored, maintained for backward compatibility).</param>
        /// <returns>The projected map point.</returns>
        public ProjectedPoint<T> GeoPointToMappoint(GeoPoint<T> geoPoint, byte zoomFactor)
        {
            return Projection.GeoPointToMapPoint(geoPoint);
        }

        /// <summary>
        /// Converts a projected map point back to geographic coordinates.
        /// </summary>
        /// <param name="projectedPoint">Projected map point.</param>
        /// <returns>Geographic coordinates.</returns>
        public GeoPoint<T> MappointToGeoPoint(ProjectedPoint<T> projectedPoint)
        {
            return Projection.MapPointToGeoPoint(projectedPoint);
        }

        /// <summary>
        /// Converts a projected map point to tile coordinates at the specified zoom level.
        /// </summary>
        /// <param name="projectedPoint">Projected map point.</param>
        /// <param name="zoomLevel">Zoom level of the tile grid.</param>
        /// <returns>The tile containing the projected point.</returns>
        public Tile<T> MappointToTile(ProjectedPoint<T> projectedPoint, byte zoomLevel)
        {
            long zoom = 1 << zoomLevel;
            return new Tile<T>(
                MathEx.Clamp((long)Convert.ChangeType(projectedPoint.X, typeof(long)) / TileSize, 0, zoom - 1),
                MathEx.Clamp((long)Convert.ChangeType(projectedPoint.Y, typeof(long)) / TileSize, 0, zoom - 1),
                zoomLevel,
                TileSize);
        }

        /// <summary>
        /// Gets the total map size (in pixels) for the specified zoom level.
        /// </summary>
        /// <param name="zoomLevel">Zoom level of the tile grid.</param>
        /// <returns>Total map size in pixels.</returns>
        public int GetMapSize(byte zoomLevel)
        {
            return TileSize << zoomLevel;
        }

        /// <summary>
        /// Computes the ground resolution at the specified latitude and zoom level.
        /// </summary>
        /// <param name="latitude">Latitude in degrees.</param>
        /// <param name="zoomLevel">Zoom level of the tile grid.</param>
        /// <returns>Ground resolution (meters per pixel) at the specified latitude.</returns>
        public T ComputeGroundResolution(T latitude, byte zoomLevel)
        {
            var mapSize = (T)Convert.ChangeType(GetMapSize(zoomLevel), typeof(T));
            return degree.Cos(latitude) * Planet.EquatorialCircumference / mapSize;
        }
    }
}
