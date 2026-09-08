using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterFATests
    {

        [TestMethod]
        public void Cardinals_AboveMaximum_Throws()
        {
            var c = NumberToStringConverter.GetConverter("FA");
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => c.Convert(new System.Numerics.BigInteger(1_000_000_000_000_000L)));
        }

        [TestMethod]
        public void FA_RegisteredUnderFAIR()
        {
            Assert.AreEqual("دو", NumberToStringConverter.GetConverter("FA-IR").Convert(2));
        }
    }
}
