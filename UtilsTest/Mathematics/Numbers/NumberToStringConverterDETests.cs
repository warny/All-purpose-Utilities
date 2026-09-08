using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterDETests
    {

        [TestMethod]
        public void BigIntTest()
        {
            (BigInteger Number, string Expected)[] tests = [
                (
                    new BigInteger([0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], true, true),
                    "eine Tredezillion achthundertzweiundfünfzig Duodezilliarden sechshundertdreiundsiebzig Duodezillionen vierhundertsiebenundzwanzig Unidezilliarden siebenhundertsiebenundneunzig Unidezillionen neunundfünfzig Dezilliarden einhundertsechsundzwanzig Dezillionen siebenhundertsiebenundsiebzig Nonilliarden einhundertfünfunddreißig Nonillionen siebenhundertsechzig Octilliarden einhundertneununddreißig Octillionen sechs Septilliarden fünfhundertfünfundzwanzig Septillionen sechshundertzweiundfünfzig Sextilliarden dreihundertneunzehn Sextillionen siebenhundertvierundfünfzig Quintilliarden sechshundertfünfzig Quintillionen zweihundertneunundvierzig Quadrilliarden vierundzwanzig Quadrillionen sechshunderteinunddreißig Trilliarden dreihunderteinundzwanzig Trillionen dreihundertvierundvierzig Billiarden einhundertsechsundzwanzig Billionen sechshundertzehn Milliarden vierundsiebzig Millionen zweihundertachtunddreißig tausend neunhundertfünfundsiebzig"
                ),
            ];

            var converter = NumberToStringConverter.GetConverter("de-DE");

            foreach (var test in tests)
            {
                var value = converter.Convert(test.Number);
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void ConvertCurrency_DE_Euro()
        {
            var c = NumberToStringConverter.GetConverter("DE");
            var euro = new CurrencyDefinition
            {
                UnitSingular    = "Euro",
                UnitPlural      = "Euro",
                SubunitSingular = "Cent",
                SubunitPlural   = "Cent",
                Connector       = "und",
            };

            // Convert(1) = "eins" (standalone); currency context does not apply attributive form
            Assert.AreEqual("eins Euro",                  c.ConvertCurrency(1m,    euro));
            Assert.AreEqual("zwei Euro",                  c.ConvertCurrency(2m,    euro));
            Assert.AreEqual("eins Euro und fünfzig Cent", c.ConvertCurrency(1.50m, euro));
        }

        [TestMethod]
        public void ConvertCurrency_DE_Gender_Feminine()
        {
            var c = NumberToStringConverter.GetConverter("DE");
            var krone = new CurrencyDefinition
            {
                UnitSingular    = "Krone",
                UnitPlural      = "Kronen",
                SubunitSingular = "Heller",
                SubunitPlural   = "Heller",
                Connector       = "und",
            };

            // Convert(1) = "eins"; GermanSpecifics replaces "\bein [A-Z]" → "eine" but not "eins"
            Assert.AreEqual("eins Krone",  c.ConvertCurrency(1m,  krone));
            Assert.AreEqual("zwei Kronen", c.ConvertCurrency(2m,  krone));
        }
    }
}
