using System;
using System.Drawing;
using System.Drawing.Imaging;
using Utils.Mathematics;

namespace Utils.Imaging
{
    /// <summary>
    /// Provides direct memory access to 32-bit ARGB bitmap data, enabling fast pixel
    /// manipulation scenarios.
    /// </summary>
    public unsafe class BitmapArgb32Accessor : IDisposable, IImageAccessor<ColorArgb32, byte>, IImageAccessor<uint>
    {
        private Bitmap bitmap;
        private BitmapData bmpdata = null;
        private uint* uintdata;
        private readonly int totalBytes;

        /// <summary>
        /// Gets the width of the accessed bitmap region.
        /// </summary>
        public int Width => bmpdata.Width;

        /// <summary>
        /// Gets the height of the accessed bitmap region.
        /// </summary>
        public int Height => bmpdata.Height;

        /// <inheritdoc/>
        uint IImageAccessor<uint>.this[int x, int y]
        {
            get { return uintdata[y * bmpdata.Width + x]; }
            set { uintdata[y * bmpdata.Width + x] = value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BitmapArgb32Accessor"/> class and
        /// locks the specified bitmap region.
        /// </summary>
        /// <param name="bitmap">Bitmap providing the pixel data.</param>
        /// <param name="region">Optional region to lock; when omitted the entire bitmap is used.</param>
        public BitmapArgb32Accessor(Bitmap bitmap, Rectangle? region = null)
        {
            this.bitmap = bitmap;
            region = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            this.bmpdata = bitmap.LockBits(region.Value, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            this.totalBytes = bmpdata.Stride * bmpdata.Height;

            this.uintdata = (uint*)(void*)bmpdata.Scan0;
        }

        /// <summary>
        /// Gets or sets the color at the specified pixel coordinates.
        /// </summary>
        /// <param name="x">Horizontal pixel coordinate.</param>
        /// <param name="y">Vertical pixel coordinate.</param>
        public ColorArgb32 this[int x, int y]
        {
            get { return new ColorArgb32(uintdata[y * bmpdata.Width + x]); }
            set { uintdata[y * bmpdata.Width + x] = value.Value; }
        }

        /// <summary>
        /// Copies the raw pixel data to an array of unsigned integers.
        /// </summary>
        /// <returns>Array containing the raw pixel values.</returns>
        public uint[] CopyToArray()
        {
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
        public ColorArgb32[,] CopyToColorArray()
        {
            ColorArgb32[,] copy = new ColorArgb32[Width, Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    copy[x, y] = this[x, y];
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
        public void ApplySprite(Point location, BitmapArgb32Accessor sprite, Func<ColorArgb32, ColorArgb32, ColorArgb32> blend)
        {
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
            if (bitmap is not null && bmpdata is not null)
            {
                this.bitmap.UnlockBits(bmpdata);
                this.bitmap = null;
                this.uintdata = null;
                this.bmpdata = null;
            }
        }

    }
}
