using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterGLTests
    {
        [TestMethod]
        public void GalicianCardinals()
        {
            (int Number, string Expected)[] tests = [
                (21, "vinte e un"),
                (105, "cento cinco"),
                (100, "cen"),
            ];

            var converter = NumberToStringConverter.GetConverter("GL");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void GalicianDecimal()
        {
            var converter = NumberToStringConverter.GetConverter("gl-ES");
            Assert.AreEqual("un coma cinco", converter.Convert(1.5m));
        }

        [TestMethod]
        public void Cardinals_Gender_Feminino()
        {
            var c = NumberToStringConverter.GetConverter("GL");

            Assert.AreEqual("unha",       c.Convert(1,   "gender=feminino"), "1f");
            Assert.AreEqual("dúas",       c.Convert(2,   "gender=feminino"), "2f");
            // item 33: 200 is the only hundred that varies in Galician (douscentos/douscentas);
            // other hundreds (e.g. trescentos) are invariable — see NumberConvertionConfiguration.GL.xml.
            Assert.AreEqual("douscentas", c.Convert(200, "gender=feminino"), "200f");
            Assert.AreEqual("trescentos", c.Convert(300, "gender=feminino"), "300f (invariable)");
        }

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
