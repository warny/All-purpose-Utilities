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
            A[,] copy = new A[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    copy[x, y] = accessor[x, y];
                }
            }

            int mw = weights.GetLength(0);
            int mh = weights.GetLength(1);
            double max = ComponentMax();

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
                            A c = copy[sx, sy];
                            sumA += w * Convert.ToDouble(c.Alpha);
                            sumR += w * Convert.ToDouble(c.Red);
                            sumG += w * Convert.ToDouble(c.Green);
                            sumB += w * Convert.ToDouble(c.Blue);
                            sumW += w;
                        }
                    }

                    if (sumW > 0)
                    {
                        A newColor = creator(
                            (T)Convert.ChangeType(sumA / sumW, typeof(T)),
                            (T)Convert.ChangeType(sumR / sumW, typeof(T)),
                            (T)Convert.ChangeType(sumG / sumW, typeof(T)),
                            (T)Convert.ChangeType(sumB / sumW, typeof(T)));

                        if (mask is null)
                        {
                            accessor[x, y] = newColor;
                        }
                        else
                        {
                            A orig = accessor[x, y];
                            A maskColor = mask[x, y];
                            double wa = Convert.ToDouble(maskColor.Alpha) / max;
                            double wr = Convert.ToDouble(maskColor.Red) / max;
                            double wg = Convert.ToDouble(maskColor.Green) / max;
                            double wb = Convert.ToDouble(maskColor.Blue) / max;

                            double fa = Convert.ToDouble(orig.Alpha) * (1 - wa) + Convert.ToDouble(newColor.Alpha) * wa;
                            double fr = Convert.ToDouble(orig.Red) * (1 - wr) + Convert.ToDouble(newColor.Red) * wr;
                            double fg = Convert.ToDouble(orig.Green) * (1 - wg) + Convert.ToDouble(newColor.Green) * wg;
                            double fb = Convert.ToDouble(orig.Blue) * (1 - wb) + Convert.ToDouble(newColor.Blue) * wb;

                            accessor[x, y] = creator(
                                (T)Convert.ChangeType(fa, typeof(T)),
                                (T)Convert.ChangeType(fr, typeof(T)),
                                (T)Convert.ChangeType(fg, typeof(T)),
                                (T)Convert.ChangeType(fb, typeof(T)));
                        }
                    }
                }
            }
        }

        private static double ComponentMax()
        {
            Type t = typeof(T);
            if (t == typeof(byte)) return byte.MaxValue;
            if (t == typeof(ushort)) return ushort.MaxValue;
            return 1.0;
        }
    }
}
