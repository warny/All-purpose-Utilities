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
            Assert.AreEqual("едно", c.Convert(1));
        }
    }
}
