using System;
using System.Numerics;
using Utils.Objects;

namespace Utils.Geography.Model;

/// <summary>
/// Represents an immutable map position defined by geographical coordinates and a zoom level.
/// </summary>
/// <typeparam name="T">The numeric type used for calculations, typically a floating point.</typeparam>
public class MapPosition<T> : IEquatable<MapPosition<T>>, IEqualityOperators<MapPosition<T>, MapPosition<T>, bool>
	where T : struct, IFloatingPointIeee754<T>
{
	/// <summary>
	/// Gets or sets the geographical coordinates of the map center.
	/// </summary>
	public GeoPoint<T> GeoPoint { get; set; }

	/// <summary>
	/// Gets or sets the zoom level of the map.
	/// </summary>
	public byte ZoomLevel { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="MapPosition{T}"/> class.
	/// </summary>
	/// <param name="geoPoint">The geographical coordinates of the map center.</param>
	/// <param name="zoomLevel">The zoom level of the map.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="geoPoint"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="zoomLevel"/> is less than or equal to 0.</exception>
	public MapPosition(GeoPoint<T> geoPoint, byte zoomLevel)
	{
		geoPoint.ArgMustNotBeNull();
		zoomLevel.ArgMustBeGreaterThan((byte)0);

		this.GeoPoint = geoPoint;
		this.ZoomLevel = zoomLevel;
	}

	#region Equality Members

	/// <inheritdoc />
	public override bool Equals(object obj)
		=> obj switch
		{
			MapPosition<T> other => Equals(other),
			_ => false
		};

	/// <inheritdoc />
	public bool Equals(MapPosition<T> other)
	{
		if (other is null) return false;
		if (this.GeoPoint is null) return other.GeoPoint is null;

		return this.GeoPoint.Equals(other.GeoPoint) && this.ZoomLevel == other.ZoomLevel;
	}

	/// <inheritdoc />
	public override int GetHashCode() => ObjectUtils.ComputeHash(this.GeoPoint?.GetHashCode() ?? 0, this.ZoomLevel);

	/// <summary>
	/// Equality operator for comparing two <see cref="MapPosition{T}"/> instances.
	/// </summary>
	/// <param name="left">The left-hand <see cref="MapPosition{T}"/> operand.</param>
	/// <param name="right">The right-hand <see cref="MapPosition{T}"/> operand.</param>
	/// <returns>True if the two instances are equal, otherwise false.</returns>
	public static bool operator ==(MapPosition<T> left, MapPosition<T> right) => left?.Equals(right) ?? right is null;

	/// <summary>
	/// Inequality operator for comparing two <see cref="MapPosition{T}"/> instances.
	/// </summary>
	/// <param name="left">The left-hand <see cref="MapPosition{T}"/> operand.</param>
	/// <param name="right">The right-hand <see cref="MapPosition{T}"/> operand.</param>
	/// <returns>True if the two instances are not equal, otherwise false.</returns>
	public static bool operator !=(MapPosition<T> left, MapPosition<T> right) => !(left == right);

	#endregion

	/// <inheritdoc />
	public override string ToString() => $"GeoPoint={this.GeoPoint}, ZoomLevel={this.ZoomLevel}";
}
