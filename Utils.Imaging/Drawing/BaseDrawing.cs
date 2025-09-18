using System;
using System.Collections.Generic;
using System.Text;
using Utils.Imaging;

namespace Utils.Drawing
{
        /// <summary>
        /// Provides a base implementation that gives access to an image accessor
        /// for derived drawing helpers.
        /// </summary>
        /// <typeparam name="T">Type of the pixel data exposed by the accessor.</typeparam>
        public class BaseDrawing<T>
        {
                /// <summary>
                /// Gets the image accessor used to read and write pixel information.
                /// </summary>
                public IImageAccessor<T> ImageAccessor { get; }

                /// <summary>
                /// Initializes a new instance of the <see cref="BaseDrawing{T}"/> class.
                /// </summary>
                /// <param name="imageAccessor">Accessor that exposes the underlying image.</param>
                public BaseDrawing(IImageAccessor<T> imageAccessor)
                {
                        ImageAccessor = imageAccessor;
                }

        }
}
