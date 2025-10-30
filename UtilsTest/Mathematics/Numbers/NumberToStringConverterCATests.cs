using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterCATests
    {
        [TestMethod]
        public void CatalanCardinals()
        {
            (int Number, string Expected)[] tests = [
                (21, "vint-i-un"),
                (105, "cent cinc"),
                (321, "tres-cents vint-i-un"),
            ];

            var converter = NumberToStringConverter.GetConverter("CA");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void CatalanDecimal()
        {
            var converter = NumberToStringConverter.GetConverter("ca-ES");
            Assert.AreEqual("un coma cinc", converter.Convert(1.5m));
        }
    }
}
