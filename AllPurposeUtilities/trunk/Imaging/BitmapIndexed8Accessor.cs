using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Imaging
{
	public unsafe class BitmapIndexed8Accessor : IDisposable, IImageAccessor<byte>
	{
		Bitmap bitmap;
		BitmapData bmpdata = null;
		byte* uintdata;
		int totalBytes;

		public int Width => bmpdata.Width;
		public int Height => bmpdata.Height;

		public BitmapIndexed8Accessor( Bitmap bitmap, Rectangle? region = null )
		{
			this.bitmap = bitmap;
			region = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);

			this.bmpdata = bitmap.LockBits(region.Value, ImageLockMode.ReadWrite, PixelFormat.Format8bppIndexed);
			this.totalBytes = bmpdata.Stride * bmpdata.Height;

			this.uintdata = (byte*)(void*)bmpdata.Scan0;
		}

		public byte this[Point point]
		{
			get { return uintdata[point.Y * bmpdata.Stride + point.X]; }
			set { uintdata[point.Y * bmpdata.Stride + point.X] = value; }
		}

		public byte this[int x, int y]
		{
			get { return uintdata[y * bmpdata.Stride + x]; }
			set { uintdata[y * bmpdata.Stride + x] = value; }
		}

		public void Rectangle( Rectangle r, byte value )
		{
			for (int y = r.Top ; y <= r.Bottom ; y++) {
				int yOffset = y * bmpdata.Width;
				for (int x = r.Left; x <= r.Right ; x++) {
					uintdata[yOffset + x] = value;
				}
			}
		}

		public void Fill( int left, int top, int right, int bottom, byte value )
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
