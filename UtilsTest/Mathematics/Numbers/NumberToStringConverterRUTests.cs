using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterRUTests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "один запятая пять"),
                (12.34m, "двенадцать запятая три четыре"),
            ];

            var converter = NumberToStringConverter.GetConverter("RU");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }
    }
}
