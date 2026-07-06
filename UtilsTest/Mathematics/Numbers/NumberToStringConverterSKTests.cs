using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterSKTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("SK");
            (long n, string expected)[] cases =
            [
                (0,   "nula"),
                (1,   "jeden"),
                (2,   "dva"),
                (3,   "tri"),
                (9,   "deväť"),
                (10,  "desať"),
                (11,  "jedenásť"),
                (12,  "dvanásť"),
                (19,  "devätnásť"),
                (20,  "dvadsať"),
                (21,  "dvadsať jeden"),
                (100, "sto"),
                (101, "sto jeden"),
                (200, "dvesto"),
                (300, "tristo"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"SK {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("SK");
            // Replacement drops "jeden" before "tisíc" for 1 000
            Assert.AreEqual("tisíc",      c.Convert(1_000));
            Assert.AreEqual("dva tisíc",  c.Convert(2_000));
            Assert.AreEqual("desať tisíc", c.Convert(10_000));
        }

        [TestMethod]
        public void Negative()
        {
            var c = NumberToStringConverter.GetConverter("SK");
            Assert.AreEqual("mínus jeden", c.Convert(-1L));
        }

        [TestMethod]
        public void SK_RegisteredUnderSKSK()
        {
            Assert.AreEqual("dva", NumberToStringConverter.GetConverter("SK-SK").Convert(2));
        }
    }
}
