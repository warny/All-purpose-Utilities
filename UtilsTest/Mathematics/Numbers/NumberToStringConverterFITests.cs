using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterFITests
    {

        /// <summary>
        /// Verifies that the Finnish converter advertises ordinal support.
        /// </summary>
        [TestMethod]
        public void SupportsOrdinals_IsTrue()
        {
            Assert.IsTrue(NumberToStringConverter.GetConverter("FI").SupportsOrdinals);
        }

        [TestMethod]
        public void FI_RegisteredUnderFI()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("FI"));
        }
    }
}
