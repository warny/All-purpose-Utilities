using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterUKTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("UK");
            (long n, string expected)[] cases =
            [
                (0,   "нуль"),
                (1,   "один"),
                (2,   "два"),
                (9,   "дев'ять"),
                (10,  "десять"),
                (11,  "одинадцять"),
                (19,  "дев'ятнадцять"),
                (20,  "двадцять"),
                (21,  "двадцять один"),
                (100, "сто"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"UK {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("UK");
            // "один тисяч" is elided to "тисяч"; thousands word is invariant (accepted limitation)
            Assert.AreEqual("тисяч",      c.Convert(1_000));
            Assert.AreEqual("два тисяч",  c.Convert(2_000));
        }

        [TestMethod]
        public void Cardinals_LongScale()
        {
            var c = NumberToStringConverter.GetConverter("UK");
            Assert.AreEqual("один мільйон",  c.Convert(1_000_000));
            Assert.AreEqual("два мільйонів", c.Convert(2_000_000));
            Assert.AreEqual("один мільярд",  c.Convert(1_000_000_000));
            Assert.AreEqual("один більйон",  c.Convert(1_000_000_000_000L));
            Assert.AreEqual("один трильйон", c.Convert(1_000_000_000_000_000_000L));
        }

        [TestMethod]
        public void SupportsOrdinals_False()
        {
            var c = NumberToStringConverter.GetConverter("UK");
            Assert.IsFalse(c.SupportsOrdinals);
        }

        [TestMethod]
        public void UK_RegisteredUnderUKAndUKUA()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("UK"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("UK-UA"));
        }
    }
}
