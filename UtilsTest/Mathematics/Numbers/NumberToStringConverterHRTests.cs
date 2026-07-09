using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterHRTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("HR");
            (long n, string expected)[] cases =
            [
                (0,   "nula"),
                (1,   "jedan"),
                (2,   "dva"),
                (9,   "devet"),
                (10,  "deset"),
                (11,  "jedanaest"),
                (12,  "dvanaest"),
                (19,  "devetnaest"),
                (20,  "dvadeset"),
                (21,  "dvadeset jedan"),
                (100, "sto"),
                (101, "sto jedan"),
                (200, "dvjesto"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"HR {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("HR");
            // "jedan tisuća" is elided to "tisuća" (Replacement onScale=1 onValue=1)
            Assert.AreEqual("tisuća",       c.Convert(1_000));
            Assert.AreEqual("dva tisuća",   c.Convert(2_000));
            Assert.AreEqual("deset tisuća", c.Convert(10_000));
        }

        [TestMethod]
        public void Cardinals_LongScale()
        {
            var c = NumberToStringConverter.GetConverter("HR");
            // Long scale generated via Suffixes "jun"/"jarda" and groupSeparator "li"
            Assert.AreEqual("jedan milijun",   c.Convert(1_000_000));
            Assert.AreEqual("jedan milijarda", c.Convert(1_000_000_000));
            Assert.AreEqual("jedan bilijun",   c.Convert(1_000_000_000_000L));
        }

        [TestMethod]
        public void Ordinals_Exceptions()
        {
            var c = NumberToStringConverter.GetConverter("HR");
            Assert.IsTrue(c.SupportsOrdinals);
            Assert.AreEqual("prvi",  c.ConvertOrdinal(1));
            Assert.AreEqual("drugi", c.ConvertOrdinal(2));
            Assert.AreEqual("treći", c.ConvertOrdinal(3));
        }

        [TestMethod]
        public void HR_RegisteredUnderHRAndHRHR()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("HR"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("HR-HR"));
        }
    }
}
