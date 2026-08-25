using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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

        // ─── NTS-05 — attributive masculine apocope via the "form" ForcedVariants dimension ────
        //
        // Spanish time-unit support was previously deferred (DONE-2026-08-24(1).md) because
        // masculine attributive numeral apocope ("uno"->"un", "veintiuno"->"veintiún", "treinta y
        // uno"->"treinta y un") applies to compound counts (21, 31, ...) as well as count==1, which
        // Count1Form alone cannot express. NTS-05 resolves this with a new "form" variant dimension
        // (standalone/attributive) forced by the time units, reusing the existing ForcedVariants +
        // nested <Variant> engine — no new C# code for Spanish specifically, no ad-hoc replacements.

        [TestMethod]
        public void Convert_ES_OrdinaryCardinal_RemainsUnaffectedByAttributiveForm()
        {
            var c = NumberToStringConverter.GetConverter("ES");
            Assert.IsTrue(c.SupportsTimeConversion);
            // Ordinary cardinals default to form=standalone: no apocope leaks in.
            Assert.AreEqual("uno", c.Convert(1));
            Assert.AreEqual("veintiuno", c.Convert(21));
            Assert.AreEqual("treinta y uno", c.Convert(31));
        }

        [TestMethod]
        public void Convert_TimeSpan_ES_Hours_ForcedFeminineAttributiveWithoutExplicitVariant()
        {
            var c = NumberToStringConverter.GetConverter("ES");
            Assert.AreEqual("una hora", c.Convert(new TimeSpan(1, 0, 0)));
            Assert.AreEqual("veintiuna horas", c.Convert(TimeSpan.FromHours(21)));
            Assert.AreEqual("treinta y una horas", c.Convert(TimeSpan.FromHours(31)));
        }

        [TestMethod]
        public void Convert_TimeSpan_ES_Minutes_ForcedMasculineAttributiveApocope()
        {
            var c = NumberToStringConverter.GetConverter("ES");
            Assert.AreEqual("un minuto", c.Convert(new TimeSpan(0, 1, 0)));
            Assert.AreEqual("veintiún minutos", c.Convert(new TimeSpan(0, 21, 0)));
            Assert.AreEqual("treinta y un minutos", c.Convert(new TimeSpan(0, 31, 0)));
        }

        [TestMethod]
        public void Convert_TimeSpan_ES_Seconds_ForcedMasculineAttributiveApocope()
        {
            var c = NumberToStringConverter.GetConverter("ES");
            Assert.AreEqual("un segundo", c.Convert(new TimeSpan(0, 0, 1)));
            Assert.AreEqual("veintiún segundos", c.Convert(new TimeSpan(0, 0, 21)));
        }

        [TestMethod]
        public void Convert_TimeSpan_ES_Composite_FeminineHourAndMasculineMinuteSecondDoNotLeak()
        {
            var c = NumberToStringConverter.GetConverter("ES");
            Assert.AreEqual("veintiuna horas veintiún minutos veintiún segundos",
                c.Convert(new TimeSpan(21, 21, 21)));
        }
    }
}
