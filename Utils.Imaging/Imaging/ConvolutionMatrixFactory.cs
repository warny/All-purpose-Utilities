using System;

namespace Utils.Imaging
{
    /// <summary>
    /// Provides common convolution kernels for image processing.
    /// </summary>
    public static class ConvolutionMatrixFactory
    {
        /// <summary>
        /// Creates a simple averaging blur kernel of the specified size.
        /// </summary>
        /// <param name="size">Side size of the square kernel.</param>
        /// <returns>A normalized blur kernel.</returns>
        public static double[,] Blur(int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            double[,] result = new double[size, size];
            double value = 1.0 / (size * size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    result[x, y] = value;
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a basic sharpening kernel.
        /// </summary>
        public static double[,] Sharpen()
        {
            return new double[,] {
                { 0, -1, 0 },
                { -1, 5, -1 },
                { 0, -1, 0 }
            };
        }

        /// <summary>
        /// Returns a simple edge detection kernel.
        /// </summary>
        public static double[,] EdgeDetection()
        {
            return new double[,] {
                { -1, -1, -1 },
                { -1, 8, -1 },
                { -1, -1, -1 }
            };
        }
    }
}
