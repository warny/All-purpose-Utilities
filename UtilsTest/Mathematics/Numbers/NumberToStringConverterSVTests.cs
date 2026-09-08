using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterSVTests
    {

        [TestMethod]
        public void SupportsOrdinals_False()
        {
            var c = NumberToStringConverter.GetConverter("SV");
            Assert.IsFalse(c.SupportsOrdinals);
        }

        [TestMethod]
        public void SV_RegisteredUnderSVAndSVSE()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("SV"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("SV-SE"));
        }
    }
}
