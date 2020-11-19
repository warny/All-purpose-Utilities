using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Objects
{
	public class RepartitedRandom
	{
		private Random r;
		Func<double, double> repartitionFunction;

		double min, max;

		public RepartitedRandom(Func<double, double> repartitionFunction, Random r)
		{
			this.r = r;
			this.repartitionFunction = repartitionFunction;
			min = repartitionFunction(0);
			max = repartitionFunction(1);
		}

		public RepartitedRandom(Func<double, double> repartitionFunction)
		{
			this.r = new Random();
			this.repartitionFunction = repartitionFunction;
			min = repartitionFunction(0);
			max = repartitionFunction(1);
		}

		public RepartitedRandom(Func<double, double> repartitionFunction, int seed)
		{
			this.r = new Random(seed);
			this.repartitionFunction = repartitionFunction;
			min = repartitionFunction(0);
			max = repartitionFunction(1);
		}

		public double Next()
		{
			var temp = repartitionFunction((double)r.Next() / (double)int.MaxValue);
			temp = (temp - min) / (max - min);
			return temp;
		}

		public int Next(int min, int max)
		{
			var temp = Next();
			return (int)Math.Ceiling(temp * (max - min) + min);
		}

	}
}
