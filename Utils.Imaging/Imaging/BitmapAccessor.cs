using System;
using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;

namespace Utils.Imaging
{
	/// <summary>
	/// Provides direct indexed access to a bitmap.
	/// </summary>
	public unsafe class BitmapAccessor : IDisposable
	{
		private Bitmap bitmap;
		private BitmapData bmpdata = null;
		private readonly PixelFormat pixelformat;
		private byte* bytedata;
		private readonly int totalBytes;
		/// <summary>
		/// Cached offsets for the start of each line.
		/// </summary>
		private readonly ImmutableArray<int> lineOffsets;

		/// <summary>
		/// Cached offsets for the start of each column.
		/// </summary>
		private readonly ImmutableArray<int> columnOffsets;

		/// <summary>
		/// Gets the bitmap width in pixels.
		/// </summary>
		public int Width => bmpdata.Width;

		/// <summary>
		/// Gets the bitmap height in pixels.
		/// </summary>
		public int Height => bmpdata.Height;

		/// <summary>
		/// Gets the number of bytes per pixel.
		/// </summary>
		public int ColorDepth { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="BitmapAccessor"/> class.
		/// </summary>
		/// <param name="bitmap">The bitmap to access.</param>
		/// <param name="pixelformat">The pixel format to use.</param>
		/// <param name="region">Optional region of the bitmap to lock.</param>
		public BitmapAccessor(Bitmap bitmap, PixelFormat pixelformat = PixelFormat.Undefined, Rectangle? region = null)
		{
			this.bitmap = bitmap;
			this.pixelformat = pixelformat == PixelFormat.Undefined ? bitmap.PixelFormat : pixelformat;
			region ??= new Rectangle(0, 0, bitmap.Width, bitmap.Height);

			this.bmpdata = bitmap.LockBits(region.Value, ImageLockMode.ReadWrite, this.pixelformat);
			this.ColorDepth = GetColorDepth(this.pixelformat);
			this.totalBytes = bmpdata.Stride * bmpdata.Height;

			this.bytedata = (byte*)(void*)bmpdata.Scan0;

			var lineBuilder = ImmutableArray.CreateBuilder<int>(bmpdata.Height);
			int index = 0;
			for (int i = 0; i < bmpdata.Height; i++, index += bmpdata.Stride)
			{
				lineBuilder.Add(index);
			}
			lineOffsets = lineBuilder.ToImmutable();

			var columnBuilder = ImmutableArray.CreateBuilder<int>(bmpdata.Width);
			for (int i = 0; i < bmpdata.Width; i++)
			{
				columnBuilder.Add(i * ColorDepth);
			}
			columnOffsets = columnBuilder.ToImmutable();
		}

		/// <summary>
		/// Gets or sets the raw value at the specified pixel and color component.
		/// </summary>
		/// <param name="x">Horizontal coordinate.</param>
		/// <param name="y">Vertical coordinate.</param>
		/// <param name="c">Color component index.</param>
		/// <returns>The byte at the specified location.</returns>
		public byte this[int x, int y, int c]
		{
			get => bytedata[lineOffsets[y] + columnOffsets[x] + c];
			set => bytedata[lineOffsets[y] + columnOffsets[x] + c] = value;
		}

		/// <summary>
		/// Draws a sprite bitmap onto this bitmap at the specified location using the
		/// provided blending function.
		/// </summary>
		/// <param name="location">Top-left destination coordinates.</param>
		/// <param name="sprite">Bitmap containing the sprite.</param>
		/// <param name="blend">Function blending sprite and destination colors.</param>
		public void ApplySprite(Point location, BitmapAccessor sprite, Func<ColorArgb32, ColorArgb32, ColorArgb32> blend)
		{
			if (ColorDepth != 4 || sprite.ColorDepth != 4)
			{
				throw new NotSupportedException("Only 32bpp images are supported for sprite blending.");
			}

			for (int sy = 0; sy < sprite.Height; sy++)
			{
				int dy = location.Y + sy;
				if (dy < 0 || dy >= Height) continue;

				for (int sx = 0; sx < sprite.Width; sx++)
				{
					int dx = location.X + sx;
					if (dx < 0 || dx >= Width) continue;

					ColorArgb32 src = new(
							sprite[sx, sy, 3],
							sprite[sx, sy, 2],
							sprite[sx, sy, 1],
							sprite[sx, sy, 0]);

					ColorArgb32 dst = new(
							this[dx, dy, 3],
							this[dx, dy, 2],
							this[dx, dy, 1],
							this[dx, dy, 0]);

					ColorArgb32 result = blend(src, dst);

					this[dx, dy, 0] = result.Blue;
					this[dx, dy, 1] = result.Green;
					this[dx, dy, 2] = result.Red;
					this[dx, dy, 3] = result.Alpha;
				}
			}
		}

		/// <summary>
		/// Returns the number of bytes per pixel for the specified format.
		/// </summary>
		/// <param name="pixelFormat">The pixel format.</param>
		/// <returns>The number of bytes per pixel.</returns>
		private static int GetColorDepth(PixelFormat pixelFormat)
		{
			switch (pixelFormat)
			{
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

		/// <summary>
		/// Releases resources associated with the bitmap.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Finalizer.
		/// </summary>
		~BitmapAccessor() => Dispose(false);

		/// <summary>
		/// Unlocks the bitmap and frees unmanaged resources.
		/// </summary>
		/// <param name="disposing">Indicates whether managed resources should be disposed.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (bitmap is not null && bmpdata is not null)
			{
				this.bitmap.UnlockBits(bmpdata);
				this.bitmap = null;
				this.bytedata = null;
				this.bmpdata = null;
			}
		}
	}
}
