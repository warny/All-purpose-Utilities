using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Imaging.Processing
{
	public class BlurArgb32 : MatrixProcessorArgb32
	{
		public BlurArgb32( double radius ) : base(CreateMatrix(radius), new Point((int)Math.Ceiling(radius) + 1, (int)Math.Ceiling(radius) + 1))
		{
		}

		private static double[,] CreateMatrix( double radius )
		{
			int size = (int)Math.Ceiling(radius) * 2 + 1;
			double[,] matrix = new double[size, size];



			return matrix;
		}
	}
}
