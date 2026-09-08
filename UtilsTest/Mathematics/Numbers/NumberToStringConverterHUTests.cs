using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterHUTests
    {

        [TestMethod]
        public void HU_RegisteredUnderHUAndHUHU()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("HU"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("HU-HU"));
        }
    }
}
