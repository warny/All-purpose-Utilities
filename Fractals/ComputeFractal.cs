using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Utils.Imaging;

namespace Fractals
{
	public unsafe class ComputeFractal
	{
		public void Compute(PictureBox pictureBox, Complex center, double step, int maxIterations, Func<Complex, Complex, Complex> func, Complex start)
		{
			var bitmap = new Bitmap(pictureBox.Width, pictureBox.Height);
			Compute(bitmap, center, step, maxIterations, func, start);
			pictureBox.Image = bitmap;
		}

		public void Compute(Bitmap image, Complex center, double step, int maxIterations, Func<Complex, Complex, Complex> func, Complex start)
		{
			using (var accessor = new BitmapArgb32Accessor(image))
			{
				Compute(accessor, center, step, maxIterations, func, start);
			}
		}

		public void Compute(BitmapArgb32Accessor accessor, Complex center, double step, int maxIterations, Func<Complex, Complex, Complex> func, Complex start)
		{
			var brushes = new ColorArgb32[maxIterations];
			ColorArgb32 black = new ColorArgb32(255, 0, 0, 0);
			for (int i = 0; i < maxIterations; i++) brushes[i] = new ColorArgb32((byte)(i * 3), (byte)(70 + i * 3), (byte)(240 + i * 3));

			var centerG = new Point(accessor.Width / 2, accessor.Height / 2);

			for (int x = 0; x < accessor.Width; x++) {
				for (int y = 0; y < accessor.Height; y++) {
					Complex complex = new Complex(center.Real + (x - centerG.X) * step, center.Imaginary - (y - centerG.Y) * step);
					var iteration = ComputePoint2(complex, maxIterations, func, start);
					if (iteration == -1)
					{
						accessor[x, y] = black;
					}
					else
					{
						accessor[x, y] = brushes[iteration];
					}
				}
			}
		}

		public int ComputePoint(Complex complex, int maxIterations, Func<Complex, Complex, Complex> func, Complex start)
		{
			var iteration = start;
			for (int i = 0; i < maxIterations; i++)
			{
				iteration = func(iteration, complex);
				if (iteration.Magnitude > 10) return i;
			}
			return -1;
		}

		public int ComputePoint2(Complex complex, int maxIterations, Func<Complex, Complex, Complex> func, Complex start)
		{
			var iteration = complex;
			for (int i = 0; i < maxIterations; i++)
			{
				iteration = func(iteration, start);
				if (iteration.Magnitude > 10) return i;
			}
			return -1;
		}
	}
}
