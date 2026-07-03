using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterWOTests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "benn pojint juróom"),
                (12.34m, "fukk ak ñaar pojint ñett ñent"),
            ];

            var converter = NumberToStringConverter.GetConverter("WO");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("WO");
            (long n, string expected)[] cases =
            [
                (1,   "benn"),
                (2,   "ñaar"),
                (10,  "fukk"),
                (11,  "fukk ak benn"),
                (20,  "ñaar-fukk"),
                (100, "téeméer"),
                (1_000, "benn junni"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"WO {n}");
        }

        [TestMethod]
        public void Ordinals_SuffixAndException()
        {
            var c = NumberToStringConverter.GetConverter("WO");

            Assert.AreEqual("bu njëkk", c.ConvertOrdinal(1));
            Assert.AreEqual("ñaarël",   c.ConvertOrdinal(2));
            Assert.AreEqual("fukkël",   c.ConvertOrdinal(10));
        }
    }
}
