using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics.InternationalSystem
{
	public class IncompatibleUnitsException : Exception
	{
		public static void TestCompatibility(PhysicalValue left, PhysicalValue right)
		{
			if (
				left.L != right.L ||
				left.M != right.M ||
				left.T != right.T ||
				left.I != right.I ||
				left.Theta != right.Theta ||
				left.N != right.N ||
				left.J != right.J ||
				left.Rad != right.Rad ||
				left.Sterad != right.Sterad
			) throw new IncompatibleUnitsException(left, right);
		}

		public IncompatibleUnitsException(PhysicalValue left, PhysicalValue right) : base($"Les unités {left.GetUnitString()} et {right.GetUnitString()} sont incompatibles pour cette opération")
		{
			
		}
	}
}
