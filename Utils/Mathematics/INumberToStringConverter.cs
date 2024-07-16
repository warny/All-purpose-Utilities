using System.Collections.Generic;
using System.Numerics;

namespace Utils.Mathematics
{
	public interface INumberToStringConverter
	{
		string Convert(BigInteger number);
		string Convert(int number);
		string Convert(long number);
	}
}