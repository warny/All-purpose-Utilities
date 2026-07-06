using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterCSTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("CS");
            (long n, string expected)[] cases =
            [
                (0,   "nula"),
                (1,   "jedna"),
                (2,   "dva"),
                (3,   "tři"),
                (9,   "devět"),
                (10,  "deset"),
                (11,  "jedenáct"),
                (12,  "dvanáct"),
                (19,  "devatenáct"),
                (20,  "dvacet"),
                (21,  "dvacet jedna"),
                (100, "sto"),
                (200, "dvě stě"),
                (300, "tři sta"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"CS {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("CS");
            Assert.AreEqual("tisíc",      c.Convert(1_000));
            Assert.AreEqual("dva tisíc",  c.Convert(2_000));
            Assert.AreEqual("deset tisíc", c.Convert(10_000));
        }

        [TestMethod]
        public void CS_RegisteredUnderCSCZ()
        {
            Assert.AreEqual("dva", NumberToStringConverter.GetConverter("CS-CZ").Convert(2));
        }
    }
}
