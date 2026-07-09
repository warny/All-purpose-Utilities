using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterHUTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("HU");
            (long n, string expected)[] cases =
            [
                (0,  "nulla"),
                (1,  "egy"),
                (2,  "kettő"),   // standalone exception; "két" is the combining form
                (3,  "három"),
                (9,  "kilenc"),
                (10, "tíz"),
                (11, "tizenegy"),
                (12, "tizenkét"),
                (20, "húsz"),
                (21, "huszonegy"),
                (30, "harminc"),
                (31, "harmincegy"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"HU {n}");
        }

        [TestMethod]
        public void Cardinals_Hundreds()
        {
            var c = NumberToStringConverter.GetConverter("HU");
            // "egyszáz" is elided to "száz"
            Assert.AreEqual("száz",             c.Convert(100));
            Assert.AreEqual("százegy",          c.Convert(101));
            Assert.AreEqual("kétszáz",          c.Convert(200));
            Assert.AreEqual("kétszázhuszonegy", c.Convert(221));
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("HU");
            Assert.AreEqual("ezer",      c.Convert(1_000));      // "egyezer" → "ezer"
            Assert.AreEqual("kétezer",   c.Convert(2_000));      // "kettő" → "két" before scale name
            Assert.AreEqual("tízezer",   c.Convert(10_000));
        }

        [TestMethod]
        public void Cardinals_LongScale()
        {
            var c = NumberToStringConverter.GetConverter("HU");
            Assert.AreEqual("millió",    c.Convert(1_000_000));      // "egymillió" → "millió"
            Assert.AreEqual("kétmillió", c.Convert(2_000_000));
            Assert.AreEqual("milliárd",  c.Convert(1_000_000_000L));
            // startIndex="2": scale 4 → billió
            Assert.AreEqual("billió",    c.Convert(1_000_000_000_000L));
        }

        [TestMethod]
        public void Ordinals_Exceptions()
        {
            var c = NumberToStringConverter.GetConverter("HU");
            Assert.IsTrue(c.SupportsOrdinals);
            Assert.AreEqual("első",     c.ConvertOrdinal(1));
            Assert.AreEqual("második",  c.ConvertOrdinal(2));
            Assert.AreEqual("harmadik", c.ConvertOrdinal(3));
            Assert.AreEqual("tizedik",  c.ConvertOrdinal(10));
            Assert.AreEqual("századik", c.ConvertOrdinal(100));
            Assert.AreEqual("ezredik",  c.ConvertOrdinal(1000));
        }

        [TestMethod]
        public void HU_RegisteredUnderHUAndHUHU()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("HU"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("HU-HU"));
        }
    }
}
