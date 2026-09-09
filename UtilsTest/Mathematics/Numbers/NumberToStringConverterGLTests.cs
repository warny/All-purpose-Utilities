using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterGLTests
    {

        // ─── NTS-04 ForcedVariants — "hora" is feminine, forced without a caller variant ────────

        /// <summary>Verifies that the converter advertises time conversion support.</summary>
        [TestMethod]
        public void SupportsTimeConversion_GL_True()
        {
            Assert.IsTrue(NumberToStringConverter.GetConverter("GL").SupportsTimeConversion);
        }

    }
}
