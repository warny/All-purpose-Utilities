using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics.InternationalSystem
{
	public static class Units
	{
		public static PhysicalValue Meter => PhysicalValue.Length(1);
		public static PhysicalValue LightSecond => PhysicalValue.Length(299_792_458);
		public static PhysicalValue Kilogram => PhysicalValue.Mass(1);
		public static PhysicalValue Second => PhysicalValue.Time(1);
		public static PhysicalValue Minute => PhysicalValue.Time(60);
		public static PhysicalValue Hour => PhysicalValue.Time(3600);
		public static PhysicalValue Ampere => PhysicalValue.Intensity(1);
		public static PhysicalValue Kelvin => PhysicalValue.Temperature(1);
		public static PhysicalValue Mole => PhysicalValue.SubstanceAmount(1);
		public static PhysicalValue Candela => PhysicalValue.LightIntensity(1);
		public static PhysicalValue Radian => PhysicalValue.Angle(1);
		public static PhysicalValue Steradian => PhysicalValue.SolidAngle(1);

		public static PhysicalValue Hertz => new (1, t: -1);

		public static PhysicalValue Gray => new (1, l: 2, t: -2);

		public static PhysicalValue Newton => new (1, m: 1, l: 1, t: -2);
		public static PhysicalValue Joule => new (1, m: 2, l: 1, t: -2);
		public static PhysicalValue Watt => new (1, m: 1, l: 2, t: -3);
		public static PhysicalValue Pascal => new (1, m: 1, l: -1, t: -2);

		public static PhysicalValue Coulomb => new (1, t: 1, i: 1);
		public static PhysicalValue Volt => new (1, m: 1, l: 2, t: -3, i: -1);
		public static PhysicalValue Tesla => new (1, m: 1, t: -2, i: -1);
		public static PhysicalValue Weber => new (1, m: 1, l: 2, t: -2, i: -1);
		public static PhysicalValue Siemens => new (1, m: -1, l: -2, t: 3, i: 2);
		public static PhysicalValue Farad => new (1, m: -1, l: -2, t: 4, i: 2);

		public static PhysicalValue Lumen => new (1, j: 1, sterad:1);
		public static PhysicalValue Lux => new (1, l: -2, j:1, sterad: 1);
	}
}
