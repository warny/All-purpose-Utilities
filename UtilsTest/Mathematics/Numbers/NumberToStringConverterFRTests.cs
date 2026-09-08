using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.NumberToString;
using Utils.Numerics;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterFRTests
    {

        [TestMethod]
        public void FrenchRegionalCompositeThousandsKeepUnit()
        {
            var converter = NumberToStringConverter.GetConverter("FR-ch");

            Assert.AreEqual("vingt et un mille", converter.Convert(21000));
            Assert.AreEqual("quatre cent un mille", converter.Convert(401000));
        }

        [TestMethod]
        public void BigIntTest()
        {
            (BigInteger Number, string Expected)[] tests = new (BigInteger Number, string Expected)[] {
                (
                    new BigInteger([0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], true, true),
                    "un tredecillion huit cent cinquante deux duodecilliards six cent septante trois duodecillions quatre cent vingt sept unidecilliards sept cent nonante sept unidecillions cinquante neuf decilliards cent vingt six decillions sept cent septante sept nonilliards cent trente cinq nonillions sept cent soixante octilliards cent trente neuf octillions six septilliards cinq cent vingt cinq septillions six cent cinquante deux sextilliards trois cent dix neuf sextillions sept cent cinquante quatre quintilliards six cent cinquante quintillions deux cent quarante neuf quadrilliards vingt quatre quadrillions six cent trente et un trilliards trois cent vingt et un trillions trois cent quarante quatre billiards cent vingt six billions six cent dix milliards septante quatre millions deux cent trente huit mille neuf cent septante cinq"
                ),
            };

            var converter = NumberToStringConverter.GetConverter("FR-ch");

            foreach (var test in tests)
            {
                var value = converter.Convert(test.Number);
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void FractionConversionTest()
        {
            var converter = NumberToStringConverter.GetConverter("FR-ch");

            Assert.AreEqual("trois sur deux", converter.Convert(new Number(3, 2)));
            Assert.AreEqual("un sur dix", converter.Convert(new Number(1, 10)));
        }
    }
}
