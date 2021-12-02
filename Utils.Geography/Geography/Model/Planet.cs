using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics;
using Utils.Mathematics.LinearAlgebra;

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
		public double Distance(GeoPoint geoPoint1, GeoPoint geoPoint2) 
			=> geoPoint1.AngleWith(geoPoint2) * EquatorialRadius * MathEx.Deg2Rad;

		public GeoVector Travel(GeoVector geoVector, double distance) 
			=> geoVector.Travel(MathEx.Rad2Deg * distance / EquatorialRadius);
	}

	public static class Planets
	{
		public static Planet Mercury { get; } = new Planet(2439700);
		public static Planet Venus { get; } = new Planet(6051800);
		public static Planet Earth { get; } = new Planet(6378137);
		public static Planet EarthMoon { get; } = new Planet(1737400);
		public static Planet Mars { get; } = new Planet(3396200);
		public static Planet Jupiter { get; } = new Planet(71492000);
		public static Planet JupiterEurope { get; } = new Planet(1560800);
		public static Planet JupiterGanymede { get; } = new Planet(2634100);
		public static Planet JupiterIo { get; } = new Planet(1821600);
		public static Planet JupiterCallisto { get; } = new Planet(2410300);
		public static Planet Saturn { get; } = new Planet(60268000);
		public static Planet SaturnTitan { get; } = new Planet(2574700);
		public static Planet SaturnEncelade { get; } = new Planet(252100);
		public static Planet SaturnThetys { get; } = new Planet(531000);
		public static Planet SaturnMimas { get; } = new Planet(198200);
		public static Planet SaturnDione { get; } = new Planet(561400);
		public static Planet SaturnJapet { get; } = new Planet(734500);
		public static Planet SaturnRhea { get; } = new Planet(763800);
		public static Planet Uranus { get; } = new Planet(25559000);
		public static Planet UranusUmbriel { get; } = new Planet(584700);
		public static Planet UranusOberon { get; } = new Planet(761400);
		public static Planet UranusTitania { get; } = new Planet(788400);
		public static Planet UranusMiranda { get; } = new Planet(235800);
		public static Planet UranusAriel { get; } = new Planet(578900);
		public static Planet Neptune { get; } = new Planet(24764000);
		public static Planet NeptuneTriton { get; } = new Planet(1353400);
	}
}
