using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Geography;

namespace Utils.Geography.Model
{
	public class GeoPointList : List<GeoPoint>
	{
		public BoundingBox BoundingBox
		{
			get
			{
				double minLatitude = double.MaxValue, minLongitude = double.MaxValue;
				double maxLatitude = 0, maxLongitude = 0;
				foreach (GeoPoint GeoPoint in this) {
					if (GeoPoint.Latitude > maxLatitude) {
						maxLatitude = GeoPoint.Latitude;
					}
					if (GeoPoint.Longitude > maxLongitude) {
						maxLongitude = GeoPoint.Longitude;
					}
					if (GeoPoint.Latitude < minLatitude) {
						minLatitude = GeoPoint.Latitude;
					}
					if (GeoPoint.Longitude < minLongitude) {
						minLongitude = GeoPoint.Longitude;
					}
				}
				return new BoundingBox(minLatitude, minLongitude, maxLatitude, maxLongitude);
			}
		}

		public GeoPointList ()
			: base()
		{
		}

		public GeoPointList ( IEnumerable<GeoPoint> values )
			: base(values)
		{
		}

		public GeoPointList ( int capacity )
			: base(capacity)
		{

		}

	}

	public class GeoPointList2 : List<GeoPointList>
	{
		public BoundingBox BoundingBox
		{
			get
			{
				double minLatitude = double.MaxValue, minLongitude = double.MaxValue;
				double maxLatitude = 0, maxLongitude = 0;
				foreach (var GeoPointList in this) {
					if (GeoPointList.BoundingBox.MaxLatitude > maxLatitude) {
						maxLatitude = GeoPointList.BoundingBox.MaxLatitude;
					}
					if (GeoPointList.BoundingBox.MaxLongitude > maxLongitude) {
						maxLongitude = GeoPointList.BoundingBox.MaxLongitude;
					}
					if (GeoPointList.BoundingBox.MinLatitude < minLatitude) {
						minLatitude = GeoPointList.BoundingBox.MinLatitude;
					}
					if (GeoPointList.BoundingBox.MinLongitude < minLongitude) {
						minLongitude = GeoPointList.BoundingBox.MinLongitude;
					}

				}
				return new BoundingBox(minLatitude, minLongitude, maxLatitude, maxLongitude);
			}
		}

		public GeoPointList2 ()
		: base()
		{
		}

		public GeoPointList2 ( IEnumerable<GeoPointList> values )
			: base(values)
		{
		}

		public GeoPointList2 ( int capacity )
			: base(capacity)
		{

		}

		public GeoPointList2 ( IEnumerable<IEnumerable<GeoPoint>> values )
		{
			foreach (var value in values) {
				this.Add(new GeoPointList(value));
			}
		}
	}
}
