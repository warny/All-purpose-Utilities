using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Utils.Imaging
{
	public unsafe class BitmapAccessor : IDisposable
	{
		private Bitmap bitmap;
		private BitmapData bmpdata = null;
		private readonly PixelFormat pixelformat;
		private byte* bytedata;
		private readonly int totalBytes;

		public int Width => bmpdata.Width;
		public int Height => bmpdata.Height;
		public int ColorDepth { get; }

		public BitmapAccessor( Bitmap bitmap, PixelFormat pixelformat = PixelFormat.Undefined, Rectangle? region = null )
		{
			this.bitmap = bitmap;
			this.pixelformat = pixelformat== PixelFormat.Undefined ? bitmap.PixelFormat : pixelformat;
			region = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);

			this.bmpdata = bitmap.LockBits(region.Value, ImageLockMode.ReadWrite, this.pixelformat);
			this.ColorDepth = GetColorDepth(this.pixelformat);
			this.totalBytes = bmpdata.Stride * bmpdata.Height;

			this.bytedata = (byte*)(void*)bmpdata.Scan0;
		}

		public byte this[int x, int y, int c]
		{
			get { return bytedata[y * this.bmpdata.Stride + x * ColorDepth + c]; }
			set { bytedata[y * this.bmpdata.Stride + x * ColorDepth + c] = value; }
		}

		private static int GetColorDepth( PixelFormat pixelFormat )
		{
			switch (pixelFormat) {
				case PixelFormat.Undefined:
				case PixelFormat.Indexed:
				case PixelFormat.Extended:
				case PixelFormat.Gdi:
				case PixelFormat.Format1bppIndexed:
				case PixelFormat.Format4bppIndexed:
				case PixelFormat.Max:
					break;
				case PixelFormat.Alpha:
				case PixelFormat.PAlpha:
					return 1;
				case PixelFormat.Format8bppIndexed:
					return 1;
				case PixelFormat.Format16bppGrayScale:
				case PixelFormat.Format16bppRgb555:
				case PixelFormat.Format16bppRgb565:
				case PixelFormat.Format16bppArgb1555:
					return 2;
				case PixelFormat.Format24bppRgb:
					return 3;
				case PixelFormat.Canonical:
				case PixelFormat.Format32bppRgb:
				case PixelFormat.Format32bppArgb:
				case PixelFormat.Format32bppPArgb:
					return 4;
				case PixelFormat.Format48bppRgb:
					return 6;
				case PixelFormat.Format64bppArgb:
					return 8;
				case PixelFormat.Format64bppPArgb:
					return 8;
				default:
					break;
			}
			throw new NotSupportedException($"La valeur {pixelFormat} n'est pas supportée");
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~BitmapAccessor() => Dispose(false);

		protected virtual void Dispose(bool disposing)
		{
			if (bitmap is not null && bmpdata is not null) {
				this.bitmap.UnlockBits(bmpdata);
				this.bitmap = null;
				this.bytedata = null;
				this.bmpdata = null;
			}
		}
	}
}
