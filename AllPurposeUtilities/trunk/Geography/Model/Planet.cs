using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Geography.Model
{
	public class Planet
	{
		public Planet( double equatorialRadius )
		{
			this.EquatorialRadius = equatorialRadius;
			this.EquatorialCircumference = 2 * Math.PI * equatorialRadius;
		}

		public double EquatorialRadius { get; }
		public double EquatorialCircumference { get; }


		/// <summary>
		/// Calculates the amount of degrees of latitude for a given distance in meters.
		/// </summary>
		/// <param name="meters">distance in meters</param>
		/// <returns>latitude degrees</returns>
		public double LatitudeDistance( int meters )
		{
			return (meters * 360) / (2 * Math.PI * EquatorialRadius);
		}

 		/// <summary>
		/// Calculates the amount of degrees of longitude for a given distance in meters.
		/// </summary>
		/// <param name="meters">distance in meters</param>
		/// <param name="latitude">the latitude at which the calculation should be performed</param>
		/// <returns></returns>
		public double LongitudeDistance( int meters, double latitude )
		{
			return (meters * 360) / (2 * Math.PI * EquatorialRadius * Math.Cos(latitude / 180 * Math.PI));
		}

		/// <summary>
		/// Calculates the distance between two points
		/// </summary>
		/// <param name="geoPoint1"></param>
		/// <param name="geoPoint2"></param>
		/// <returns>Distance in meters</returns>
		public double Distance( GeoPoint geoPoint1, GeoPoint geoPoint2 )
		{
			double Deg2Rad = Math.PI / 180;
			return Math.Acos(
					Math.Sin(geoPoint1.Latitude * Deg2Rad) * Math.Sin(geoPoint2.Latitude * Deg2Rad)
					+ Math.Cos(geoPoint1.Latitude * Deg2Rad) * Math.Cos(geoPoint2.Latitude * Deg2Rad) * Math.Cos((geoPoint1.Longitude - geoPoint2.Longitude) * Deg2Rad)
				) * EquatorialRadius;
		}
	}

	public static class Planets
	{
		private static Lazy<Planet> mercury = new Lazy<Planet>(() => new Planet(2439700));
		private static Lazy<Planet> venus = new Lazy<Planet>(() => new Planet(6051800));
		private static Lazy<Planet> earth = new Lazy<Planet>(()=>new Planet(6378137));
		private static Lazy<Planet> earthMoon = new Lazy<Planet>(() => new Planet(1737400));
		private static Lazy<Planet> mars = new Lazy<Planet>(() => new Planet(3396200));
		private static Lazy<Planet> jupiter = new Lazy<Planet>(() => new Planet(71492000));
		private static Lazy<Planet> saturn = new Lazy<Planet>(() => new Planet(60268000));
		private static Lazy<Planet> uranus = new Lazy<Planet>(() => new Planet(25559000));
		private static Lazy<Planet> neptune = new Lazy<Planet>(() => new Planet(24764000));

		public static Planet Mercury => mercury.Value;
		public static Planet Venus => venus.Value;
		public static Planet Earth => earth.Value;
		public static Planet EarthMoon => earthMoon.Value;
		public static Planet Mars => mars.Value;
		public static Planet Jupiter => jupiter.Value;
		public static Planet Saturn => saturn.Value;
		public static Planet Uranus => uranus.Value;
		public static Planet Neptune => neptune.Value;
	}
}
