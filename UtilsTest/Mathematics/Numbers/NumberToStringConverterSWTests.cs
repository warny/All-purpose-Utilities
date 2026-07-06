using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterSWTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("SW");
            (long n, string expected)[] cases =
            [
                (0,   "sifuri"),
                (1,   "moja"),
                (2,   "mbili"),
                (3,   "tatu"),
                (9,   "tisa"),
                (10,  "kumi"),
                (11,  "kumi na moja"),
                (12,  "kumi na mbili"),
                (19,  "kumi na tisa"),
                (20,  "ishirini"),
                (21,  "ishirini na moja"),
                (100, "mia moja"),
                (101, "mia moja na moja"),
                (200, "mia mbili"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"SW {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("SW");
            // 1 000 = "elfu" (replacement: "moja elfu" → "elfu")
            Assert.AreEqual("elfu",       c.Convert(1_000));
            Assert.AreEqual("mbili elfu", c.Convert(2_000));
            Assert.AreEqual("kumi elfu",  c.Convert(10_000));
        }

        [TestMethod]
        public void Negative()
        {
            var c = NumberToStringConverter.GetConverter("SW");
            Assert.AreEqual("hasi moja", c.Convert(-1L));
        }

        [TestMethod]
        public void SW_RegisteredUnderSWKE()
        {
            Assert.AreEqual("mbili", NumberToStringConverter.GetConverter("SW-KE").Convert(2));
        }

        [TestMethod]
        public void SW_RegisteredUnderSWTZ()
        {
            Assert.AreEqual("mbili", NumberToStringConverter.GetConverter("SW-TZ").Convert(2));
        }
    }
}
