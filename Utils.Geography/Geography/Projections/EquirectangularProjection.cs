using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Geography.Model;
using Utils.Mathematics;

namespace Utils.Geography.Projections
{
	public class EquirectangularProjection<T> : IProjectionTransformation<T>
		where T : struct, IFloatingPointIeee754<T>
	{
		public static readonly T MaxLatitude = (T)Convert.ChangeType(90, typeof(T));
		public static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

		public ProjectedPoint<T> GeoPointToMapPoint( GeoPoint<T> geopoint )
		{
			T longitude = geopoint.Longitude % degree.Perigon;
			T X = (longitude / degree.Perigon) + (T.One / (T.One + T.One));

			T latitude = geopoint.Latitude;
			if (latitude > MaxLatitude) latitude = MaxLatitude;
			else if (latitude < -MaxLatitude) latitude = -MaxLatitude;
			T Y = latitude / degree.StraightAngle + (T.One / (T.One + T.One));

			return new ProjectedPoint<T>(X, Y, this );
		}

		public GeoPoint<T> MapPointToGeoPoint( ProjectedPoint<T> mappoint )
		{
			T coordinateX = (mappoint.X) % T.One;
			T latitude = degree.Perigon * (coordinateX - (T.One / (T.One + T.One)));
			
			T longitude =  (mappoint.Y - (T.One / (T.One + T.One))) * degree.StraightAngle;
			return new GeoPoint<T>(latitude, longitude);
		}
	}
}
