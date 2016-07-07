using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Imaging
{
	public unsafe class BitmapArgb32Accessor : IDisposable, IImageAccessor<ColorArgb32, byte>, IImageAccessor<uint>
	{
		Bitmap bitmap;
		BitmapData bmpdata = null;
		uint* uintdata;
		int totalBytes;

		public int Width => bmpdata.Width;
		public int Height => bmpdata.Height;

		uint IImageAccessor<uint>.this[Point point]
		{
			get { return uintdata[point.Y * bmpdata.Width + point.X]; }
			set { uintdata[point.Y * bmpdata.Width + point.X] = value; }
		}

		uint IImageAccessor<uint>.this[int x, int y]
		{
			get { return uintdata[y * bmpdata.Width + x]; }
			set { uintdata[y * bmpdata.Width + x] = value; }
		}

		public BitmapArgb32Accessor( Bitmap bitmap, Rectangle? region = null )
		{
			this.bitmap = bitmap;
			region = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);

			this.bmpdata = bitmap.LockBits(region.Value, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			this.totalBytes = bmpdata.Stride * bmpdata.Height;

			this.uintdata = (uint*)(void*)bmpdata.Scan0;
		}

		public ColorArgb32 this[Point point]
		{
			get { return new ColorArgb32(uintdata[point.Y * bmpdata.Width + point.X]); }
			set { uintdata[point.Y * bmpdata.Width + point.X] = value.Value; }
		}

		public ColorArgb32 this[int x, int y]
		{
			get { return new ColorArgb32(uintdata[y * bmpdata.Width + x]); }
			set { uintdata[y * bmpdata.Width + x] = value.Value; }
		}

		public void Rectangle( Rectangle r, uint value )
		{
			for (int y = r.Top ; y <= r.Bottom ; y++) {
				int yOffset = y * bmpdata.Width;
				for (int x = r.Left ; x <= r.Right ; x++) {
					uintdata[yOffset + x] = value;
				}
			}
		}

		public void Fill( int left, int top, int right, int bottom, uint value )
		{
			for (int y = top ; y <= bottom ; y++) {
				int yOffset = y * bmpdata.Width;
				for (int x = left ; x <= right ; x++) {
					uintdata[yOffset + x] = value;
				}
			}
		}

		public void Dispose()
		{
			if (bitmap != null && bmpdata != null) {
				this.bitmap.UnlockBits(bmpdata);
				this.bitmap = null;
				this.bmpdata = null;
			}
		}

	}
}
