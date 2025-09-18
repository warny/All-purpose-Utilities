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
                private BitmapData bmpdata = null;
                private byte* bytedata;
                private readonly int totalBytes;

                /// <summary>
                /// Gets the width of the accessed bitmap region.
                /// </summary>
                public int Width => bmpdata.Width;

                /// <summary>
                /// Gets the height of the accessed bitmap region.
                /// </summary>
                public int Height => bmpdata.Height;

                /// <summary>
                /// Initializes a new instance of the <see cref="BitmapIndexed8Accessor"/> class and
                /// locks the specified bitmap region.
                /// </summary>
                /// <param name="bitmap">Bitmap providing the pixel data.</param>
                /// <param name="region">Optional region to lock; when omitted the entire bitmap is used.</param>
                public BitmapIndexed8Accessor( Bitmap bitmap, Rectangle? region = null )
                {
                        this.bitmap = bitmap;
                        region = region ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);

                        this.bmpdata = bitmap.LockBits(region.Value, ImageLockMode.ReadWrite, PixelFormat.Format8bppIndexed);
                        this.totalBytes = bmpdata.Stride * bmpdata.Height;

                        this.bytedata = (byte*)(void*)bmpdata.Scan0;
                }

                /// <summary>
                /// Gets or sets the indexed value at the specified pixel coordinates.
                /// </summary>
                /// <param name="x">Horizontal pixel coordinate.</param>
                /// <param name="y">Vertical pixel coordinate.</param>
                public byte this[int x, int y]
                {
                        get { return bytedata[y * bmpdata.Stride + x]; }
                        set { bytedata[y * bmpdata.Stride + x] = value; }
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
                        if (bitmap is not null && bmpdata is not null) {
                                this.bitmap.UnlockBits(bmpdata);
                                this.bitmap = null;
                                this.bytedata = null;
				this.bmpdata = null;
			}
		}

	}
}
