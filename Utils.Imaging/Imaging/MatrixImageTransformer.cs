using System;
using System.Drawing;
using System.Numerics;

namespace Utils.Imaging
{
    /// <summary>
    /// Applies a weighted matrix transformation to an image.
    /// </summary>
    /// <typeparam name="A">Color type.</typeparam>
    /// <typeparam name="T">Component type.</typeparam>
    public class MatrixImageTransformer<A, T> : IImageTransformer<A, T>
        where A : struct, IColorArgb<T>
        where T : struct, IConvertible, INumber<T>
    {
        private readonly double[,] weights;
        private readonly Point offset;
        private readonly Func<T, T, T, T, A> creator;
        private static readonly double _componentMax = ComponentMax();
        // true when T is an integer type; used to apply rounding before truncation.
        private static readonly bool _isIntegral = T.CreateChecked(0.5) == T.Zero;

        /// <summary>
        /// Initializes a new instance of the <see cref="MatrixImageTransformer{A, T}"/> class.
        /// </summary>
        /// <param name="weights">Weight matrix.</param>
        /// <param name="offset">Offset of the matrix relative to the processed pixel.</param>
        /// <param name="creator">Factory used to create color values.</param>
        public MatrixImageTransformer(double[,] weights, Point offset, Func<T, T, T, T, A> creator)
        {
            this.weights = weights ?? throw new ArgumentNullException(nameof(weights));
            this.offset = offset;
            this.creator = creator ?? throw new ArgumentNullException(nameof(creator));
        }

        /// <inheritdoc/>
        public void Transform(IImageAccessor<A, T> accessor)
        {
            Transform(accessor, null);
        }

        /// <inheritdoc/>
        public void Transform(IImageAccessor<A, T> accessor, IImageAccessor<A, T> mask)
        {
            if (accessor is null) throw new ArgumentNullException(nameof(accessor));
            int width = accessor.Width;
            int height = accessor.Height;
            A[] copy = new A[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    copy[y * width + x] = accessor[x, y];

            int mw = weights.GetLength(0);
            int mh = weights.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double sumA = 0, sumR = 0, sumG = 0, sumB = 0, sumW = 0;

                    for (int j = 0; j < mh; j++)
                    {
                        int sy = y + j + offset.Y;
                        if (sy < 0 || sy >= height) continue;
                        for (int i = 0; i < mw; i++)
                        {
                            int sx = x + i + offset.X;
                            if (sx < 0 || sx >= width) continue;
                            double w = weights[i, j];
                            if (w == 0) continue;
                            A c = copy[sy * width + sx];
                            sumA += w * double.CreateChecked(c.Alpha);
                            sumR += w * double.CreateChecked(c.Red);
                            sumG += w * double.CreateChecked(c.Green);
                            sumB += w * double.CreateChecked(c.Blue);
                            sumW += w;
                        }
                    }

                    if (sumW > 0)
                    {
                        A newColor = creator(
                            ToComponent(sumA / sumW),
                            ToComponent(sumR / sumW),
                            ToComponent(sumG / sumW),
                            ToComponent(sumB / sumW));

                        if (mask is null)
                        {
                            accessor[x, y] = newColor;
                        }
                        else
                        {
                            A orig = accessor[x, y];
                            A maskColor = mask[x, y];
                            double wa = double.CreateChecked(maskColor.Alpha) / _componentMax;
                            double wr = double.CreateChecked(maskColor.Red) / _componentMax;
                            double wg = double.CreateChecked(maskColor.Green) / _componentMax;
                            double wb = double.CreateChecked(maskColor.Blue) / _componentMax;

                            double fa = double.CreateChecked(orig.Alpha) * (1 - wa) + double.CreateChecked(newColor.Alpha) * wa;
                            double fr = double.CreateChecked(orig.Red) * (1 - wr) + double.CreateChecked(newColor.Red) * wr;
                            double fg = double.CreateChecked(orig.Green) * (1 - wg) + double.CreateChecked(newColor.Green) * wg;
                            double fb = double.CreateChecked(orig.Blue) * (1 - wb) + double.CreateChecked(newColor.Blue) * wb;

                            accessor[x, y] = creator(
                                ToComponent(fa),
                                ToComponent(fr),
                                ToComponent(fg),
                                ToComponent(fb));
                        }
                    }
                }
            }
        }

        // Converts a double accumulator back to T, rounding for integer types.
        private static T ToComponent(double value) =>
            _isIntegral ? T.CreateChecked(Math.Round(value)) : T.CreateChecked(value);

        private static double ComponentMax()
        {
            Type t = typeof(T);
            if (t == typeof(byte)) return byte.MaxValue;
            if (t == typeof(ushort)) return ushort.MaxValue;
            return 1.0;
        }
    }
}
