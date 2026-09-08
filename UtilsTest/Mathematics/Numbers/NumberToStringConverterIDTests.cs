using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterIDTests
    {

        [TestMethod]
        public void ID_RegisteredUnderIDID()
        {
            Assert.AreEqual("dua", NumberToStringConverter.GetConverter("ID-ID").Convert(2));
        }
    }
}
