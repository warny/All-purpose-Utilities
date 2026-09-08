using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterVNTests
    {

        /// <summary>
        /// Verifies that values above the Vietnamese converter maximum are rejected.
        /// </summary>
        [TestMethod]
        public void Cardinals_AboveMaximum_Throws()
        {
            var c = NumberToStringConverter.GetConverter("VN");
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => c.Convert(1_000_000_000_000L));
        }

        [TestMethod]
        public void VN_RegisteredUnderVNAndVIAndVIVN()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("VN"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("VI"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("VI-VN"));
        }
    }
}
