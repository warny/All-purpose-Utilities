using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Utils.Imaging
{
	public unsafe class BitmapArgb64Accessor : IDisposable, IImageAccessor<ColorArgb64, ushort>, IImageAccessor <ulong>
	{
		private Bitmap bitmap;
		private BitmapData bmpdata = null;
		private ulong* ulongdata;
		private readonly int totalBytes;

		public int Width => bmpdata.Width;
		public int Height => bmpdata.Height;

		ulong IImageAccessor<ulong>.this[Point point]
		{
			get { return ulongdata[point.Y * bmpdata.Width + point.X]; }
			set { ulongdata[point.Y * bmpdata.Width + point.X] = value; }
		}

		ulong IImageAccessor<ulong>.this[int x, int y]
		{
			get { return ulongdata[y * bmpdata.Width + x]; }
			set { ulongdata[y * bmpdata.Width + x] = value; }
		}

		public BitmapArgb64Accessor( Bitmap bitmap, Rectangle? region = null )
		{
			this.bitmap = bitmap;
			region = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);

			this.bmpdata = bitmap.LockBits(region.Value, ImageLockMode.ReadWrite, PixelFormat.Format64bppArgb);
			this.totalBytes = bmpdata.Stride * bmpdata.Height;

			this.ulongdata = (ulong*)(void*)bmpdata.Scan0;
		}
		public ColorArgb64 this[Point point]
		{
			get { return new ColorArgb64(ulongdata[point.Y * bmpdata.Width + point.X]); }
			set { ulongdata[point.Y * bmpdata.Width + point.X] = value.Value; }
		}

		public ColorArgb64 this[int x, int y]
		{
			get { return new ColorArgb64(ulongdata[y * bmpdata.Width + x]); }
			set { ulongdata[y * bmpdata.Width + x] = value.Value; }
		}

		public void Rectangle(Rectangle rectangle, ColorArgb64 color) => Rectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color.Value);
		public void Rectangle(Rectangle rectangle, ulong color) => Rectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color);
		public void Rectangle(int left, int top, int width, int height, ColorArgb64 color) => Rectangle(left, top, width, height, color.Value);
		public void Rectangle(int left, int top, int width, int height, ulong color)
		{
			int bottom = top + height;
			int right = left + width;
			for (int y = top; y <= bottom; y++)
			{
				int yOffset = y * bmpdata.Width;
				for (int x = left; x <= right; x++)
				{
					ulongdata[yOffset + x] = color;
				}
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~BitmapArgb64Accessor() => Dispose(false);

		protected virtual void Dispose(bool disposing)
		{
			if (bitmap != null && bmpdata != null) {
				this.bitmap.UnlockBits(bmpdata);
				this.bitmap = null;
				this.ulongdata = null;
				this.bmpdata = null;
			}
		}

	}
}
