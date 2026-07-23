using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace Utils.Imaging
{
    /// <summary>
    /// Provides direct memory access to 64-bit ARGB bitmap data for high precision pixel
    /// manipulation scenarios.
    /// </summary>
    /// <remarks>
    /// This class depends on <c>System.Drawing</c> (GDI+) and is supported on Windows only.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public unsafe class BitmapArgb64Accessor : IDisposable, IImageAccessor<ColorArgb64, ushort>, IImageAccessor<ulong>
    {
        private Bitmap bitmap;
        private BitmapData bmpdata;
        private ulong* ulongdata;
        // Stride expressed in ulong units (8 bytes per pixel in Format64bppArgb).
        private readonly int strideInPixels;
        private readonly int width;
        private readonly int height;
        private bool disposed;

        /// <summary>
        /// Gets the width of the accessed bitmap region.
        /// </summary>
        public int Width { get { ThrowIfDisposed(); return width; } }

        /// <summary>
        /// Gets the height of the accessed bitmap region.
        /// </summary>
        public int Height { get { ThrowIfDisposed(); return height; } }

        /// <inheritdoc/>
        ulong IImageAccessor<ulong>.this[int x, int y]
        {
            get
            {
                ThrowIfDisposed();
                ValidateCoordinates(x, y);
                return ulongdata[y * strideInPixels + x];
            }
            set
            {
                ThrowIfDisposed();
                ValidateCoordinates(x, y);
                ulongdata[y * strideInPixels + x] = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BitmapArgb64Accessor"/> class and
        /// locks the specified bitmap region.
        /// </summary>
        /// <param name="bitmap">Bitmap providing the pixel data.</param>
        /// <param name="region">Optional region to lock; when omitted the entire bitmap is used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="region"/> has non-positive dimensions or exceeds the bitmap bounds.
        /// </exception>
        public BitmapArgb64Accessor(Bitmap bitmap, Rectangle? region = null)
        {
            if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));

            var rgn = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            ValidateRegion(rgn, bitmap.Width, bitmap.Height);

            this.bitmap = bitmap;

            bmpdata = bitmap.LockBits(rgn, ImageLockMode.ReadWrite, PixelFormat.Format64bppArgb);
            try
            {
                ulongdata = (ulong*)(void*)bmpdata.Scan0;
                // Stride is in bytes; each pixel is sizeof(ulong) = 8 bytes.
                strideInPixels = bmpdata.Stride / sizeof(ulong);
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
        /// Gets or sets the color at the specified pixel coordinates.
        /// </summary>
        /// <param name="x">Horizontal pixel coordinate.</param>
        /// <param name="y">Vertical pixel coordinate.</param>
        /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose()"/> has been called.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any coordinate is out of bounds.</exception>
        public ColorArgb64 this[int x, int y]
        {
            get
            {
                ThrowIfDisposed();
                ValidateCoordinates(x, y);
                return new ColorArgb64(ulongdata[y * strideInPixels + x]);
            }
            set
            {
                ThrowIfDisposed();
                ValidateCoordinates(x, y);
                ulongdata[y * strideInPixels + x] = value.Value;
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
        public void ApplySprite(Point location, BitmapArgb64Accessor sprite, Func<ColorArgb64, ColorArgb64, ColorArgb64> blend)
        {
            ThrowIfDisposed();
            if (sprite is null) throw new ArgumentNullException(nameof(sprite));
            if (blend is null) throw new ArgumentNullException(nameof(blend));
            ImageAccessorExtensions.ApplySprite<ColorArgb64, ushort>(this, sprite, location, blend);
        }

        private static void ValidateRegion(Rectangle region, int bitmapWidth, int bitmapHeight)
        {
            if (region.Width <= 0 || region.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(region), "Region dimensions must be positive.");
            if (region.X < 0 || region.Y < 0 || region.Right > bitmapWidth || region.Bottom > bitmapHeight)
                throw new ArgumentOutOfRangeException(nameof(region), "Region must lie within the bitmap bounds.");
        }

        private void ValidateCoordinates(int x, int y)
        {
            if ((uint)x >= (uint)width)
                throw new ArgumentOutOfRangeException(nameof(x), x, $"x must be in [0, {width - 1}].");
            if ((uint)y >= (uint)height)
                throw new ArgumentOutOfRangeException(nameof(y), y, $"y must be in [0, {height - 1}].");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// Releases resources associated with the accessor and unlocks the bitmap.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizes the accessor and ensures resources are released.
        /// </summary>
        ~BitmapArgb64Accessor() => Dispose(false);

        /// <summary>
        /// Performs the actual resource cleanup.
        /// </summary>
        /// <param name="disposing">Indicates whether the method is called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (bitmap is not null && bmpdata is not null)
                    bitmap.UnlockBits(bmpdata);
                bitmap = null;
                ulongdata = null;
                bmpdata = null;
                disposed = true;
            }
        }
    }
}
