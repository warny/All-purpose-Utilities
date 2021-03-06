﻿using System;
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
		public static Planet Mercury { get; } = new Lazy<Planet>(() => new Planet(2439700)).Value;
		public static Planet Venus { get; } = new Lazy<Planet>(() => new Planet(6051800)).Value;
		public static Planet Earth { get; } = new Lazy<Planet>(() => new Planet(6378137)).Value;
		public static Planet EarthMoon { get; } = new Lazy<Planet>(() => new Planet(1737400)).Value;
		public static Planet Mars { get; } = new Lazy<Planet>(() => new Planet(3396200)).Value;
		public static Planet Jupiter { get; } = new Lazy<Planet>(() => new Planet(71492000)).Value;
		public static Planet JupiterEurope { get; } = new Lazy<Planet>(() => new Planet(1560800)).Value;
		public static Planet JupiterGanymede { get; } = new Lazy<Planet>(() => new Planet(2634100)).Value;
		public static Planet JupiterIo { get; } = new Lazy<Planet>(() => new Planet(1821600)).Value;
		public static Planet JupiterCallisto { get; } = new Lazy<Planet>(() => new Planet(2410300)).Value;
		public static Planet Saturn { get; } = new Lazy<Planet>(() => new Planet(60268000)).Value;
		public static Planet SaturnTitan { get; } = new Lazy<Planet>(() => new Planet(2574700)).Value;
		public static Planet SaturnEncelade { get; } = new Lazy<Planet>(() => new Planet(252100)).Value;
		public static Planet SaturnThetys { get; } = new Lazy<Planet>(() => new Planet(531000)).Value;
		public static Planet SaturnMimas { get; } = new Lazy<Planet>(() => new Planet(198200)).Value;
		public static Planet SaturnDione { get; } = new Lazy<Planet>(() => new Planet(561400)).Value;
		public static Planet SaturnJapet { get; } = new Lazy<Planet>(() => new Planet(734500)).Value;
		public static Planet SaturnRhea { get; } = new Lazy<Planet>(() => new Planet(763800)).Value;
		public static Planet Uranus { get; } = new Lazy<Planet>(() => new Planet(25559000)).Value;
		public static Planet UranusUmbriel { get; } = new Lazy<Planet>(() => new Planet(584700)).Value;
		public static Planet UranusOberon { get; } = new Lazy<Planet>(() => new Planet(761400)).Value;
		public static Planet UranusTitania { get; } = new Lazy<Planet>(() => new Planet(788400)).Value;
		public static Planet UranusMiranda { get; } = new Lazy<Planet>(() => new Planet(235800)).Value;
		public static Planet UranusAriel { get; } = new Lazy<Planet>(() => new Planet(578900)).Value;
		public static Planet Neptune { get; } = new Lazy<Planet>(() => new Planet(24764000)).Value;
		public static Planet NeptuneTriton { get; } = new Lazy<Planet>(() => new Planet(1353400)).Value;
	}
}
