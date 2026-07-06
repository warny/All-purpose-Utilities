using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterFITests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("FI");
            (long n, string expected)[] cases =
            [
                (0,   "nolla"),
                (1,   "yksi"),
                (2,   "kaksi"),
                (3,   "kolme"),
                (9,   "yhdeksän"),
                (10,  "kymmenen"),
                (11,  "yksitoista"),
                (12,  "kaksitoista"),
                (19,  "yhdeksäntoista"),
                (20,  "kaksikymmentä"),
                (21,  "kaksikymmentä yksi"),
                (100, "sata"),
                (200, "kaksisataa"),
                (101, "sata yksi"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"FI {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("FI");
            // Finnish: no replacement rule for 1×tuhat; engine outputs "yksi tuhat"
            Assert.AreEqual("yksi tuhat",   c.Convert(1_000));
            Assert.AreEqual("kaksi tuhat",  c.Convert(2_000));
        }

        [TestMethod]
        public void Ordinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("FI");
            Assert.IsTrue(c.SupportsOrdinals);
            // OrdinalExceptions for 1 and 2
            Assert.AreEqual("ensimmäinen", c.ConvertOrdinal(1));
            Assert.AreEqual("toinen",      c.ConvertOrdinal(2));
            // Word-based ordinal transforms (last word)
            Assert.AreEqual("kolmas",         c.ConvertOrdinal(3));
            Assert.AreEqual("kymmenes",       c.ConvertOrdinal(10));
            Assert.AreEqual("yhdestoista",    c.ConvertOrdinal(11));
            Assert.AreEqual("kahdestoista",   c.ConvertOrdinal(12));
            Assert.AreEqual("kahdeskymmenes", c.ConvertOrdinal(20));
            Assert.AreEqual("sadas",          c.ConvertOrdinal(100));
            // 1000 = "yksi tuhat" → last word "tuhat" → "tuhannes"
            Assert.AreEqual("yksi tuhannes",  c.ConvertOrdinal(1_000));
        }

        [TestMethod]
        public void Variants_Partitiivi()
        {
            var c = NumberToStringConverter.GetConverter("FI");
            // Variants use "dimension=value" format
            Assert.AreEqual("yhtä",      c.Convert(1,     "case=partitiivi"));
            Assert.AreEqual("kahta",     c.Convert(2,     "case=partitiivi"));
            Assert.AreEqual("kolmea",    c.Convert(3,     "case=partitiivi"));
            Assert.AreEqual("kymmentä",  c.Convert(10,    "case=partitiivi"));
            Assert.AreEqual("sataa",         c.Convert(100,   "case=partitiivi"));
            // 1000 = "yksi tuhat" → LastWord "tuhat" → "tuhatta" → "yksi tuhatta"
            Assert.AreEqual("yksi tuhatta", c.Convert(1_000, "case=partitiivi"));
        }

        [TestMethod]
        public void Variants_Genetiivi()
        {
            var c = NumberToStringConverter.GetConverter("FI");
            Assert.AreEqual("yhden",    c.Convert(1,     "case=genetiivi"));
            Assert.AreEqual("kahden",   c.Convert(2,     "case=genetiivi"));
            Assert.AreEqual("sadan",     c.Convert(100,   "case=genetiivi"));
            // "tuhat" in "yksi tuhat" uses scope="Standalone" in config → not replaced when not alone
            Assert.AreEqual("yksi tuhat", c.Convert(1_000, "case=genetiivi"));
        }

        [TestMethod]
        public void FI_RegisteredUnderFI()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("FI"));
        }
    }
}
