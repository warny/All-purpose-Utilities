using System;
using System.Drawing;
using System.Numerics;

namespace Utils.Imaging
{
    /// <summary>
    /// Provides helper methods for <see cref="IImageAccessor{T}"/> instances.
    /// </summary>
    public static class ImageAccessorExtensions
    {
        /// <summary>
        /// Applies a sprite onto the destination image using a blending function.
        /// </summary>
        /// <param name="destination">The destination image.</param>
        /// <param name="sprite">The sprite to apply.</param>
        /// <param name="location">The top-left location where the sprite will be drawn.</param>
        /// <param name="blend">Blending function combining sprite and destination colors.</param>
        /// <typeparam name="A">Color type.</typeparam>
        /// <typeparam name="T">Component type.</typeparam>
        public static void ApplySprite<A, T>(this IImageAccessor<A, T> destination,
                                             IImageAccessor<A, T> sprite,
                                             Point location,
                                             Func<A, A, A> blend)
            where A : struct, IColorArgb<T>
            where T : struct, INumber<T>
        {
            for (int y = 0; y < sprite.Height; y++)
            {
                int dy = location.Y + y;
                if (dy < 0 || dy >= destination.Height) continue;

                for (int x = 0; x < sprite.Width; x++)
                {
                    int dx = location.X + x;
                    if (dx < 0 || dx >= destination.Width) continue;

                    A srcColor = sprite[x, y];
                    A dstColor = destination[dx, dy];
                    destination[dx, dy] = blend(srcColor, dstColor);
                }
            }
        }

        /// <summary>
        /// Applies a weighted matrix to each pixel of the image.
        /// </summary>
        /// <param name="image">Image to transform.</param>
        /// <param name="weights">Matrix of weights.</param>
        /// <param name="offset">Offset of the matrix relative to the processed pixel.</param>
        /// <param name="creator">Factory creating a color instance from components.</param>
        /// <typeparam name="A">Color type.</typeparam>
        /// <typeparam name="T">Component type.</typeparam>
        public static void ApplyWeightedMatrix<A, T>(this IImageAccessor<A, T> image,
                                                     double[,] weights,
                                                     Point offset,
                                                     Func<T, T, T, T, A> creator)
            where A : struct, IColorArgb<T>
            where T : struct, IConvertible, INumber<T>
        {
            var transformer = new MatrixImageTransformer<A, T>(weights, offset, creator);
            transformer.Transform(image);
        }

        /// <summary>
        /// Applies a weighted matrix on a <see cref="ColorArgb"/> image.
        /// </summary>
        public static void ApplyWeightedMatrix(this IImageAccessor<ColorArgb, double> image,
                                               double[,] weights,
                                               Point offset)
        {
            ApplyWeightedMatrix<ColorArgb, double>(image, weights, offset,
                static (a, r, g, b) => new ColorArgb(a, r, g, b));
        }

        /// <summary>
        /// Applies a weighted matrix on a <see cref="ColorArgb32"/> image.
        /// </summary>
        public static void ApplyWeightedMatrix(this IImageAccessor<ColorArgb32, byte> image,
                                               double[,] weights,
                                               Point offset)
        {
            ApplyWeightedMatrix<ColorArgb32, byte>(image, weights, offset,
                static (a, r, g, b) => new ColorArgb32(a, r, g, b));
        }

        /// <summary>
        /// Applies a weighted matrix on a <see cref="ColorArgb64"/> image.
        /// </summary>
        public static void ApplyWeightedMatrix(this IImageAccessor<ColorArgb64, ushort> image,
                                               double[,] weights,
                                               Point offset)
        {
            ApplyWeightedMatrix<ColorArgb64, ushort>(image, weights, offset,
                static (a, r, g, b) => new ColorArgb64(a, r, g, b));
        }
    }
}
