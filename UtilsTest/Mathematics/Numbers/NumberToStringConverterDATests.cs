using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterDATests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("DA");
            (long n, string expected)[] cases =
            [
                (0,   "nul"),
                (1,   "en"),
                (2,   "to"),
                (3,   "tre"),
                (10,  "ti"),
                (11,  "elleve"),
                (12,  "tolv"),
                (19,  "nitten"),
                (20,  "tyve"),
                (21,  "en og tyve"),
                (30,  "tredive"),
                (50,  "halvtreds"),
                (99,  "ni og halvfems"),
                (100, "hundrede"),
                (101, "hundrede og en"),
                (200, "to hundrede"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"DA {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("DA");
            // Replacement drops "en" before "tusind" for 1 000
            Assert.AreEqual("tusind", c.Convert(1_000));
            Assert.AreEqual("to tusind", c.Convert(2_000));
        }

        [TestMethod]
        public void Cardinals_LongScale()
        {
            var c = NumberToStringConverter.GetConverter("DA");
            Assert.AreEqual("en million", c.Convert(1_000_000));
            Assert.AreEqual("to millioner", c.Convert(2_000_000));
            Assert.AreEqual("en milliard", c.Convert(1_000_000_000));
            Assert.AreEqual("to milliarder", c.Convert(2_000_000_000L));
            Assert.AreEqual("en billion", c.Convert(1_000_000_000_000L));
            Assert.AreEqual("en billiard", c.Convert(1_000_000_000_000_000L));
        }

        [TestMethod]
        public void DA_RegisteredUnderDADK()
        {
            Assert.AreEqual("to", NumberToStringConverter.GetConverter("DA-DK").Convert(2));
        }
    }
}
