using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterIDTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("ID");
            (long n, string expected)[] cases =
            [
                (0,   "nol"),
                (1,   "satu"),
                (2,   "dua"),
                (3,   "tiga"),
                (9,   "sembilan"),
                (10,  "sepuluh"),
                (11,  "sebelas"),
                (12,  "dua belas"),
                (19,  "sembilan belas"),
                (20,  "dua puluh"),
                (21,  "dua puluh satu"),
                (100, "seratus"),
                (101, "seratus satu"),
                (200, "dua ratus"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"ID {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("ID");
            // 1 000 = "seribu" (replacement: "satu ribu" → "seribu")
            Assert.AreEqual("seribu",     c.Convert(1_000));
            Assert.AreEqual("dua ribu",   c.Convert(2_000));
            Assert.AreEqual("sepuluh ribu", c.Convert(10_000));
            Assert.AreEqual("satu juta",   c.Convert(1_000_000));
        }

        [TestMethod]
        public void Negative()
        {
            var c = NumberToStringConverter.GetConverter("ID");
            Assert.AreEqual("negatif satu", c.Convert(-1L));
        }

        [TestMethod]
        public void ID_RegisteredUnderIDID()
        {
            Assert.AreEqual("dua", NumberToStringConverter.GetConverter("ID-ID").Convert(2));
        }
    }
}
