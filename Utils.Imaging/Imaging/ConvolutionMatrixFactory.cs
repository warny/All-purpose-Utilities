using System;

namespace Utils.Imaging
{
    /// <summary>
    /// Provides common convolution kernels for image processing.
    /// </summary>
    public static class ConvolutionMatrixFactory
    {
        /// <summary>Maximum allowed kernel side length for the Blur factory.</summary>
        private const int MaxKernelSize = 1024;

        /// <summary>
        /// Creates a simple averaging blur kernel of the specified size.
        /// </summary>
        /// <param name="size">Side size of the square kernel (must be in [1, 1024]).</param>
        /// <returns>A normalized blur kernel.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="size"/> is less than 1 or greater than <see cref="MaxKernelSize"/>.
        /// </exception>
        public static double[,] Blur(int size)
        {
            if (size <= 0)         throw new ArgumentOutOfRangeException(nameof(size), "size must be at least 1.");
            if (size > MaxKernelSize) throw new ArgumentOutOfRangeException(nameof(size), $"size must not exceed {MaxKernelSize}.");
            double[,] result = new double[size, size];
            // Use double arithmetic to avoid integer overflow for large (but valid) sizes.
            double weight = 1.0 / ((double)size * size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    result[x, y] = weight;
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
