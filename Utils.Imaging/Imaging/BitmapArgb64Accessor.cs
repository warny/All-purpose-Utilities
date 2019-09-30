using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Imaging
{
	public unsafe class BitmapArgb64Accessor : IDisposable, IImageAccessor<ColorArgb64, ushort>, IImageAccessor <ulong>
	{
		Bitmap bitmap;
		BitmapData bmpdata = null;
		ulong* uintdata;
		int totalBytes;

		public int Width => bmpdata.Width;
		public int Height => bmpdata.Height;

		ulong IImageAccessor<ulong>.this[Point point]
		{
			get { return uintdata[point.Y * bmpdata.Width + point.X]; }
			set { uintdata[point.Y * bmpdata.Width + point.X] = value; }
		}

		ulong IImageAccessor<ulong>.this[int x, int y]
		{
			get { return uintdata[y * bmpdata.Width + x]; }
			set { uintdata[y * bmpdata.Width + x] = value; }
		}

		public BitmapArgb64Accessor( Bitmap bitmap, Rectangle? region = null )
		{
			this.bitmap = bitmap;
			region = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);

			this.bmpdata = bitmap.LockBits(region.Value, ImageLockMode.ReadWrite, PixelFormat.Format64bppArgb);
			this.totalBytes = bmpdata.Stride * bmpdata.Height;

			this.uintdata = (ulong*)(void*)bmpdata.Scan0;
		}
		public ColorArgb64 this[Point point]
		{
			get { return new ColorArgb64(uintdata[point.Y * bmpdata.Width + point.X]); }
			set { uintdata[point.Y * bmpdata.Width + point.X] = value.Value; }
		}

		public ColorArgb64 this[int x, int y]
		{
			get { return new ColorArgb64(uintdata[y * bmpdata.Width + x]); }
			set { uintdata[y * bmpdata.Width + x] = value.Value; }
		}

		public void Fill( Rectangle r, ulong value )
		{
			for (int y = r.Top ; y <= r.Bottom ; y++) {
				int yOffset = y * bmpdata.Width;
				for (int x = r.Left ; x <= r.Right ; x++) {
					uintdata[yOffset + x] = value;
				}
			}
		}

		public void Rectangle( int left, int top, int right, int bottom, ulong value )
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
