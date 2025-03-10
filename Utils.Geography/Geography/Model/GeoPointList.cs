using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.Geography;

namespace Utils.Geography.Model
{
	/// <summary>
	/// A list of geographic points (GeoPoint) with additional functionality, such as calculating a bounding box.
	/// </summary>
	/// <typeparam name="T">Numeric type that must implement floating-point operations.</typeparam>
	public class GeoPointList<T> : List<GeoPoint<T>>
		where T : struct, IFloatingPointIeee754<T>
	{
		/// <summary>
		/// Gets the bounding box that encapsulates all GeoPoints in this list.
		/// </summary>
		public BoundingBox<T> BoundingBox
		{
			get {
				if (this.Count == 0) throw new InvalidOperationException("Cannot calculate bounding box for an empty list.");

				T minLatitude = (T)Convert.ChangeType(10000, typeof(T));
				T minLongitude = (T)Convert.ChangeType(10000, typeof(T));
				T maxLatitude = T.Zero;
				T maxLongitude = T.Zero;

				foreach (GeoPoint<T> geoPoint in this)
				{
					if (geoPoint.Latitude > maxLatitude) maxLatitude = geoPoint.Latitude;
					if (geoPoint.Longitude > maxLongitude) maxLongitude = geoPoint.Longitude;
					if (geoPoint.Latitude < minLatitude) minLatitude = geoPoint.Latitude;
					if (geoPoint.Longitude < minLongitude) minLongitude = geoPoint.Longitude;
				}

				return new BoundingBox<T>(minLatitude, minLongitude, maxLatitude, maxLongitude);
			}
		}

		#region Constructors

		/// <summary>
		/// Initializes a new empty instance of <see cref="GeoPointList{T}"/>.
		/// </summary>
		public GeoPointList() : base() { }

		/// <summary>
		/// Initializes a new instance of <see cref="GeoPointList{T}"/> that contains elements copied from the specified collection.
		/// </summary>
		/// <param name="values">The collection of GeoPoints to copy to this list.</param>
		public GeoPointList(IEnumerable<GeoPoint<T>> values) : base(values) { }

		/// <summary>
		/// Initializes a new instance of <see cref="GeoPointList{T}"/> with the specified capacity.
		/// </summary>
		/// <param name="capacity">The number of elements that the list can initially store.</param>
		public GeoPointList(int capacity) : base(capacity) { }

		#endregion
	}

	/// <summary>
	/// A list of GeoPointList, where each inner list represents a collection of geographic points.
	/// </summary>
	/// <typeparam name="T">Numeric type that must implement floating-point operations.</typeparam>
	public class GeoPointList2<T> : List<GeoPointList<T>>
		where T : struct, IFloatingPointIeee754<T>
	{
		/// <summary>
		/// Gets the bounding box that encapsulates all GeoPoints in all GeoPointLists contained in this list.
		/// </summary>
		public BoundingBox<T> BoundingBox
		{
			get {
				if (this.Count == 0) throw new InvalidOperationException("Cannot calculate bounding box for an empty list.");

				T minLatitude = (T)Convert.ChangeType(10000, typeof(T));
				T minLongitude = (T)Convert.ChangeType(10000, typeof(T));
				T maxLatitude = T.Zero;
				T maxLongitude = T.Zero;

				foreach (var geoPointList in this)
				{
					var bbox = geoPointList.BoundingBox;
					if (bbox.MaxLatitude > maxLatitude) maxLatitude = bbox.MaxLatitude;
					if (bbox.MaxLongitude > maxLongitude) maxLongitude = bbox.MaxLongitude;
					if (bbox.MinLatitude < minLatitude) minLatitude = bbox.MinLatitude;
					if (bbox.MinLongitude < minLongitude) minLongitude = bbox.MinLongitude;
				}

				return new BoundingBox<T>(minLatitude, minLongitude, maxLatitude, maxLongitude);
			}
		}

		#region Constructors

		/// <summary>
		/// Initializes a new empty instance of <see cref="GeoPointList2{T}"/>.
		/// </summary>
		public GeoPointList2() : base() { }

		/// <summary>
		/// Initializes a new instance of <see cref="GeoPointList2{T}"/> that contains elements copied from the specified collection.
		/// </summary>
		/// <param name="values">The collection of GeoPointLists to copy to this list.</param>
		public GeoPointList2(IEnumerable<GeoPointList<T>> values) : base(values) { }

		/// <summary>
		/// Initializes a new instance of <see cref="GeoPointList2{T}"/> with the specified capacity.
		/// </summary>
		/// <param name="capacity">The number of elements that the list can initially store.</param>
		public GeoPointList2(int capacity) : base(capacity) { }

		/// <summary>
		/// Initializes a new instance of <see cref="GeoPointList2{T}"/> from a collection of GeoPoint collections.
		/// Each inner collection becomes a <see cref="GeoPointList{T}"/>.
		/// </summary>
		/// <param name="values">The collection of GeoPoint collections to copy to this list.</param>
		public GeoPointList2(IEnumerable<IEnumerable<GeoPoint<T>>> values)
		{
			foreach (var value in values)
			{
				this.Add([.. value]);
			}
		}

		#endregion
	}
}
