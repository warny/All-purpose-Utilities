using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterNOTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("NO");
            (long n, string expected)[] cases =
            [
                (0,  "null"),
                (1,  "en"),
                (2,  "to"),
                (9,  "ni"),
                (10, "ti"),
                (11, "elleve"),
                (19, "nitten"),
                (20, "tjue"),
                (21, "tjue en"),
                (100, "ett hundre"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"NO {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("NO");
            // "en tusen" is elided to "tusen"
            Assert.AreEqual("tusen",     c.Convert(1_000));
            Assert.AreEqual("to tusen",  c.Convert(2_000));
        }

        [TestMethod]
        public void Cardinals_LongScale()
        {
            var c = NumberToStringConverter.GetConverter("NO");
            Assert.AreEqual("en million",     c.Convert(1_000_000));
            Assert.AreEqual("to millioner",   c.Convert(2_000_000));
            Assert.AreEqual("en milliard",    c.Convert(1_000_000_000));
            Assert.AreEqual("tre milliarder", c.Convert(3_000_000_000L));
            Assert.AreEqual("en billion",     c.Convert(1_000_000_000_000L));
            Assert.AreEqual("en billiard",    c.Convert(1_000_000_000_000_000L));
        }

        [TestMethod]
        public void SupportsOrdinals_False()
        {
            var c = NumberToStringConverter.GetConverter("NO");
            Assert.IsFalse(c.SupportsOrdinals);
        }

        [TestMethod]
        public void NO_RegisteredUnderNOAndNBAndNBNO()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("NO"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("NB"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("NB-NO"));
        }
    }
}
