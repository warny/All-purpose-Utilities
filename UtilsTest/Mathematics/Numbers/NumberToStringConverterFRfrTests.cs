using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.NumberToString;
using Utils.Numerics;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterFRfrTests
    {

        [TestMethod]
        public void FrenchCompositeThousandsKeepUnit()
        {
            var converter = NumberToStringConverter.GetConverter("FR-fr");

            Assert.AreEqual("vingt et un mille", converter.Convert(21000));
            Assert.AreEqual("quatre cent un mille", converter.Convert(401000));
        }

        [TestMethod]
        public void BigIntTest()
        {
            (BigInteger Number, string Expected)[] tests = new (BigInteger Number, string Expected)[] {
                (
                    new BigInteger([0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], true, true),
                    "un tredecillion huit cent cinquante deux duodecilliards six cent soixante treize duodecillions quatre cent vingt sept unidecilliards sept cent quatre-vingt dix sept unidecillions cinquante neuf decilliards cent vingt six decillions sept cent soixante dix sept nonilliards cent trente cinq nonillions sept cent soixante octilliards cent trente neuf octillions six septilliards cinq cent vingt cinq septillions six cent cinquante deux sextilliards trois cent dix neuf sextillions sept cent cinquante quatre quintilliards six cent cinquante quintillions deux cent quarante neuf quadrilliards vingt quatre quadrillions six cent trente et un trilliards trois cent vingt et un trillions trois cent quarante quatre billiards cent vingt six billions six cent dix milliards soixante quatorze millions deux cent trente huit mille neuf cent soixante quinze"
                ),
            };

            var converter = NumberToStringConverter.GetConverter("FR-fr");

            foreach (var test in tests)
            {
                var value = converter.Convert(test.Number);
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

    }
}
