using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using Utils.Imaging;

namespace UtilsTest.Imaging
{

    [TestClass]
    public class MatrixOperationTests
    {
        [TestMethod]
        public void AverageKernel()
        {
            var image = new ArrayImageAccessor<ColorArgb32, byte>(3, 3);
            byte val = 0;
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    image[x, y] = new ColorArgb32(255, val, val, val);
                    val++;
                }
            }

            double[,] matrix = ConvolutionMatrixFactory.Blur(2);
            var transformer = new MatrixImageTransformer<ColorArgb32, byte>(
                matrix,
                new Point(-1, -1),
                static (a, r, g, b) => new ColorArgb32(a, r, g, b));
            transformer.Transform(image);

            ColorArgb32 result = image[1, 1];
            Assert.AreEqual((byte)2, result.Red);
            Assert.AreEqual((byte)2, result.Green);
            Assert.AreEqual((byte)2, result.Blue);
        }

        [TestMethod]
        public void AverageKernelWithMask()
        {
            var image = new ArrayImageAccessor<ColorArgb32, byte>(3, 3);
            byte val = 0;
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    image[x, y] = new ColorArgb32(255, val, val, val);
                    val++;
                }
            }

            var mask = new ArrayImageAccessor<ColorArgb32, byte>(3, 3);
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    mask[x, y] = new ColorArgb32(128, 128, 128, 128);
                }
            }

            double[,] matrix = ConvolutionMatrixFactory.Blur(2);
            var transformer = new MatrixImageTransformer<ColorArgb32, byte>(
                matrix,
                new Point(-1, -1),
                static (a, r, g, b) => new ColorArgb32(a, r, g, b));
            transformer.Transform(image, mask);

            ColorArgb32 result = image[1, 1];
            Assert.AreEqual((byte)3, result.Red);
            Assert.AreEqual((byte)3, result.Green);
            Assert.AreEqual((byte)3, result.Blue);
        }
    }
}
