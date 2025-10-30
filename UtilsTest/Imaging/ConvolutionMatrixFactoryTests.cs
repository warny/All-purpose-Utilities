using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class ConvolutionMatrixFactoryTests
{
    [TestMethod]
    public void BlurMatrixIsNormalized()
    {
        double[,] m = ConvolutionMatrixFactory.Blur(3);
        double sum = 0;
        foreach (double v in m) sum += v;
        Assert.AreEqual(1.0, sum, 1e-6);
        Assert.AreEqual(1.0 / 9, m[0, 0], 1e-6);
    }

    [TestMethod]
    public void SharpenMatrixCenter()
    {
        double[,] m = ConvolutionMatrixFactory.Sharpen();
        Assert.AreEqual(5, m[1, 1]);
        Assert.AreEqual(-1, m[0, 1]);
    }
}
