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
            Assert.AreEqual("dva", NumberToStringConverter.GetConverter("SK-SK").Convert(2));
        }
    }
}
