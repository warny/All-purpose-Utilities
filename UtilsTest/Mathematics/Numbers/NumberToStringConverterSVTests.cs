using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterSVTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("SV");
            (long n, string expected)[] cases =
            [
                (0,  "noll"),
                (1,  "ett"),
                (2,  "två"),
                (9,  "nio"),
                (10, "tio"),
                (11, "elva"),
                (19, "nitton"),
                (20, "tjugo"),
                (21, "tjugoett"),   // 20s fuse without space
                (31, "trettio ett"),
                (100, "ett hundra"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"SV {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("SV");
            // "ett tusen" is elided to "tusen"
            Assert.AreEqual("tusen",     c.Convert(1_000));
            Assert.AreEqual("två tusen", c.Convert(2_000));
        }

        [TestMethod]
        public void Cardinals_LongScale()
        {
            var c = NumberToStringConverter.GetConverter("SV");
            Assert.AreEqual("ett miljon",     c.Convert(1_000_000));
            Assert.AreEqual("två miljoner",   c.Convert(2_000_000));
            Assert.AreEqual("ett miljard",    c.Convert(1_000_000_000));
            Assert.AreEqual("tre miljarder",  c.Convert(3_000_000_000L));
            Assert.AreEqual("ett biljon",     c.Convert(1_000_000_000_000L));
            Assert.AreEqual("ett biljard",    c.Convert(1_000_000_000_000_000L));
        }

        [TestMethod]
        public void SupportsOrdinals_False()
        {
            var c = NumberToStringConverter.GetConverter("SV");
            Assert.IsFalse(c.SupportsOrdinals);
        }

        [TestMethod]
        public void SV_RegisteredUnderSVAndSVSE()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("SV"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("SV-SE"));
        }
    }
}
