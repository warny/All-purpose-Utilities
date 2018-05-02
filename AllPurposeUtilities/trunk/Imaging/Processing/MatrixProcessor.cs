using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Imaging.Processing
{
	public class MatrixProcessorArgb32 : ImageProcessorArgb32
	{
		public double[,] Matrix { get; private set; }
		public Point Center { get; private set; }

		public MatrixProcessorArgb32( double[,] matrix, Point center )
		{
			Matrix = matrix;
			Center = center;
		}

		protected override void Process( BitmapArgb32Accessor imageAccessor )
		{
			var source = imageAccessor.CopyToColorArray();

			for (int y = 0 ; y <= imageAccessor.Height ; y++) {
				for (int x = 0 ; x <= imageAccessor.Width ; x++) {
					int minX = Math.Max(0, x-Center.X);
					int minY = Math.Max(0, y-Center.Y);

					int maxX = Math.Min(imageAccessor.Width - x, x-Center.X + Matrix.GetUpperBound(0));
					int maxY = Math.Min(imageAccessor.Height - y, x-Center.Y + Matrix.GetUpperBound(1));

					double A = 1, r = 0, g = 0, b = 0;
					for (int matY = minY ; matY < maxY ; matY++) {
						for (int matX = minX ; matX < maxX ; matX++) {
							r += source[matX + x, matY + y].Red * Matrix[matX, matY];
							g += source[matX + x, matY + y].Green * Matrix[matX, matY];
							b += source[matX + x, matY + y].Blue * Matrix[matX, matY];
						}
					}
					imageAccessor[x, y] = new ColorArgb32((byte)(A), (byte)(r), (byte)(g), (byte)(b));
				}
			}

		}
	}
}
