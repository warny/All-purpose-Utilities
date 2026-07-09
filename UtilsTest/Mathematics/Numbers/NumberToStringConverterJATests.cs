using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterJATests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "一 点 五"),
                (12.34m, "十二 点 三 四"),
            ];

            var converter = NumberToStringConverter.GetConverter("JA");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("JA");
            // item 32: "1 000" is "千", not "一 千" (unlike Chinese, which keeps "一千")
            Assert.AreEqual("千",    c.Convert(1_000));
            Assert.AreEqual("二 千", c.Convert(2_000));
        }

        [TestMethod]
        public void Cardinals_Hundred_NoLeadingOne()
        {
            var c = NumberToStringConverter.GetConverter("JA");
            // Already correct before item 32: "百", not "一百"
            Assert.AreEqual("百", c.Convert(100));
        }
    }
}
