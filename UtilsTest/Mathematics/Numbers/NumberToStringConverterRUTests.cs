using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterRUTests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "один запятая пять"),
                (12.34m, "двенадцать запятая три четыре"),
            ];

            var converter = NumberToStringConverter.GetConverter("RU");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("RU");
            (long n, string expected)[] cases =
            [
                (1,   "один"),
                (2,   "два"),
                (11,  "одиннадцать"),
                (20,  "двадцать"),
                (100, "сто"),
                (1_000, "тысяча"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"RU {n}");
        }

        [TestMethod]
        public void Ordinals_NominativeExceptions()
        {
            var c = NumberToStringConverter.GetConverter("RU");

            Assert.AreEqual("первый",    c.ConvertOrdinal(1));
            Assert.AreEqual("второй",    c.ConvertOrdinal(2));
            Assert.AreEqual("третий",    c.ConvertOrdinal(3));
            Assert.AreEqual("четвёртый", c.ConvertOrdinal(4));
            Assert.AreEqual("десятый",   c.ConvertOrdinal(10));
            Assert.AreEqual("сотый",     c.ConvertOrdinal(100));
            Assert.AreEqual("тысячный",  c.ConvertOrdinal(1_000));
        }

        [TestMethod]
        public void Ordinals_GenderVariants()
        {
            var c = NumberToStringConverter.GetConverter("RU");

            Assert.AreEqual("первый",  c.ConvertOrdinal(1));
            Assert.AreEqual("первая",  c.ConvertOrdinal(1, "gender=feminin"));
            Assert.AreEqual("первое",  c.ConvertOrdinal(1, "gender=neutrum"));
            Assert.AreEqual("первые",  c.ConvertOrdinal(1, "gender=plural"));
        }
    }
}
