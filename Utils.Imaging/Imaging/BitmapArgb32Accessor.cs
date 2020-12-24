using System;
using System.Drawing;
using System.Drawing.Imaging;
using Utils.Mathematics;

namespace Utils.Imaging
{
	public unsafe class BitmapArgb32Accessor : IDisposable, IImageAccessor<ColorArgb32, byte>, IImageAccessor<uint>
	{
		private Bitmap bitmap;
		private BitmapData bmpdata = null;
		private uint* uintdata;
		private readonly int totalBytes;

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

		public uint[] CopyToArray()
		{
			uint[] copy = new uint[totalBytes / sizeof(uint)];
			for (int i = 0 ; i < copy.Length ; i++) {
				copy[i] = uintdata[i];
			}
			return copy;
		}

		public ColorArgb32[,] CopyToColorArray()
		{
			ColorArgb32[,] copy = new ColorArgb32[Width, Height];
			for (int y = 0 ; y < Height ; y++) {
				for (int x = 0 ; x < Width ; x++) {
					copy[x, y] = this[x, y];
				}
			}
			return copy;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~BitmapArgb32Accessor() => Dispose(false);

		protected virtual void Dispose(bool disposing)
		{
			if (bitmap != null && bmpdata != null) {
				this.bitmap.UnlockBits(bmpdata);
				this.bitmap = null;
				this.uintdata = null;
				this.bmpdata = null;
			}
		}

	}
}
