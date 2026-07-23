using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Utils.Mathematics;

namespace Utils.Imaging
{
    /// <summary>
    /// Provides direct memory access to 32-bit ARGB bitmap data, enabling fast pixel
    /// manipulation scenarios.
    /// </summary>
    /// <remarks>
    /// This class depends on <c>System.Drawing</c> (GDI+) and is supported on Windows only.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public unsafe class BitmapArgb32Accessor : IDisposable, IImageAccessor<ColorArgb32, byte>, IImageAccessor<uint>
    {
        private Bitmap bitmap;
        private BitmapData bmpdata;
        private uint* uintdata;
        private readonly int totalBytes;
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
        uint IImageAccessor<uint>.this[int x, int y]
        {
            get { ThrowIfDisposed(); return uintdata[y * width + x]; }
            set { ThrowIfDisposed(); uintdata[y * width + x] = value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BitmapArgb32Accessor"/> class and
        /// locks the specified bitmap region.
        /// </summary>
        /// <param name="bitmap">Bitmap providing the pixel data.</param>
        /// <param name="region">Optional region to lock; when omitted the entire bitmap is used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
        public BitmapArgb32Accessor(Bitmap bitmap, Rectangle? region = null)
        {
            if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));
            this.bitmap = bitmap;
            region = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            this.bmpdata = bitmap.LockBits(region.Value, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                this.uintdata = (uint*)(void*)bmpdata.Scan0;
                this.width = bmpdata.Width;
                this.height = bmpdata.Height;
                this.totalBytes = bmpdata.Stride * bmpdata.Height;
            }
            catch
            {
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
        public ColorArgb32 this[int x, int y]
        {
            get { ThrowIfDisposed(); return new ColorArgb32(uintdata[y * width + x]); }
            set { ThrowIfDisposed(); uintdata[y * width + x] = value.Value; }
        }

        /// <summary>
        /// Copies the raw pixel data to an array of unsigned integers.
        /// </summary>
        /// <returns>Array containing the raw pixel values.</returns>
        /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose()"/> has been called.</exception>
        public uint[] CopyToArray()
        {
            ThrowIfDisposed();
            uint[] copy = new uint[totalBytes / sizeof(uint)];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = uintdata[i];
            }
            return copy;
        }

        /// <summary>
        /// Copies the pixel data to a two-dimensional array of color structures.
        /// </summary>
        /// <returns>Matrix of <see cref="ColorArgb32"/> values.</returns>
        /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose()"/> has been called.</exception>
        public ColorArgb32[,] CopyToColorArray()
        {
            ThrowIfDisposed();
            ColorArgb32[,] copy = new ColorArgb32[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    copy[x, y] = new ColorArgb32(uintdata[y * width + x]);
                }
            }
            return copy;
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
        public void ApplySprite(Point location, BitmapArgb32Accessor sprite, Func<ColorArgb32, ColorArgb32, ColorArgb32> blend)
        {
            ThrowIfDisposed();
            if (sprite is null) throw new ArgumentNullException(nameof(sprite));
            if (blend is null) throw new ArgumentNullException(nameof(blend));
            for (int sy = 0; sy < sprite.Height; sy++)
            {
                int dy = location.Y + sy;
                if (dy < 0 || dy >= Height) continue;

                for (int sx = 0; sx < sprite.Width; sx++)
                {
                    int dx = location.X + sx;
                    if (dx < 0 || dx >= Width) continue;

                    ColorArgb32 src = sprite[sx, sy];
                    ColorArgb32 dst = this[dx, dy];
                    ColorArgb32 result = blend(src, dst);
                    this[dx, dy] = result;
                }
            }
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
        ~BitmapArgb32Accessor() => Dispose(false);

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
                uintdata = null;
                bmpdata = null;
                disposed = true;
            }
        }

    }
}
