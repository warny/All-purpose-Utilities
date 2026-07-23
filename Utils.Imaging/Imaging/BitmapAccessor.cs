using System;
using System.Collections.Generic;
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
        private BitmapData bmpdata;
        private readonly PixelFormat pixelformat;
        private byte* bytedata;
        private readonly int stride;
        private readonly int width;
        private readonly int height;
        private bool disposed;

        /// <summary>
        /// Gets the bitmap width in pixels.
        /// </summary>
        public int Width { get { ThrowIfDisposed(); return width; } }

        /// <summary>
        /// Gets the bitmap height in pixels.
        /// </summary>
        public int Height { get { ThrowIfDisposed(); return height; } }

        /// <summary>
        /// Gets the number of bytes per pixel.
        /// </summary>
        public int ColorDepth { get; }

        private static readonly IReadOnlyDictionary<PixelFormat, int> ColorDepths =
            new Dictionary<PixelFormat, int>
            {
                { PixelFormat.Alpha,                   1 },
                { PixelFormat.PAlpha,                  1 },
                { PixelFormat.Format8bppIndexed,       1 },
                { PixelFormat.Format16bppGrayScale,    2 },
                { PixelFormat.Format16bppRgb555,       2 },
                { PixelFormat.Format16bppRgb565,       2 },
                { PixelFormat.Format16bppArgb1555,     2 },
                { PixelFormat.Format24bppRgb,          3 },
                { PixelFormat.Canonical,               4 },
                { PixelFormat.Format32bppRgb,          4 },
                { PixelFormat.Format32bppArgb,         4 },
                { PixelFormat.Format32bppPArgb,        4 },
                { PixelFormat.Format48bppRgb,          6 },
                { PixelFormat.Format64bppArgb,         8 },
                { PixelFormat.Format64bppPArgb,        8 },
            }.ToImmutableDictionary();

        /// <summary>
        /// Initializes a new instance of the <see cref="BitmapAccessor"/> class.
        /// </summary>
        /// <param name="bitmap">The bitmap to access.</param>
        /// <param name="pixelformat">The pixel format to use.</param>
        /// <param name="region">Optional region of the bitmap to lock.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="region"/> has non-positive dimensions or exceeds the bitmap bounds.
        /// </exception>
        /// <exception cref="NotSupportedException">Thrown when the resolved pixel format is not supported.</exception>
        public BitmapAccessor(Bitmap bitmap, PixelFormat pixelformat = PixelFormat.Undefined, Rectangle? region = null)
        {
            if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));

            var fmt = pixelformat == PixelFormat.Undefined ? bitmap.PixelFormat : pixelformat;
            // Resolve color depth before touching the bitmap so that an unsupported format
            // cannot leave LockBits half-committed.
            int colorDepth = GetColorDepth(fmt);

            var rgn = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            ValidateRegion(rgn, bitmap.Width, bitmap.Height);

            this.pixelformat = fmt;
            this.ColorDepth = colorDepth;
            this.bitmap = bitmap;

            bmpdata = bitmap.LockBits(rgn, ImageLockMode.ReadWrite, fmt);
            try
            {
                bytedata = (byte*)(void*)bmpdata.Scan0;
                stride = bmpdata.Stride;
                width = bmpdata.Width;
                height = bmpdata.Height;
            }
            catch
            {
                // Ensure we never leave the bitmap locked when construction fails.
                bitmap.UnlockBits(bmpdata);
                bmpdata = null;
                throw;
            }
        }

        /// <summary>
        /// Gets or sets the raw value at the specified pixel and color component.
        /// </summary>
        /// <param name="x">Horizontal coordinate.</param>
        /// <param name="y">Vertical coordinate.</param>
        /// <param name="c">Color component index.</param>
        /// <returns>The byte at the specified location.</returns>
        /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose()"/> has been called.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any coordinate is out of bounds.</exception>
        public byte this[int x, int y, int c]
        {
            get
            {
                ThrowIfDisposed();
                ValidateCoordinates(x, y, c);
                return bytedata[y * stride + x * ColorDepth + c];
            }
            set
            {
                ThrowIfDisposed();
                ValidateCoordinates(x, y, c);
                bytedata[y * stride + x * ColorDepth + c] = value;
            }
        }

        /// <summary>
        /// Draws a sprite bitmap onto this bitmap at the specified location using the
        /// provided blending function.
        /// </summary>
        /// <param name="location">Top-left destination coordinates.</param>
        /// <param name="sprite">Bitmap containing the sprite.</param>
        /// <param name="blend">Function blending sprite and destination colors.</param>
        /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose()"/> has been called.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sprite"/> or <paramref name="blend"/> is null.</exception>
        public void ApplySprite(Point location, BitmapAccessor sprite, Func<ColorArgb32, ColorArgb32, ColorArgb32> blend)
        {
            ThrowIfDisposed();
            if (sprite is null) throw new ArgumentNullException(nameof(sprite));
            if (blend is null) throw new ArgumentNullException(nameof(blend));

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
        private static int GetColorDepth(PixelFormat pixelFormat) =>
            ColorDepths.TryGetValue(pixelFormat, out var depth)
                ? depth
                : throw new NotSupportedException($"La valeur {pixelFormat} n'est pas supportée");

        private static void ValidateRegion(Rectangle region, int bitmapWidth, int bitmapHeight)
        {
            if (region.Width <= 0 || region.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(region), "Region dimensions must be positive.");
            if (region.X < 0 || region.Y < 0 || region.Right > bitmapWidth || region.Bottom > bitmapHeight)
                throw new ArgumentOutOfRangeException(nameof(region), "Region must lie within the bitmap bounds.");
        }

        private void ValidateCoordinates(int x, int y, int c)
        {
            if ((uint)x >= (uint)width)
                throw new ArgumentOutOfRangeException(nameof(x), x, $"x must be in [0, {width - 1}].");
            if ((uint)y >= (uint)height)
                throw new ArgumentOutOfRangeException(nameof(y), y, $"y must be in [0, {height - 1}].");
            if ((uint)c >= (uint)ColorDepth)
                throw new ArgumentOutOfRangeException(nameof(c), c, $"Component index must be in [0, {ColorDepth - 1}].");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
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
            if (!disposed)
            {
                if (bitmap is not null && bmpdata is not null)
                    bitmap.UnlockBits(bmpdata);
                bitmap = null;
                bytedata = null;
                bmpdata = null;
                disposed = true;
            }
        }
    }
}
