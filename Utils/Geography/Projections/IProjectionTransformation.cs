using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Geography.Model;

namespace Utils.Geography.Projections
{
	public interface IProjectionTransformation
	{

		/// <summary>
		/// transforme un point geographique en un point cartographique
		/// </summary>
		/// <param name="geopoint"></param>
		/// <param name="zoomFactor"></param>
		/// <returns></returns>
		ProjectedPoint GeopointToMappoint( GeoPoint geopoint );

		/// <summary>
		/// transforme un point cartographique en un point geographique
		/// </summary>
		/// <param name="mappoint"></param>
		/// <param name="zoomFactor"></param>
		/// <returns></returns>
		GeoPoint MappointToGeopoint(ProjectedPoint mappoint);

	}

	public abstract class ProjectionTransformation : IProjectionTransformation
	{
		public abstract ProjectedPoint GeopointToMappoint( GeoPoint geopoint );
		public abstract GeoPoint MappointToGeopoint( ProjectedPoint mappoint );

		public override bool Equals( object obj )=>	this.GetType() == obj.GetType();
		public override int GetHashCode()=>this.GetType().Name.GetHashCode();

		public static bool operator ==( ProjectionTransformation p1, ProjectionTransformation p2 )
		{
			return p1.Equals(p2);
		}

		public static bool operator !=( ProjectionTransformation p1, ProjectionTransformation p2 )
		{
			return !p1.Equals(p2);
		}
	}
}
