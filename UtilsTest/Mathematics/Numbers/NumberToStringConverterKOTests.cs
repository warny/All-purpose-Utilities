using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterKOTests
    {

        [TestMethod]
        public void KO_RegisteredUnderKO()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("KO"));
        }
    }
}
