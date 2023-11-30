using System;
using System.Numerics;
using Utils.Geography.Model;
using Utils.Mathematics;

namespace Utils.Geography.Projections
{
	public class MollweidProjection<T> : IProjectionTransformation<T>
        where T : struct, IFloatingPointIeee754<T>
    {
		private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

		public static readonly T MaxLatitude = degree.StraightAngle;

		public ProjectedPoint<T> GeoPointToMapPoint( GeoPoint<T> geoPoint )
		{
			T latitude = geoPoint.Latitude;
			if (latitude > MaxLatitude) latitude = MaxLatitude;
			else if (latitude < -MaxLatitude) latitude = -MaxLatitude;

			T longitude = geoPoint.Longitude % degree.Perigon;
			T X = (longitude / degree.Perigon) * T.Cos(latitude) + (T.One / (T.One + T.One));

			T Y = latitude / degree.StraightAngle + (T.One / (T.One + T.One));

			return new ProjectedPoint<T>(X, Y, this);
		}

		public GeoPoint<T> MapPointToGeoPoint( ProjectedPoint<T> mapPoint )
		{
			T coordinateX = (mapPoint.X) % T.One;
			T latitude = degree.Perigon * (coordinateX - (T.One/(T.One + T.One)));

			T longitude = (mapPoint.Y - (T.One / (T.One + T.One))) * degree.StraightAngle * degree.Cos(latitude);
			return new GeoPoint<T>(latitude, longitude);
		}
	}
}
