using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterUKTests
    {

        [TestMethod]
        public void SupportsOrdinals_False()
        {
            var c = NumberToStringConverter.GetConverter("UK");
            Assert.IsFalse(c.SupportsOrdinals);
        }

        [TestMethod]
        public void UK_RegisteredUnderUKAndUKUA()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("UK"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("UK-UA"));
        }
    }
}
