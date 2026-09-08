using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterTRTests
    {

        [TestMethod]
        public void TR_RegisteredUnderTRTR()
        {
            Assert.AreEqual("iki", NumberToStringConverter.GetConverter("TR-TR").Convert(2));
        }
    }
}
