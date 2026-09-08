using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterNOTests
    {

        [TestMethod]
        public void SupportsOrdinals_False()
        {
            var c = NumberToStringConverter.GetConverter("NO");
            Assert.IsFalse(c.SupportsOrdinals);
        }

        [TestMethod]
        public void NO_RegisteredUnderNOAndNBAndNBNO()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("NO"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("NB"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("NB-NO"));
        }
    }
}
