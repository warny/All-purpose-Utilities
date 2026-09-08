using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterBGTests
    {

        [TestMethod]
        public void BG_RegisteredUnderBGBG()
        {
            var c = NumberToStringConverter.GetConverter("BG-BG");
            Assert.AreEqual(NumberToStringConverter.GetConverter("BG").Convert(1), c.Convert(1));
        }
    }
}
