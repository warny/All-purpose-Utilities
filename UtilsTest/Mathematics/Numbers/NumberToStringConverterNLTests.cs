using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterNLTests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "een komma vijf"),
                (12.34m, "twaalf komma drie vier"),
            ];

            var converter = NumberToStringConverter.GetConverter("NL");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("NL");
            (long n, string expected)[] cases =
            [
                (1,    "een"),
                (11,   "elf"),
                (12,   "twaalf"),
                (20,   "twintig"),
                (21,   "eenentwintig"),
                (100,  "honderd"),
                (1_000, "duizend"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"NL {n}");
        }

        [TestMethod]
        public void Ordinals_SuffixAndExceptions()
        {
            var c = NumberToStringConverter.GetConverter("NL");

            Assert.AreEqual("eerste",      c.ConvertOrdinal(1));
            Assert.AreEqual("tweede",      c.ConvertOrdinal(2));
            Assert.AreEqual("derde",       c.ConvertOrdinal(3));
            Assert.AreEqual("vierde",      c.ConvertOrdinal(4));
            Assert.AreEqual("vijfde",      c.ConvertOrdinal(5));
            Assert.AreEqual("tiende",      c.ConvertOrdinal(10));
            Assert.AreEqual("twintigste",  c.ConvertOrdinal(20));
            Assert.AreEqual("honderdste",  c.ConvertOrdinal(100));
        }
    }
}
