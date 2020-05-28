using System.Drawing;
using System.Numerics;
using Utils.Imaging;
using Utils.Lists;

namespace Fractals
{
	public class ComputeFractal<T> : IComputeFractal where T : IFractal, new()
	{
		private struct ComputePixel
		{
			public ComputePixel(IFractal fractal)
			{
				this.Continue = true;
				this.Iteration = 0;
				this.Fractal = fractal;
			}

			public bool Continue { get; set; }
			IFractal Fractal { get; set; }
			public int Iteration { get; set; }

			public int Compute()
			{
				if (!Continue) return Iteration;
				Iteration++;
				if (Fractal.ComputeIteration())
				{
					Continue = false;
					return Iteration;
				}
				return -1;
			}

			public override string ToString() => $"{Iteration} {(Continue ? "Continue" : "Fin")} {Fractal}";
		}

		public Bitmap Image { get; }
		private readonly ComputePixel[,] Pixels;

		public ComputeFractal(Bitmap image, Complex center, Complex constant, double step)
		{

			this.Image = image;
			Pixels = new ComputePixel[image.Width, image.Height];

			var centerG = new Point(image.Width / 2, image.Height / 2);

			for (int x = 0; x < image.Width; x++)
			{
				for (int y = 0; y < image.Height; y++)
				{
					Complex complex = new Complex(center.Real + (x - centerG.X) * step, center.Imaginary - (y - centerG.Y) * step);
					var pixelParameters = new T();
					pixelParameters.Initialize(complex, constant);
					Pixels[x, y] = new ComputePixel(pixelParameters);
				}
			}
		}

		public void Compute()
		{
			using (var accessor = new BitmapArgb32Accessor(Image))
			{
				Compute(accessor);
			}
		}

		private readonly CachedLoader<int, ColorArgb32> Brushes = new CachedLoader<int, ColorArgb32>(
			i => new ColorArgb32((byte)(i * 3), (byte)(70 + i * 3), (byte)(240 + i * 3))
		);

		private void Compute(BitmapArgb32Accessor accessor)
		{
			ColorArgb32 black = new ColorArgb32(255, 0, 0, 0);

			for (int x = 0; x < accessor.Width; x++)
			{
				for (int y = 0; y < accessor.Height; y++)
				{
					var iteration = Pixels[x, y].Compute();
					if (iteration == -1)
						accessor[x, y] = black;
					else
						accessor[x, y] = Brushes[iteration];
				}
			}
		}
	}
}
