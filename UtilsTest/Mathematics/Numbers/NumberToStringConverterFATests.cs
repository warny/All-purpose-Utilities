using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterFATests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("FA");
            (long n, string expected)[] cases =
            [
                (0,   "صفر"),
                (1,   "یک"),
                (2,   "دو"),
                (3,   "سه"),
                (9,   "نه"),
                (10,  "ده"),
                (11,  "یازده"),
                (12,  "دوازده"),
                (19,  "نوزده"),
                (20,  "بیست"),
                (21,  "بیست و یک"),
                (30,  "سی"),
                (100, "صد"),
                (101, "صد و یک"),
                (200, "دویست"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"FA {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("FA");
            // 1 000 = "هزار" (replacement drops "یک هزار" → "هزار")
            Assert.AreEqual("هزار",        c.Convert(1_000));
            Assert.AreEqual("دو هزار",     c.Convert(2_000));
            Assert.AreEqual("ده هزار",     c.Convert(10_000));
            Assert.AreEqual("یک میلیون",   c.Convert(1_000_000));
        }

        [TestMethod]
        public void Negative()
        {
            var c = NumberToStringConverter.GetConverter("FA");
            Assert.AreEqual("منفی یک", c.Convert(-1L));
            Assert.AreEqual("منفی ده", c.Convert(-10L));
        }

        [TestMethod]
        public void FA_RegisteredUnderFAIR()
        {
            Assert.AreEqual("دو", NumberToStringConverter.GetConverter("FA-IR").Convert(2));
        }
    }
}
