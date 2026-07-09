using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterZHTests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "一 点 五"),
                (12.34m, "十二 点 三 四"),
            ];

            var converter = NumberToStringConverter.GetConverter("ZH");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("ZH");
            (long n, string expected)[] cases =
            [
                (1,   "一"),
                (10,  "十"),
                (20,  "二十"),
                (100, "一百"),
                // item 32 evaluated "一千" for a possible elision fix (as done for FI/KO/JA) but
                // standard Mandarin requires "一" before both 百 and 千 — left unchanged.
                (1_000, "一 千"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"ZH {n}");
        }

        [TestMethod]
        public void Ordinals_PrefixDi()
        {
            var c = NumberToStringConverter.GetConverter("ZH");

            Assert.AreEqual("第一",  c.ConvertOrdinal(1));
            Assert.AreEqual("第二",  c.ConvertOrdinal(2));
            Assert.AreEqual("第十",  c.ConvertOrdinal(10));
            Assert.AreEqual("第一百",  c.ConvertOrdinal(100));
        }
    }
}
