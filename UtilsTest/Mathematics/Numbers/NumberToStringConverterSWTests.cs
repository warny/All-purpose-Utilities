using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterSWTests
    {

        [TestMethod]
        public void SW_RegisteredUnderSWKE()
        {
            Assert.AreEqual("mbili", NumberToStringConverter.GetConverter("SW-KE").Convert(2));
        }

        [TestMethod]
        public void SW_RegisteredUnderSWTZ()
        {
            Assert.AreEqual("mbili", NumberToStringConverter.GetConverter("SW-TZ").Convert(2));
        }
    }
}
