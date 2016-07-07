using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Geography.Model;

namespace Utils.Geography.Projections
{
	public class MercatorProjection : IProjectionTransformation
	{

		public const double MaxLatitude = 85.05112877980659;

		public ProjectedPoint GeopointToMappoint( GeoPoint geopoint )
		{
			double longitude = geopoint.Longitude % 360;
			double X = (longitude / 360) + 0.5;

			double latitude = geopoint.Latitude;
			if (latitude > MaxLatitude) latitude = MaxLatitude;
			else if (latitude < -MaxLatitude) latitude = -MaxLatitude;

			double sinLatitude = Math.Sin(latitude * (Math.PI / 180));
			double Y = (0.5 - Math.Log((1 + sinLatitude) / (1 - sinLatitude)) / (4 * Math.PI));


			return new ProjectedPoint(X, Y, this);
		}

		public GeoPoint MappointToGeopoint( ProjectedPoint mappoint )
		{
			double coordinateX = (mappoint.X ) % 1;
			double latitude = 360 * (coordinateX - 0.5);

			double y = 0.5 - mappoint.Y;
			double longitude = 90 - 360 * Math.Atan(Math.Exp(-y * (2 * Math.PI))) / Math.PI;

			return new GeoPoint(latitude, longitude);
		}

	}
}
