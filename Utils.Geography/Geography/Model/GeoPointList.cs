using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Utils.Geography;

namespace Utils.Geography.Model
{
	public class GeoPointList<T> : List<GeoPoint<T>>
		where T : struct, IFloatingPointIeee754<T>
    {
        public BoundingBox<T> BoundingBox
		{
			get
			{
				T minLatitude = (T)Convert.ChangeType(10000, typeof(T)), minLongitude = (T)Convert.ChangeType(10000, typeof(T));
                T maxLatitude = T.Zero, maxLongitude = T.Zero;
				foreach (GeoPoint<T> geoPoint in this) {
					if (geoPoint.Latitude > maxLatitude) {
						maxLatitude = geoPoint.Latitude;
					}
					if (geoPoint.Longitude > maxLongitude) {
						maxLongitude = geoPoint.Longitude;
					}
					if (geoPoint.Latitude < minLatitude) {
						minLatitude = geoPoint.Latitude;
					}
					if (geoPoint.Longitude < minLongitude) {
						minLongitude = geoPoint.Longitude;
					}
				}
				return new BoundingBox<T>(minLatitude, minLongitude, maxLatitude, maxLongitude);
			}
		}

		public GeoPointList ()
			: base()
		{
		}

		public GeoPointList ( IEnumerable<GeoPoint<T>> values )
			: base(values)
		{
		}

		public GeoPointList ( int capacity )
			: base(capacity)
		{

		}

	}

	public class GeoPointList2<T> : List<GeoPointList<T>>
		where T : struct, IFloatingPointIeee754<T>
    {
        public BoundingBox<T> BoundingBox
		{
			get
			{
                T minLatitude = (T)Convert.ChangeType(10000, typeof(T)), minLongitude = (T)Convert.ChangeType(10000, typeof(T));
                T maxLatitude = T.Zero, maxLongitude = T.Zero;
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
				return new BoundingBox<T>(minLatitude, minLongitude, maxLatitude, maxLongitude);
			}
		}

		public GeoPointList2 ()
		: base()
		{
		}

		public GeoPointList2 ( IEnumerable<GeoPointList<T>> values )
			: base(values)
		{
		}

		public GeoPointList2 ( int capacity )
			: base(capacity)
		{

		}

		public GeoPointList2 ( IEnumerable<IEnumerable<GeoPoint<T>>> values )
		{
			foreach (var value in values) {
				this.Add(new GeoPointList<T>(value));
			}
		}
	}
}
