using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Geography.Model;
using Utils.Mathematics;

namespace Utils.Geography.Projections;

public class GallsPetersProjection<T> : IProjectionTransformation<T>
	where T : struct, IFloatingPointIeee754<T>
{
    private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;
    public static readonly T MaxLatitude = degree.StraightAngle;

    public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geopoint)
    {
        T longitude = geopoint.Longitude % degree.Perigon;
        T X = (longitude / degree.Perigon) + (T.One / (T.One + T.One));

        T latitude = geopoint.Latitude;
        if (latitude > MaxLatitude) latitude = MaxLatitude;
        else if (latitude < -MaxLatitude) latitude = -MaxLatitude;

        T Y = degree.Sin(latitude);

        return new ProjectedPoint<T>(X, Y, this);
    }

    public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mappoint)
    {
        T coordinateX = mappoint.X % T.One;
        T latitude = degree.Perigon * (coordinateX - (T.One / (T.One + T.One)));

        T y = (T.One / (T.One + T.One)) - mappoint.Y;
        T longitude = degree.Asin(y);

        return new GeoPoint<T>(latitude, longitude);
    }
}
