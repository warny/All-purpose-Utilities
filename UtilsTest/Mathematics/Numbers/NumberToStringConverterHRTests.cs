using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.NumberToString
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
