using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterCSTests
    {

        [TestMethod]
        public void CS_RegisteredUnderCSCZ()
        {
            Assert.AreEqual("dva", NumberToStringConverter.GetConverter("CS-CZ").Convert(2));
        }
    }
}
