using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterHRTests
    {

        [TestMethod]
        public void HR_RegisteredUnderHRAndHRHR()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("HR"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("HR-HR"));
        }
    }
}
