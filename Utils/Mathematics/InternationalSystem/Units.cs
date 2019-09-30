using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics.InternationalSystem
{
	public class Units
	{
		public PhysicalValue Meter => PhysicalValue.Length(1);
		public PhysicalValue LightSecond => PhysicalValue.Length(299_792_458);
		public PhysicalValue Kilogram => PhysicalValue.Mass(1);
		public PhysicalValue Second => PhysicalValue.Time(1);
		public PhysicalValue Minute => PhysicalValue.Time(60);
		public PhysicalValue Hour => PhysicalValue.Time(3600);
		public PhysicalValue Ampere => PhysicalValue.Intensity(1);
		public PhysicalValue Kelvin => PhysicalValue.Temperature(1);
		public PhysicalValue Mole => PhysicalValue.SubstanceAmount(1);
		public PhysicalValue Candela => PhysicalValue.LightIntensity(1);
		public PhysicalValue Radian => PhysicalValue.Angle(1);
		public PhysicalValue Steradian => PhysicalValue.SolidAngle(1);

		public PhysicalValue Hertz => new PhysicalValue(1, t: -1);

		public PhysicalValue Gray => new PhysicalValue(1, l: 2, t: -2);

		public PhysicalValue Newton => new PhysicalValue(1, m: 1, l: 1, t: -2);
		public PhysicalValue Joule => new PhysicalValue(1, m: 2, l: 1, t: -2);
		public PhysicalValue Watt => new PhysicalValue(1, m: 1, l: 2, t: -3);
		public PhysicalValue Pascal => new PhysicalValue(1, m: 1, l: -1, t: -2);

		public PhysicalValue Coulomb => new PhysicalValue(1, t: 1, i: 1);
		public PhysicalValue Volt => new PhysicalValue(1, m: 1, l: 2, t: -3, i: -1);
		public PhysicalValue Tesla => new PhysicalValue(1, m: 1, t: -2, i: -1);
		public PhysicalValue Weber => new PhysicalValue(1, m: 1, l: 2, t: -2, i: -1);
		public PhysicalValue Siemens => new PhysicalValue(1, m: -1, l: -2, t: 3, i: 2);
		public PhysicalValue Farad => new PhysicalValue(1, m: -1, l: -2, t: 4, i: 2);

		public PhysicalValue Lumen => new PhysicalValue(1, j: 1, sterad:1);
		public PhysicalValue Lux => new PhysicalValue(1, l: -2, j:1, sterad: 1);
	}
}
