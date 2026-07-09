using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterESTests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "uno coma cinco"),
                (12.34m, "doce coma tres cuatro"),
            ];

            var converter = NumberToStringConverter.GetConverter("ES");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void CastilianCultureAliases()
        {
            var reference = NumberToStringConverter.GetConverter("ES");

            (string Culture, decimal Value, string Expected)[] tests = [
                ("es-ES", 21.4m, "veintiuno coma cuatro"),
                ("ES-es", 1000m, "mil"),
            ];

            foreach (var test in tests)
            {
                var converter = NumberToStringConverter.GetConverter(test.Culture);
                Assert.AreEqual(reference.Convert(test.Value), converter.Convert(test.Value));
                Assert.AreEqual(test.Expected, converter.Convert(test.Value));
            }
        }

        [TestMethod]
        public void SpanishThousandsDoNotCollapseWithinCompositeNumbers()
        {
            var converter = NumberToStringConverter.GetConverter("ES");

            var oneThousand = converter.Convert(1000);
            var twentyOneThousand = converter.Convert(21000);
            var thirtyOneThousand = converter.Convert(31000);

            Assert.AreEqual("mil", oneThousand);
            StringAssert.Contains(twentyOneThousand, "uno mil", "Composite thousands should retain the phrase 'uno mil'.");
            Assert.IsFalse(twentyOneThousand.Contains("veintimil"), "The substring replacement must not collapse 'veintiuno mil'.");
            StringAssert.Contains(thirtyOneThousand, "uno mil", "Composite thousands should retain the phrase 'uno mil'.");
            Assert.IsFalse(thirtyOneThousand.Contains("treintamil"), "The substring replacement must not collapse 'treintauno mil'.");
        }

        [TestMethod]
        public void ConvertCurrency_ES_Euro()
        {
            var c = NumberToStringConverter.GetConverter("ES");
            var euro = new CurrencyDefinition
            {
                UnitSingular    = "euro",
                UnitPlural      = "euros",
                SubunitSingular = "céntimo",
                SubunitPlural   = "céntimos",
                Connector       = "con",
            };

            // Convert(1) = "uno" (standalone); no attributive shortening in ES config
            Assert.AreEqual("uno euro",                       c.ConvertCurrency(1m,    euro));
            Assert.AreEqual("dos euros",                      c.ConvertCurrency(2m,    euro));
            Assert.AreEqual("uno euro con cincuenta céntimos", c.ConvertCurrency(1.50m, euro));
        }

        [TestMethod]
        public void ConvertCurrency_ES_Gender_Femenino()
        {
            var c = NumberToStringConverter.GetConverter("ES");
            var peseta = new CurrencyDefinition
            {
                UnitSingular    = "peseta",
                UnitPlural      = "pesetas",
                SubunitSingular = "céntimo",
                SubunitPlural   = "céntimos",
                Connector       = "con",
            };

            Assert.AreEqual("una peseta",  c.ConvertCurrency(1m, peseta, "gender=femenino"));
            // 31 uses the space-separated buildString ("treinta y *") — LastWord applies directly
            Assert.AreEqual("treinta y una pesetas", c.ConvertCurrency(31m, peseta, "gender=femenino"));
        }

        [TestMethod]
        public void Convert_ES_21_29_Gender_Femenino()
        {
            var c = NumberToStringConverter.GetConverter("ES");

            // item 41: 21 is a fused word ("veintiuno") — whole-word replacement now covers it.
            Assert.AreEqual("veintiuna",  c.Convert(21, "gender=femenino"));
            Assert.AreEqual("veintiuno",  c.Convert(21));
            // 22-29 do not vary in gender (only the "uno" unit does).
            Assert.AreEqual("veintidos",  c.Convert(22, "gender=femenino"));
            Assert.AreEqual("veintinueve", c.Convert(29, "gender=femenino"));
        }
    }
}
