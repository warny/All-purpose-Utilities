using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Geography.Model;

namespace Utils.Geography.Projections
{
	public class EquirectangularProjection : IProjectionTransformation
	{
		public const double MaxLatitude = 90;

		public ProjectedPoint GeopointToMappoint( GeoPoint geopoint )
		{
			double longitude = geopoint.Longitude % 360;
			double X = (longitude / 360) + 0.5;

			double latitude = geopoint.Latitude;
			if (latitude > MaxLatitude) latitude = MaxLatitude;
			else if (latitude < -MaxLatitude) latitude = -MaxLatitude;
			double Y = latitude / 180 + 0.5;

			return new ProjectedPoint(X, Y, this );
		}

		public GeoPoint MappointToGeopoint( ProjectedPoint mappoint )
		{
			double coordinateX = (mappoint.X) % 1;
			double latitude = 360 * (coordinateX - 0.5);
			
			double longitude =  (mappoint.Y - 0.5) * 180;
			return new GeoPoint(latitude, longitude);
		}
	}
}
