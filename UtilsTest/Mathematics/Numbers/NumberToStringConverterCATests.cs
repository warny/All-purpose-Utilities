using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterCATests
    {

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
