using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Imaging.Processing
{
	public abstract class ImageProcessor<T, A, T1>
		where T : IImageAccessor<A, T1>
		where A : IColorArgb<T1>
		where T1 : struct
	{
		protected abstract void Process( T imageAccessor );

		public static void Process( T accessor, params ImageProcessor<T, A, T1>[] processors )
		{
			foreach (var processor in processors) {
				processor.Process(accessor);
			}
		}
	}

	public abstract class ImageProcessorArgb32 : ImageProcessor<BitmapArgb32Accessor, ColorArgb32, byte> { }

	public abstract class ImageProcessorArgb64 : ImageProcessor<BitmapArgb64Accessor, ColorArgb64, ushort> { }
}
