using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

namespace Utils.Geography.Model
{
    /// <summary>
    /// A list of geographic points (GeoPoint) with additional functionality, such as calculating a bounding box.
    /// </summary>
    /// <typeparam name="T">Numeric type that must implement floating-point operations.</typeparam>
    /// <remarks>
    /// Wraps a <see cref="List{T}"/> rather than inheriting from it, so that the collection storage
    /// (data) stays separate from the bounding-box computation (processing logic exposed through
    /// <see cref="IList{T}"/>/<see cref="IReadOnlyList{T}"/>). All list operations still work exactly as
    /// before, including collection-initializer syntax (<c>new GeoPointList&lt;double&gt; { p1, p2 }</c>).
    /// </remarks>
    public class GeoPointList<T> : IList<GeoPoint<T>>, IReadOnlyList<GeoPoint<T>>
        where T : struct, IFloatingPointIeee754<T>
    {
        private readonly List<GeoPoint<T>> _points;

        /// <summary>
        /// Gets the bounding box that encapsulates all GeoPoints in this list.
        /// </summary>
        public BoundingBox<T> BoundingBox
        {
            get
            {
                if (_points.Count == 0) throw new InvalidOperationException("Cannot calculate bounding box for an empty list.");

                T minLatitude = _points[0].Latitude;
                T minLongitude = _points[0].Longitude;
                T maxLatitude = _points[0].Latitude;
                T maxLongitude = _points[0].Longitude;

                foreach (GeoPoint<T> geoPoint in _points)
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
        public GeoPointList() => _points = [];

        /// <summary>
        /// Initializes a new instance of <see cref="GeoPointList{T}"/> that contains elements copied from the specified collection.
        /// </summary>
        /// <param name="values">The collection of GeoPoints to copy to this list.</param>
        public GeoPointList(IEnumerable<GeoPoint<T>> values) => _points = [.. values];

        /// <summary>
        /// Initializes a new instance of <see cref="GeoPointList{T}"/> with the specified capacity.
        /// </summary>
        /// <param name="capacity">The number of elements that the list can initially store.</param>
        public GeoPointList(int capacity) => _points = new List<GeoPoint<T>>(capacity);

        #endregion

        #region IList<GeoPoint<T>> / IReadOnlyList<GeoPoint<T>>

        /// <inheritdoc/>
        public GeoPoint<T> this[int index]
        {
            get => _points[index];
            set => _points[index] = value;
        }

        /// <inheritdoc/>
        public int Count => _points.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public void Add(GeoPoint<T> item) => _points.Add(item);

        /// <inheritdoc/>
        public void Clear() => _points.Clear();

        /// <inheritdoc/>
        public bool Contains(GeoPoint<T> item) => _points.Contains(item);

        /// <inheritdoc/>
        public void CopyTo(GeoPoint<T>[] array, int arrayIndex) => _points.CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        public IEnumerator<GeoPoint<T>> GetEnumerator() => _points.GetEnumerator();

        /// <inheritdoc/>
        public int IndexOf(GeoPoint<T> item) => _points.IndexOf(item);

        /// <inheritdoc/>
        public void Insert(int index, GeoPoint<T> item) => _points.Insert(index, item);

        /// <inheritdoc/>
        public bool Remove(GeoPoint<T> item) => _points.Remove(item);

        /// <inheritdoc/>
        public void RemoveAt(int index) => _points.RemoveAt(index);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }

    /// <summary>
    /// A list of GeoPointList, where each inner list represents a collection of geographic points.
    /// </summary>
    /// <typeparam name="T">Numeric type that must implement floating-point operations.</typeparam>
    /// <remarks>
    /// Wraps a <see cref="List{T}"/> rather than inheriting from it; see <see cref="GeoPointList{T}"/> remarks.
    /// </remarks>
    public class GeoPointList2<T> : IList<GeoPointList<T>>, IReadOnlyList<GeoPointList<T>>
        where T : struct, IFloatingPointIeee754<T>
    {
        private readonly List<GeoPointList<T>> _lists;

        /// <summary>
        /// Gets the bounding box that encapsulates all GeoPoints in all GeoPointLists contained in this list.
        /// </summary>
        public BoundingBox<T> BoundingBox
        {
            get
            {
                if (_lists.Count == 0) throw new InvalidOperationException("Cannot calculate bounding box for an empty list.");

                var firstBox = _lists[0].BoundingBox;
                T minLatitude = firstBox.MinLatitude;
                T minLongitude = firstBox.MinLongitude;
                T maxLatitude = firstBox.MaxLatitude;
                T maxLongitude = firstBox.MaxLongitude;

                foreach (var geoPointList in _lists)
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
        public GeoPointList2() => _lists = [];

        /// <summary>
        /// Initializes a new instance of <see cref="GeoPointList2{T}"/> that contains elements copied from the specified collection.
        /// </summary>
        /// <param name="values">The collection of GeoPointLists to copy to this list.</param>
        public GeoPointList2(IEnumerable<GeoPointList<T>> values) => _lists = [.. values];

        /// <summary>
        /// Initializes a new instance of <see cref="GeoPointList2{T}"/> with the specified capacity.
        /// </summary>
        /// <param name="capacity">The number of elements that the list can initially store.</param>
        public GeoPointList2(int capacity) => _lists = new List<GeoPointList<T>>(capacity);

        /// <summary>
        /// Initializes a new instance of <see cref="GeoPointList2{T}"/> from a collection of GeoPoint collections.
        /// Each inner collection becomes a <see cref="GeoPointList{T}"/>.
        /// </summary>
        /// <param name="values">The collection of GeoPoint collections to copy to this list.</param>
        public GeoPointList2(IEnumerable<IEnumerable<GeoPoint<T>>> values)
        {
            _lists = [];
            foreach (var value in values)
            {
                _lists.Add(new GeoPointList<T>(value));
            }
        }

        #endregion

        #region IList<GeoPointList<T>> / IReadOnlyList<GeoPointList<T>>

        /// <inheritdoc/>
        public GeoPointList<T> this[int index]
        {
            get => _lists[index];
            set => _lists[index] = value;
        }

        /// <inheritdoc/>
        public int Count => _lists.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public void Add(GeoPointList<T> item) => _lists.Add(item);

        /// <inheritdoc/>
        public void Clear() => _lists.Clear();

        /// <inheritdoc/>
        public bool Contains(GeoPointList<T> item) => _lists.Contains(item);

        /// <inheritdoc/>
        public void CopyTo(GeoPointList<T>[] array, int arrayIndex) => _lists.CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        public IEnumerator<GeoPointList<T>> GetEnumerator() => _lists.GetEnumerator();

        /// <inheritdoc/>
        public int IndexOf(GeoPointList<T> item) => _lists.IndexOf(item);

        /// <inheritdoc/>
        public void Insert(int index, GeoPointList<T> item) => _lists.Insert(index, item);

        /// <inheritdoc/>
        public bool Remove(GeoPointList<T> item) => _lists.Remove(item);

        /// <inheritdoc/>
        public void RemoveAt(int index) => _lists.RemoveAt(index);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
