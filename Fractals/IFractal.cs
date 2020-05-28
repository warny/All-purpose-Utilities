using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Fractals
{
	public interface IFractal
	{
		void Initialize(Complex variable, Complex constant);
		bool ComputeIteration();
	}

	public class Mandelbrot : IFractal
	{
		private Complex Complex;
		private Complex Iteration;
		public bool ComputeIteration()
		{
			Iteration = Iteration * Iteration + Complex;
			return Iteration.Magnitude > 10;
		}

		public void Initialize(Complex variable, Complex constant)
		{
			this.Iteration = constant;
			this.Complex = variable;
		}

		public override string ToString() => Iteration.ToString();
	}

	public class Julia : IFractal
	{
		private Complex Complex;
		private Complex Iteration;
		public bool ComputeIteration()
		{
			Iteration = Iteration * Iteration + Complex;
			return Iteration.Magnitude > 10;
		}

		public void Initialize(Complex variable, Complex constant)
		{
			this.Iteration = variable;
			this.Complex = constant;
		}
		public override string ToString() => Iteration.ToString();
	}
}
