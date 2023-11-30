using System;
using System.Numerics;
using Utils.Geography.Model;
using Utils.Mathematics;

namespace Utils.Geography.Projections;

public class MercatorProjection<T> : IProjectionTransformation<T>
	where T : struct, IFloatingPointIeee754<T>
{
	private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

	public static readonly T MaxLatitude = (T)Convert.ChangeType(85.05112877980659, typeof(T));

	public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geopoint)
	{
		T longitude = degree.NormalizeMinToMax(geopoint.Longitude);
		T X = (longitude / degree.Perigon) + (T.One / (T.One + T.One));

		T latitude = geopoint.Latitude;
		if (latitude > MaxLatitude) latitude = MaxLatitude;
		else if (latitude < -MaxLatitude) latitude = -MaxLatitude;

		T sinLatitude = degree.Sin(latitude);
		T Y = ((T.One / (T.One + T.One)) - T.Log((T.One + sinLatitude) / (T.One - sinLatitude)) / (T.Pi + T.Pi + T.Pi + T.Pi));


		return new ProjectedPoint<T>(X, Y, this);
	}

	public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mappoint)
	{
		T coordinateX = (mappoint.X) % T.One;
		T latitude = degree.Perigon * (coordinateX - (T.One / (T.One + T.One)));

		T y = (T.One / (T.One + T.One)) - mappoint.Y;
		T longitude = degree.RightAngle - degree.Atan(T.Exp(-y * (T.Pi + T.Pi)));

		return new GeoPoint<T>(latitude, longitude);
	}

}