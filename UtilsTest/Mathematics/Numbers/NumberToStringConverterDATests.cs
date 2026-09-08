using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterDATests
    {

        [TestMethod]
        public void DA_RegisteredUnderDADK()
        {
            Assert.AreEqual(NumberToStringConverter.GetConverter("DA").Convert(2), NumberToStringConverter.GetConverter("DA-DK").Convert(2));
        }
    }
}
