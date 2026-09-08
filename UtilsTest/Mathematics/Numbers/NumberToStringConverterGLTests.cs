using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterGLTests
    {

        // ─── NTS-04 ForcedVariants — "hora" is feminine, forced without a caller variant ────────

        [TestMethod]
        public void Convert_GL_OrdinaryCardinal_RemainsMasculineByDefault()
        {
            var c = NumberToStringConverter.GetConverter("GL");
            Assert.IsTrue(c.SupportsTimeConversion);
            Assert.AreEqual("un",          c.Convert(1));
            Assert.AreEqual("dous",        c.Convert(2));
            Assert.AreEqual("vinte e un",  c.Convert(21));
        }

        [TestMethod]
        public void Convert_TimeSpan_GL_Hours_ForcedFeminineWithoutExplicitVariant()
        {
            var c = NumberToStringConverter.GetConverter("GL");
            Assert.AreEqual("unha hora",           c.Convert(new TimeSpan(1, 0, 0)));
            Assert.AreEqual("dúas horas",          c.Convert(new TimeSpan(2, 0, 0)));
            Assert.AreEqual("vinte e unha horas",  c.Convert(TimeSpan.FromHours(21)));
            Assert.AreEqual("vinte e dúas horas",  c.Convert(TimeSpan.FromHours(22)));
        }

        [TestMethod]
        public void Convert_TimeSpan_GL_Minutes_RemainMasculine()
        {
            var c = NumberToStringConverter.GetConverter("GL");
            Assert.AreEqual("dous minutos", c.Convert(new TimeSpan(0, 2, 0)));
        }

        [TestMethod]
        public void Convert_TimeSpan_GL_Composite_FeminineHourDoesNotLeakIntoMasculineMinute()
        {
            var c = NumberToStringConverter.GetConverter("GL");
            Assert.AreEqual("dúas horas dous minutos", c.Convert(new TimeSpan(2, 2, 0)));
        }

        [TestMethod]
        public void Convert_TimeSpan_GL_ExplicitMasculineIsOverriddenByForcedFeminine()
        {
            var c = NumberToStringConverter.GetConverter("GL");
            Assert.AreEqual("dúas horas", c.Convert(new TimeSpan(2, 0, 0), "gender=masculino"));
        }
    }
}
