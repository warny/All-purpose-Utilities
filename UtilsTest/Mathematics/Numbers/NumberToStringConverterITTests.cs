using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterITTests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "uno virgola cinque"),
                (12.34m, "dodici virgola tre quattro"),
            ];

            var converter = NumberToStringConverter.GetConverter("IT");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("IT");
            (long n, string expected)[] cases =
            [
                (1,   "uno"),
                (11,  "undici"),
                (20,  "venti"),
                (21,  "venti uno"),
                (100, "cento"),
                (1_000, "mille"),
                (2_000, "due mila"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"IT {n}");
        }

        [TestMethod]
        public void Cardinals_Gender_Femminile()
        {
            var c = NumberToStringConverter.GetConverter("IT");

            Assert.AreEqual("una", c.Convert(1, "gender=femminile"), "1f");
            Assert.AreEqual("venti una", c.Convert(21, "gender=femminile"), "21f");
        }

        [TestMethod]
        public void Ordinals_MaschileAndFemminile()
        {
            var c = NumberToStringConverter.GetConverter("IT");

            Assert.AreEqual("primo",         c.ConvertOrdinal(1));
            Assert.AreEqual("prima",         c.ConvertOrdinal(1, "gender=femminile"));
            Assert.AreEqual("secondo",       c.ConvertOrdinal(2));
            Assert.AreEqual("seconda",       c.ConvertOrdinal(2, "gender=femminile"));
            Assert.AreEqual("terzo",         c.ConvertOrdinal(3));
            Assert.AreEqual("decimo",        c.ConvertOrdinal(10));
            Assert.AreEqual("undicesimo",    c.ConvertOrdinal(11));
            Assert.AreEqual("undicesima",    c.ConvertOrdinal(11, "gender=femminile"));
            Assert.AreEqual("ventesimo",     c.ConvertOrdinal(20));
            Assert.AreEqual("centesimo",     c.ConvertOrdinal(100));
        }

        [TestMethod]
        public void ConvertCurrency_IT_Euro()
        {
            var c = NumberToStringConverter.GetConverter("IT");
            var euro = new CurrencyDefinition
            {
                UnitSingular   = "euro",
                UnitPlural     = "euro",
                SubunitSingular = "centesimo",
                SubunitPlural  = "centesimi",
                Connector      = "e",
            };

            Assert.AreEqual("uno euro",          c.ConvertCurrency(1m,    euro));
            Assert.AreEqual("due euro",          c.ConvertCurrency(2m,    euro));
            Assert.AreEqual("uno euro e cinquanta centesimi", c.ConvertCurrency(1.50m, euro));
        }
    }
}
