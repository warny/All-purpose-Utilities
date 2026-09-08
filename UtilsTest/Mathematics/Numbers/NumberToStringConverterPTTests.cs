using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterPTTests
    {

        // ─── NTS-04 ForcedVariants — "hora" is feminine, forced without a caller variant ────────

        [TestMethod]
        public void Convert_PT_OrdinaryCardinal_RemainsMasculineByDefault()
        {
            var c = NumberToStringConverter.GetConverter("PT");
            Assert.IsTrue(c.SupportsTimeConversion);
            Assert.AreEqual("um",           c.Convert(1));
            Assert.AreEqual("dois",         c.Convert(2));
            Assert.AreEqual("vinte e um",   c.Convert(21));
        }

        [TestMethod]
        public void Convert_TimeSpan_PT_Hours_ForcedFeminineWithoutExplicitVariant()
        {
            var c = NumberToStringConverter.GetConverter("PT");
            Assert.AreEqual("uma hora",             c.Convert(new TimeSpan(1, 0, 0)));
            Assert.AreEqual("duas horas",           c.Convert(new TimeSpan(2, 0, 0)));
            Assert.AreEqual("vinte e uma horas",    c.Convert(TimeSpan.FromHours(21)));
            Assert.AreEqual("vinte e duas horas",   c.Convert(TimeSpan.FromHours(22)));
        }

        [TestMethod]
        public void Convert_TimeSpan_PT_Minutes_RemainMasculine()
        {
            var c = NumberToStringConverter.GetConverter("PT");
            Assert.AreEqual("dois minutos", c.Convert(new TimeSpan(0, 2, 0)));
        }

        [TestMethod]
        public void Convert_TimeSpan_PT_Composite_FeminineHourDoesNotLeakIntoMasculineMinute()
        {
            var c = NumberToStringConverter.GetConverter("PT");
            Assert.AreEqual("duas horas dois minutos", c.Convert(new TimeSpan(2, 2, 0)));
        }

        [TestMethod]
        public void Convert_TimeSpan_PT_ExplicitMasculineIsOverriddenByForcedFeminine()
        {
            var c = NumberToStringConverter.GetConverter("PT");
            Assert.AreEqual("duas horas", c.Convert(new TimeSpan(2, 0, 0), "gender=masculino"));
        }
    }
}
