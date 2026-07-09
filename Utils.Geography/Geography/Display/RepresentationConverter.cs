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
        /// <remarks>
        /// Uses <see cref="IProjectionTransformation{T}.Normalize"/> to convert the projected point into
        /// <c>[0,1]</c> map-fraction coordinates before scaling by <see cref="GetMapSize"/>, matching
        /// <see cref="MapPoint{T}"/>'s own pixel calculation exactly, so both agree on which tile a given
        /// point falls into. The resulting pixel coordinates are clamped to <c>[0, mapSize - 1]</c> before
        /// dividing by <see cref="TileSize"/>: a normalized value of exactly <c>1</c> (e.g. Equirectangular's
        /// latitude <c>90°</c>, or Mercator's own <see cref="MercatorProjection{T}.MaxLatitude"/>) would
        /// otherwise floor to <c>mapSize</c>, one tile past the last valid one — the same edge case
        /// <see cref="MapPoint{T}"/> guards against.
        /// </remarks>
        public Tile<T> MappointToTile(ProjectedPoint<T> projectedPoint, byte zoomLevel)
        {
            long zoom = 1L << zoomLevel;

            var (nx, ny) = projectedPoint.Projection.Normalize(projectedPoint);
            long mapSize = GetMapSize(zoomLevel);
            T mapSizeT = T.CreateChecked(mapSize);
            T maxPixel = T.CreateChecked(mapSize - 1);

            long pixelX = long.CreateChecked(MathEx.Clamp(T.Floor(nx * mapSizeT), T.Zero, maxPixel));
            long pixelY = long.CreateChecked(MathEx.Clamp(T.Floor(ny * mapSizeT), T.Zero, maxPixel));

            return new Tile<T>(
                MathEx.Clamp(pixelX / TileSize, 0, zoom - 1),
                MathEx.Clamp(pixelY / TileSize, 0, zoom - 1),
                zoomLevel,
                TileSize);
        }

        /// <summary>
        /// Gets the total map size (in pixels) for the specified zoom level.
        /// </summary>
        /// <param name="zoomLevel">Zoom level of the tile grid.</param>
        /// <returns>Total map size in pixels.</returns>
        /// <remarks>
        /// Returns <see langword="long"/> (rather than <see langword="int"/>) because <c>TileSize &lt;&lt; zoomLevel</c>
        /// overflows a 32-bit <see langword="int"/> at realistic zoom levels — e.g. with the default 256px
        /// tile size, zoom 24 already overflows.
        /// </remarks>
        public long GetMapSize(byte zoomLevel)
        {
            return (long)TileSize << zoomLevel;
        }

        /// <summary>
        /// Computes the ground resolution at the specified latitude and zoom level.
        /// </summary>
        /// <param name="latitude">Latitude in degrees.</param>
        /// <param name="zoomLevel">Zoom level of the tile grid.</param>
        /// <returns>Ground resolution (meters per pixel) at the specified latitude.</returns>
        public T ComputeGroundResolution(T latitude, byte zoomLevel)
        {
            var mapSize = T.CreateChecked(GetMapSize(zoomLevel));
            return degree.Cos(latitude) * Planet.EquatorialCircumference / mapSize;
        }
    }
}
