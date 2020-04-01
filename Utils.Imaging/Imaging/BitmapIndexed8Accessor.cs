using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Utils.Imaging
{
	public unsafe class BitmapIndexed8Accessor : IDisposable, IImageAccessor<byte>
	{
		private Bitmap bitmap;
		private BitmapData bmpdata = null;
		private byte* bytedata;
		private readonly int totalBytes;

		public int Width => bmpdata.Width;
		public int Height => bmpdata.Height;

		public BitmapIndexed8Accessor( Bitmap bitmap, Rectangle? region = null )
		{
			this.bitmap = bitmap;
			region = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);

			this.bmpdata = bitmap.LockBits(region.Value, ImageLockMode.ReadWrite, PixelFormat.Format8bppIndexed);
			this.totalBytes = bmpdata.Stride * bmpdata.Height;

			this.bytedata = (byte*)(void*)bmpdata.Scan0;
		}

		public byte this[Point point]
		{
			get { return bytedata[point.Y * bmpdata.Stride + point.X]; }
			set { bytedata[point.Y * bmpdata.Stride + point.X] = value; }
		}

		public byte this[int x, int y]
		{
			get { return bytedata[y * bmpdata.Stride + x]; }
			set { bytedata[y * bmpdata.Stride + x] = value; }
		}

		public void Rectangle(Rectangle rectangle, byte color)
		{
			Rectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color);
		}

		public void Rectangle(int left, int top, int width, int height, byte color)
		{
			int bottom = top + height;
			int right = left + width;
			for (int y = top; y <= bottom; y++)
			{
				int yOffset = y * bmpdata.Width;
				for (int x = left; x <= right; x++)
				{
					bytedata[yOffset + x] = color;
				}
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~BitmapIndexed8Accessor() => Dispose(false);
		
		protected virtual void Dispose(bool disposing)
		{
			if (bitmap != null && bmpdata != null) {
				this.bitmap.UnlockBits(bmpdata);
				this.bitmap = null;
				this.bytedata = null;
				this.bmpdata = null;
			}
		}

	}
}
