using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterEETests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "deka kpɔ atɔ"),
                (12.34m, "ewo kple eve kpɔ eto ene"),
            ];

            var converter = NumberToStringConverter.GetConverter("EE");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("EE");
            (long n, string expected)[] cases =
            [
                (1,   "deka"),
                (2,   "eve"),
                (3,   "eto"),
                (10,  "ewo"),
                (11,  "ewo kple deka"),
                (19,  "ewo kple asea"),
                (20,  "blavo eve"),
                (100, "kpeɖe"),
                (1_000, "deka akpe"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"EE {n}");
        }

        [TestMethod]
        public void Ordinal_FirstException()
        {
            var c = NumberToStringConverter.GetConverter("EE");
            Assert.AreEqual("gbãtõ", c.ConvertOrdinal(1));
        }
    }
}
