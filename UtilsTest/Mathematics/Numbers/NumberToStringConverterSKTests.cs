using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterSKTests
    {

        [TestMethod]
        public void SK_RegisteredUnderSKSK()
        {
            Assert.AreEqual(NumberToStringConverter.GetConverter("SK").Convert(2), NumberToStringConverter.GetConverter("SK-SK").Convert(2));
        }
    }
}
