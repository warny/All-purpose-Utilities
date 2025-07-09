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

		public byte this[int x, int y]
		{
			get { return bytedata[y * bmpdata.Stride + x]; }
			set { bytedata[y * bmpdata.Stride + x] = value; }
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~BitmapIndexed8Accessor() => Dispose(false);
		
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
