using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics;

namespace Utils.Geography.Model
{
	[DebuggerDisplay("{name} Equatorial (Radius={EquatorialRadius}, Circumference={EquatorialCircumference}) ")]
	public class Planet<T>
        where T : struct, IFloatingPointIeee754<T>
    {
		private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

        public Planet( T equatorialRadius, string name = null )
		{
			this.EquatorialRadius = equatorialRadius;
			this.EquatorialCircumference = (T.Pi + T.Pi) * equatorialRadius;
			this.Name = name;
		}

		public string Name { get; }
		public T EquatorialRadius { get; }
		public T EquatorialCircumference { get; }


		/// <summary>
		/// Calculates the amount of degrees of latitude for a given distance in meters.
		/// </summary>
		/// <param name="meters">distance in meters</param>
		/// <returns>latitude degrees</returns>
		public T LatitudeDistance( T meters )
		{
			return (meters * degree.Perigon) / ((T.Pi + T.Pi) * EquatorialRadius);
		}

 		/// <summary>
		/// Calculates the amount of degrees of longitude for a given distance in meters.
		/// </summary>
		/// <param name="meters">distance in meters</param>
		/// <param name="latitude">the latitude at which the calculation should be performed</param>
		/// <returns></returns>
		public T LongitudeDistance( T meters, T latitude )
		{
			return (meters * degree.Perigon) / ((T.Pi + T.Pi) * EquatorialRadius * degree.Cos(latitude));
		}

		/// <summary>
		/// Calculates the distance between two points
		/// </summary>
		/// <param name="geoPoint1"></param>
		/// <param name="geoPoint2"></param>
		/// <returns>Distance in meters</returns>
		public T Distance(GeoPoint<T> geoPoint1, GeoPoint<T> geoPoint2) 
			=> degree.ToRadian(geoPoint1.AngleWith(geoPoint2) * EquatorialRadius);

		public GeoVector<T> Travel(GeoVector<T> geoVector, T distance) 
			=> geoVector.Travel(degree.FromRadian(distance / EquatorialRadius));
	}

	public static class Planets<T>
        where T : struct, IFloatingPointIeee754<T>
    {
        public static Planet<T> Mercury { get; } = new Planet<T>((T)Convert.ChangeType(2439700, typeof(T)), "Mercury");
		public static Planet<T> Venus { get; } = new Planet<T>((T)Convert.ChangeType(6051800, typeof(T)), "Venus");
		public static Planet<T> Earth { get; } = new Planet<T>((T)Convert.ChangeType(6378137, typeof(T)), "Earth");
		public static Planet<T> EarthMoon { get; } = new Planet<T>((T)Convert.ChangeType(1737400, typeof(T)), "Moon");
		public static Planet<T> Mars { get; } = new Planet<T>((T)Convert.ChangeType(3396200, typeof(T)), "Mars");
		public static Planet<T> Jupiter { get; } = new Planet<T>((T)Convert.ChangeType(71492000, typeof(T)), "Jupiter");
		public static Planet<T> JupiterEurope { get; } = new Planet<T>((T)Convert.ChangeType(1560800, typeof(T)), "Europe");
		public static Planet<T> JupiterGanymede { get; } = new Planet<T>((T)Convert.ChangeType(2634100, typeof(T)), "Ganymede");
		public static Planet<T> JupiterIo { get; } = new Planet<T>((T)Convert.ChangeType(1821600, typeof(T)), "Io");
		public static Planet<T> JupiterCallisto { get; } = new Planet<T>((T)Convert.ChangeType(2410300, typeof(T)), "Callisto");
		public static Planet<T> Saturn { get; } = new Planet<T>((T)Convert.ChangeType(60268000, typeof(T)), "Saturn");
		public static Planet<T> SaturnTitan { get; } = new Planet<T>((T)Convert.ChangeType(2574700, typeof(T)), "Titan");
		public static Planet<T> SaturnEncelade { get; } = new Planet<T>((T)Convert.ChangeType(252100, typeof(T)), "Encelade");
		public static Planet<T> SaturnThetys { get; } = new Planet<T>((T)Convert.ChangeType(531000, typeof(T)), "Thetys");
		public static Planet<T> SaturnMimas { get; } = new Planet<T>((T)Convert.ChangeType(198200, typeof(T)), "Mimas");
		public static Planet<T> SaturnDione { get; } = new Planet<T>((T)Convert.ChangeType(561400, typeof(T)), "Dione");
		public static Planet<T> SaturnJapet { get; } = new Planet<T>((T)Convert.ChangeType(734500, typeof(T)), "Japet");
		public static Planet<T> SaturnRhea { get; } = new Planet<T>((T)Convert.ChangeType(763800, typeof(T)), "Rhea");
		public static Planet<T> Uranus { get; } = new Planet<T>((T)Convert.ChangeType(25559000, typeof(T)), "Uranus");
		public static Planet<T> UranusUmbriel { get; } = new Planet<T>((T)Convert.ChangeType(584700, typeof(T)), "Umbriel");
		public static Planet<T> UranusOberon { get; } = new Planet<T>((T)Convert.ChangeType(761400, typeof(T)), "Oberon");
		public static Planet<T> UranusTitania { get; } = new Planet<T>((T)Convert.ChangeType(788400, typeof(T)), "Titania");
		public static Planet<T> UranusMiranda { get; } = new Planet<T>((T)Convert.ChangeType(235800, typeof(T)), "Miranda");
		public static Planet<T> UranusAriel { get; } = new Planet<T>((T)Convert.ChangeType(578900, typeof(T)), "Ariel");
		public static Planet<T> Neptune { get; } = new Planet<T>((T)Convert.ChangeType(24764000, typeof(T)), "Neptune");
		public static Planet<T> NeptuneTriton { get; } = new Planet<T>((T)Convert.ChangeType(1353400, typeof(T)), "Triton");
	}
}
