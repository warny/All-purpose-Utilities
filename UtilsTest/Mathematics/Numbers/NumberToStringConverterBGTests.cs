using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterBGTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("BG");
            (long n, string expected)[] cases =
            [
                (0,   "нула"),
                (1,   "едно"),
                (2,   "две"),
                (3,   "три"),
                (9,   "девет"),
                (10,  "десет"),
                (11,  "единадесет"),
                (12,  "дванадесет"),
                (19,  "деветнадесет"),
                (20,  "двадесет"),
                (21,  "двадесет едно"),
                (100, "сто"),
                (101, "сто едно"),
                (200, "двеста"),
                (999, "деветстотин деветдесет девет"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"BG {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("BG");
            // Replacement drops "едно" before "хиляда" for 1 000
            Assert.AreEqual("хиляда",       c.Convert(1_000));
            Assert.AreEqual("две хиляда",   c.Convert(2_000));
            Assert.AreEqual("десет хиляда", c.Convert(10_000));
        }

        [TestMethod]
        public void BG_RegisteredUnderBGBG()
        {
            var c = NumberToStringConverter.GetConverter("BG-BG");
            Assert.AreEqual("едно", c.Convert(1));
        }
    }
}
