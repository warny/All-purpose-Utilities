using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterENTests
    {

        [TestMethod]
        public void BigIntTest()
        {
            (BigInteger Number, string Expected)[] tests = [
                (
                    new BigInteger([0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], true, true),
                    "one quinquavingtillion, eight hundred and fifty-two quattuorvingtillion, six hundred and seventy-three tresvingtillion, four hundred and twenty-seven duovingtillion, seven hundred and ninety-seven univingtillion, fifty-nine vingtillion, one hundred and twenty-six novendecillion, seven hundred and seventy-seven octodecillion, one hundred and thirty-five septendecillion, seven hundred and sixty sedecillion, one hundred and thirty-nine quinquadecillion, six quattuordecillion, five hundred and twenty-five tredecillion, six hundred and fifty-two duodecillion, three hundred and nineteen unidecillion, seven hundred and fifty-four decillion, six hundred and fifty nonillion, two hundred and forty-nine octillion, twenty-four septillion, six hundred and thirty-one sextillion, three hundred and twenty-one quintillion, three hundred and forty-four quadrillion, one hundred and twenty-six trillion, six hundred and ten billion, seventy-four million, two hundred and thirty-eight thousand, nine hundred and seventy-five"
                ),
            ];

            var converter = NumberToStringConverter.GetConverter("en-UK");

            foreach (var test in tests)
            {
                var value = converter.Convert(test.Number);
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

    }
}
