using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterCATests
    {
        [TestMethod]
        public void CatalanCardinals()
        {
            (int Number, string Expected)[] tests = [
                (21, "vint-i-un"),
                (105, "cent cinc"),
                (321, "tres-cents vint-i-un"),
            ];

            var converter = NumberToStringConverter.GetConverter("CA");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void CatalanDecimal()
        {
            var converter = NumberToStringConverter.GetConverter("ca-ES");
            Assert.AreEqual("un coma cinc", converter.Convert(1.5m));
        }

        [TestMethod]
        public void Cardinals_Gender_Femeni()
        {
            var c = NumberToStringConverter.GetConverter("CA");

            Assert.AreEqual("una",           c.Convert(1,  "gender=femení"), "1f");
            Assert.AreEqual("dues",          c.Convert(2,  "gender=femení"), "2f");
            Assert.AreEqual("vint-i-una",    c.Convert(21, "gender=femení"), "21f");
            Assert.AreEqual("vint-i-dues",   c.Convert(22, "gender=femení"), "22f");
        }

        // ─── NTS-04 ForcedVariants — "hora" is feminine, forced without a caller variant ────────

        [TestMethod]
        public void Convert_CA_OrdinaryCardinal_RemainsMasculineByDefault()
        {
            var c = NumberToStringConverter.GetConverter("CA");
            Assert.IsTrue(c.SupportsTimeConversion);
            Assert.AreEqual("un",         c.Convert(1));
            Assert.AreEqual("dos",        c.Convert(2));
            Assert.AreEqual("vint-i-un",  c.Convert(21));
        }

        [TestMethod]
        public void Convert_TimeSpan_CA_Hours_ForcedFeminineWithoutExplicitVariant()
        {
            var c = NumberToStringConverter.GetConverter("CA");
            Assert.AreEqual("una hora",              c.Convert(new TimeSpan(1, 0, 0)));
            Assert.AreEqual("dues hores",            c.Convert(new TimeSpan(2, 0, 0)));
            Assert.AreEqual("vint-i-una hores",      c.Convert(TimeSpan.FromHours(21)));
            Assert.AreEqual("vint-i-dues hores",     c.Convert(TimeSpan.FromHours(22)));
        }

        [TestMethod]
        public void Convert_TimeSpan_CA_Minutes_RemainMasculine()
        {
            var c = NumberToStringConverter.GetConverter("CA");
            Assert.AreEqual("dos minuts", c.Convert(new TimeSpan(0, 2, 0)));
        }

        [TestMethod]
        public void Convert_TimeSpan_CA_Composite_FeminineHourDoesNotLeakIntoMasculineMinute()
        {
            var c = NumberToStringConverter.GetConverter("CA");
            Assert.AreEqual("dues hores dos minuts", c.Convert(new TimeSpan(2, 2, 0)));
        }

        [TestMethod]
        public void Convert_TimeSpan_CA_ExplicitMasculineIsOverriddenByForcedFeminine()
        {
            var c = NumberToStringConverter.GetConverter("CA");
            Assert.AreEqual("dues hores", c.Convert(new TimeSpan(2, 0, 0), "gender=masculí"));
        }
    }
}
