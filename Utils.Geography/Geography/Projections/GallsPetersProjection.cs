using System;
using System.Numerics;
using Utils.Geography.Model;
using Utils.Geography.Projections;
using Utils.Mathematics;

namespace Utils.Geography.Projections
{
    /// <summary>
    /// Implements the Gall–Peters cylindrical equal-area projection
    /// "centered" at (lat=0°, lon=0°) => (x=0, y=0).
    /// 
    /// Formulas (in degrees):
    ///   x = cos(45°) * longitude
    ///   y = sin(latitude) / cos(45°)
    ///
    ///   lon = x / cos(45°)
    ///   lat = asin( y * cos(45°) )
    ///
    /// Both input (GeoPoint) and output (ProjectedPoint) use degrees.
    /// </summary>
    /// <typeparam name="T">
    /// A numeric type implementing IFloatingPointIeee754 (e.g. float, double, decimal).
    /// </typeparam>
    public class GallsPetersProjection<T> : IProjectionTransformation<T>
        where T : struct, IFloatingPointIeee754<T>
    {
        // We'll use your degree-based trigonometry helper.
        private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

        // Cache cos(45°) and its reciprocal so we don't keep computing it.
        private static readonly T cos45 = degree.Cos(T.CreateChecked(45));
        private static readonly T invCos45 = T.One / cos45;

        /// <summary>
        /// Projects a geographical point (in degrees) to the Gall–Peters projection (also in degrees).
        /// (0°,0°) maps to (0,0), with standard parallels at ±45° for equal-area property.
        /// </summary>
        /// <param name="geoPoint">Latitude/Longitude in degrees.</param>
        /// <returns>A <see cref="ProjectedPoint{T}"/> where <c>X</c> = cos(45°)*lon, <c>Y</c>=sin(lat)/cos(45°).</returns>
        public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geoPoint)
        {
            // x = cos(45°)*lon
            T x = cos45 * geoPoint.Longitude;

            // y = sin(lat) / cos(45°)
            // lat, lon, sin => all in degrees
            T sLat = degree.Sin(geoPoint.Latitude);
            T y = sLat * invCos45;

            return new ProjectedPoint<T>(x, y, this);
        }

        /// <summary>
        /// Unprojects a Gall–Peters point (x,y) in "degrees" back to geographical lat/lon in degrees.
        /// </summary>
        /// <param name="mapPoint">Projected point, <c>X=cos(45°)*lon</c>, <c>Y=sin(lat)/cos(45°).</c></param>
        /// <returns><see cref="GeoPoint{T}"/> in degrees.</returns>
        public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mapPoint)
        {
            // lon = x / cos(45°)
            T lon = mapPoint.X / cos45;

            // lat = asin( y * cos(45°) )
            // because y = sin(lat)/cos(45°)
            T latSin = mapPoint.Y * cos45;
            T lat = degree.Asin(latSin);

            return new GeoPoint<T>(lat, lon);
        }
    }
}
