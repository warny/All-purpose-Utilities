using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Geography.Model;

namespace Utils.Geography.Projections;

public interface IProjectionTransformation<T>
	where T : struct, IFloatingPointIeee754<T>
{

	/// <summary>
	/// transforme un point geographique en un point cartographique
	/// </summary>
	/// <param name="geopoint"></param>
	/// <param name="zoomFactor"></param>
	/// <returns></returns>
	ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geopoint);

	/// <summary>
	/// transforme un point cartographique en un point geographique
	/// </summary>
	/// <param name="mappoint"></param>
	/// <param name="zoomFactor"></param>
	/// <returns></returns>
	GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mappoint);

}

public abstract class ProjectionTransformation<T> :
	IProjectionTransformation<T>,
	IEquatable<ProjectionTransformation<T>>,
	IEqualityOperators<ProjectionTransformation<T>, ProjectionTransformation<T>, bool>
	where T : struct, IFloatingPointIeee754<T>
{
	public abstract ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geopoint);
	public abstract GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mappoint);

	public override bool Equals(object obj)
		=> obj switch
		{
			ProjectionTransformation<T> other => Equals(other),
			_ => false
		};
	public override int GetHashCode() => this.GetType().Name.GetHashCode();

	public virtual bool Equals(ProjectionTransformation<T> other) => this.GetType() == other.GetType();

	public static bool operator ==(ProjectionTransformation<T> p1, ProjectionTransformation<T> p2)
	{
		return p1?.Equals(p2) ?? p2 is null;
	}

	public static bool operator !=(ProjectionTransformation<T> p1, ProjectionTransformation<T> p2)
	{
		return !(p1?.Equals(p2) ?? p2 is null);
	}
}
