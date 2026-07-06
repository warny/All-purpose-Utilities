using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterPTTests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "um vírgula cinco"),
                (12.34m, "doze vírgula três quatro"),
            ];

            var converter = NumberToStringConverter.GetConverter("PT");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("PT");
            (long n, string expected)[] cases =
            [
                (1,   "um"),
                (2,   "dois"),
                (11,  "onze"),
                (20,  "vinte"),
                (100, "cem"),
                (101, "cento e um"),
                (200, "duzentos"),
                (1_000, "mil"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"PT {n}");
        }

        [TestMethod]
        public void Cardinals_Gender_Feminino()
        {
            var c = NumberToStringConverter.GetConverter("PT");

            Assert.AreEqual("uma",      c.Convert(1,   "gender=feminino"), "1f");
            Assert.AreEqual("duas",     c.Convert(2,   "gender=feminino"), "2f");
            Assert.AreEqual("duzentas", c.Convert(200, "gender=feminino"), "200f");
        }

        [TestMethod]
        public void Ordinals_MasculinoAndFeminino()
        {
            var c = NumberToStringConverter.GetConverter("PT");

            Assert.AreEqual("primeiro",  c.ConvertOrdinal(1));
            Assert.AreEqual("primeira",  c.ConvertOrdinal(1, "gender=feminino"));
            Assert.AreEqual("segundo",   c.ConvertOrdinal(2));
            Assert.AreEqual("segunda",   c.ConvertOrdinal(2, "gender=feminino"));
            Assert.AreEqual("décimo",    c.ConvertOrdinal(10));
            Assert.AreEqual("décima",    c.ConvertOrdinal(10, "gender=feminino"));
            Assert.AreEqual("vigésimo",  c.ConvertOrdinal(20));
            Assert.AreEqual("centésimo", c.ConvertOrdinal(100));
            Assert.AreEqual("milésimo",  c.ConvertOrdinal(1_000));
        }
    }
}
