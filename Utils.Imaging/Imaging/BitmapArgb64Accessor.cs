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

		/// <summary>
		/// Draws a sprite bitmap onto this bitmap at the specified location using the
		/// provided blending function.
		/// </summary>
		/// <param name="location">Top-left destination coordinates.</param>
		/// <param name="sprite">Bitmap containing the sprite.</param>
		/// <param name="blend">Function blending sprite and destination colors.</param>
		public void ApplySprite(Point location, BitmapArgb64Accessor sprite, Func<ColorArgb64, ColorArgb64, ColorArgb64> blend)
		{
			for (int sy = 0; sy < sprite.Height; sy++)
			{
				int dy = location.Y + sy;
				if (dy < 0 || dy >= Height) continue;

				for (int sx = 0; sx < sprite.Width; sx++)
				{
					int dx = location.X + sx;
					if (dx < 0 || dx >= Width) continue;

					ColorArgb64 src = sprite[sx, sy];
					ColorArgb64 dst = this[dx, dy];
					ColorArgb64 result = blend(src, dst);
					this[dx, dy] = result;
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
			if (bitmap is not null && bmpdata is not null) {
				this.bitmap.UnlockBits(bmpdata);
				this.bitmap = null;
				this.ulongdata = null;
				this.bmpdata = null;
			}
		}

	}
}
