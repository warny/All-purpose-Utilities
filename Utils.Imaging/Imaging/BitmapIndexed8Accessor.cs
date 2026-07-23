using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Utils.Imaging
{
    /// <summary>
    /// Provides direct memory access to 8-bit indexed bitmap data.
    /// </summary>
    public unsafe class BitmapIndexed8Accessor : IDisposable, IImageAccessor<byte>
    {
        private Bitmap bitmap;
        private BitmapData bmpdata;
        private byte* bytedata;
        private readonly int stride;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="BitmapIndexed8Accessor"/> class and
        /// locks the specified bitmap region.
        /// </summary>
        /// <param name="bitmap">Bitmap providing the pixel data.</param>
        /// <param name="region">Optional region to lock; when omitted the entire bitmap is used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="region"/> has non-positive dimensions or exceeds the bitmap bounds.
        /// </exception>
        public BitmapIndexed8Accessor(Bitmap bitmap, Rectangle? region = null)
        {
            if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));

            var rgn = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            ValidateRegion(rgn, bitmap.Width, bitmap.Height);

            this.bitmap = bitmap;

            bmpdata = bitmap.LockBits(rgn, ImageLockMode.ReadWrite, PixelFormat.Format8bppIndexed);
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
        /// Gets or sets the indexed value at the specified pixel coordinates.
        /// </summary>
        /// <param name="x">Horizontal pixel coordinate.</param>
        /// <param name="y">Vertical pixel coordinate.</param>
        /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose()"/> has been called.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any coordinate is out of bounds.</exception>
        public byte this[int x, int y]
        {
            get
            {
                ThrowIfDisposed();
                ValidateCoordinates(x, y);
                return bytedata[y * stride + x];
            }
            set
            {
                ThrowIfDisposed();
                ValidateCoordinates(x, y);
                bytedata[y * stride + x] = value;
            }
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
        ~BitmapIndexed8Accessor() => Dispose(false);

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
                bytedata = null;
                bmpdata = null;
                disposed = true;
            }
        }
    }
}
