using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class MatrixImageTransformerTests
{
    // Minimal in-memory accessor for testing.
    private sealed class ArrayAccessor<A> : IImageAccessor<A, double>
        where A : struct, IColorArgb<double>
    {
        private readonly A[] _data;
        public int Width { get; }
        public int Height { get; }

        public ArrayAccessor(int width, int height)
        {
            Width = width;
            Height = height;
            _data = new A[width * height];
        }

        public A this[int x, int y]
        {
            get => _data[y * Width + x];
            set => _data[y * Width + x] = value;
        }

        public A this[Point p]
        {
            get => this[p.X, p.Y];
            set => this[p.X, p.Y] = value;
        }
    }

    [TestMethod]
    public void BlurKernel_AveragesNeighbors()
    {
        // 3×3 image: all pixels white except center which is black.
        var image = new ArrayAccessor<ColorArgb>(3, 3);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                image[x, y] = new ColorArgb(1, 1, 1, 1);
        image[1, 1] = new ColorArgb(1, 0, 0, 0);

        double[,] blur = ConvolutionMatrixFactory.Blur(3);
        image.ApplyWeightedMatrix(blur, new Point(-1, -1));

        // Center pixel: 8 white + 1 black = average 8/9 ≈ 0.889
        Assert.AreEqual(8.0 / 9.0, image[1, 1].Red, 1e-9);
    }

    [TestMethod]
    public void UniformImage_UnchangedByBlur()
    {
        var image = new ArrayAccessor<ColorArgb>(4, 4);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                image[x, y] = new ColorArgb(1, 0.5, 0.3, 0.7);

        double[,] blur = ConvolutionMatrixFactory.Blur(3);
        image.ApplyWeightedMatrix(blur, new Point(-1, -1));

        // A uniform image blurred with any normalized kernel stays uniform.
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                Assert.AreEqual(0.5, image[x, y].Red,   1e-9, $"pixel ({x},{y}) red");
                Assert.AreEqual(0.3, image[x, y].Green, 1e-9, $"pixel ({x},{y}) green");
                Assert.AreEqual(0.7, image[x, y].Blue,  1e-9, $"pixel ({x},{y}) blue");
            }
    }
}
