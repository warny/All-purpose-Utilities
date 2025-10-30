using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterGLTests
    {
        [TestMethod]
        public void GalicianCardinals()
        {
            (int Number, string Expected)[] tests = [
                (21, "vinte e un"),
                (105, "cento cinco"),
                (100, "cen"),
            ];

            var converter = NumberToStringConverter.GetConverter("GL");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void GalicianDecimal()
        {
            var converter = NumberToStringConverter.GetConverter("gl-ES");
            Assert.AreEqual("un coma cinco", converter.Convert(1.5m));
        }
    }
}
