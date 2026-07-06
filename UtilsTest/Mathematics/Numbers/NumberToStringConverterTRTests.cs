using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterTRTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("TR");
            (long n, string expected)[] cases =
            [
                (0,   "sıfır"),
                (1,   "bir"),
                (2,   "iki"),
                (3,   "üç"),
                (9,   "dokuz"),
                (10,  "on"),
                (11,  "on bir"),
                (12,  "on iki"),
                (19,  "on dokuz"),
                (20,  "yirmi"),
                (21,  "yirmi bir"),
                (100, "yüz"),
                (101, "yüz bir"),
                (200, "iki yüz"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"TR {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("TR");
            // 1 000 = "bin" (replacement "bir bin" → "bin")
            Assert.AreEqual("bin",      c.Convert(1_000));
            Assert.AreEqual("iki bin",  c.Convert(2_000));
            Assert.AreEqual("on bin",   c.Convert(10_000));
            Assert.AreEqual("bir milyon", c.Convert(1_000_000));
        }

        [TestMethod]
        public void Negative()
        {
            var c = NumberToStringConverter.GetConverter("TR");
            Assert.AreEqual("eksi bir", c.Convert(-1L));
        }

        [TestMethod]
        public void TR_RegisteredUnderTRTR()
        {
            Assert.AreEqual("iki", NumberToStringConverter.GetConverter("TR-TR").Convert(2));
        }
    }
}
