using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics;

namespace UtilsTest.Objects
{
    [TestClass]
    public class NumberToStringConverterENTests
    {
        [TestMethod]
        public void From1To999Test()
        {
            (long Number, string Expected)[] tests = [
                (-1, "minus one"),
                (0, "zero"),
                (1, "one"),
                (2, "two"),
                (11, "eleven"),
                (20, "twenty"),
                (21, "twenty one"),
                (22, "twenty two"),
                (60, "sixty"),
                (61, "sixty one"),
                (62, "sixty two"),
                (111, "one hundred and eleven"),
                (121, "one hundred and twenty one"),
                (122, "one hundred and twenty two"),
                (160, "one hundred and sixty"),
                (161, "one hundred and sixty one"),
                (162, "one hundred and sixty two"),
                (200, "two hundreds"),
                (201, "two hundreds and one"),
                (221, "two hundreds and twenty one"),
                (222, "two hundreds and twenty two"),
                (260, "two hundreds and sixty"),
                (261, "two hundreds and sixty one"),
                (262, "two hundreds and sixty two"),
            ];

            var converter = NumberToStringConverter.EnglishAmericanNumbers();

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void From1000To9999Test()
        {
            (long Number, string Expected)[] tests = new (long Number, string Expected)[] {
                (1000, "one thousand"),
                (1001, "one thousand one"),
                (1002, "one thousand two"),
                (1011, "one thousand eleven"),
                (1020, "one thousand twenty"),
                (1021, "one thousand twenty one"),
                (1022, "one thousand twenty two"),
                (1060, "one thousand sixty"),
                (1061, "one thousand sixty one"),
                (1062, "one thousand sixty two"),
                (1111, "one thousand one hundred and eleven"),
                (1121, "one thousand one hundred and twenty one"),
                (1122, "one thousand one hundred and twenty two"),
                (1160, "one thousand one hundred and sixty"),
                (1161, "one thousand one hundred and sixty one"),
                (1162, "one thousand one hundred and sixty two"),
                (1200, "one thousand two hundreds"),
                (1201, "one thousand two hundreds and one"),
                (1221, "one thousand two hundreds and twenty one"),
                (1222, "one thousand two hundreds and twenty two"),
                (1260, "one thousand two hundreds and sixty"),
                (1261, "one thousand two hundreds and sixty one"),
                (1262, "one thousand two hundreds and sixty two"),
            };

            var converter = NumberToStringConverter.EnglishAmericanNumbers();

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void From10000To99999Test()
        {
            (long Number, string Expected)[] tests = new (long Number, string Expected)[] {
                (12000, "twelve thousands"),
                (12001, "twelve thousands one"),
                (12002, "twelve thousands two"),
                (12011, "twelve thousands eleven"),
                (12020, "twelve thousands twenty"),
                (12021, "twelve thousands twenty one"),
                (12022, "twelve thousands twenty two"),
                (12060, "twelve thousands sixty"),
                (12061, "twelve thousands sixty one"),
                (12062, "twelve thousands sixty two"),
                (12111, "twelve thousands one hundred and eleven"),
                (12121, "twelve thousands one hundred and twenty one"),
                (12122, "twelve thousands one hundred and twenty two"),
                (99160, "ninety nine thousands one hundred and sixty"),
                (99161, "ninety nine thousands one hundred and sixty one"),
                (99162, "ninety nine thousands one hundred and sixty two"),
                (99200, "ninety nine thousands two hundreds"),
                (99201, "ninety nine thousands two hundreds and one"),
                (99221, "ninety nine thousands two hundreds and twenty one"),
                (99222, "ninety nine thousands two hundreds and twenty two"),
                (99260, "ninety nine thousands two hundreds and sixty"),
                (99261, "ninety nine thousands two hundreds and sixty one"),
                (99262, "ninety nine thousands two hundreds and sixty two"),
            };

            var converter = NumberToStringConverter.EnglishAmericanNumbers();

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void BiggerTest()
        {
            (long Number, string Expected)[] tests = new (long Number, string Expected)[] {
                (-401000, "minus four hundreds and one thousands"),
                (401000, "four hundreds and one thousands"),
                (999999, "nine hundreds and ninety nine thousands nine hundreds and ninety nine"),
                (1000000, "one million"),
                (999999999, "nine hundreds and ninety nine millions nine hundreds and ninety nine thousands nine hundreds and ninety nine"),
            };

            var converter = NumberToStringConverter.EnglishAmericanNumbers();

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void BigIntTest()
        {
            (BigInteger Number, string Expected)[] tests = new (BigInteger Number, string Expected)[] {
                (
                    new BigInteger([0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], true, true),
                    "one quinquavingtillion height hundreds and fifty two quattuorvingtillions six hundreds and seventy three tresvingtillions four hundreds and twenty seven duovingtillions seven hundreds and ninety seven univingtillions fifty nine vingtillions one hundred and twenty six novendecillions seven hundreds and seventy seven octodecillions one hundred and thirty five septendecillions seven hundreds and sixty sedecillions one hundred and thirty nine quinquadecillions six quattuordecillions five hundreds and twenty five tredecillions six hundreds and fifty two duodecillions three hundreds and nineteen unidecillions seven hundreds and fifty four decillions six hundreds and fifty nonillions two hundreds and forty nine octillions twenty four septillions six hundreds and thirty one sextillions three hundreds and twenty one quintillions three hundreds and forty four quadrillions one hundred and twenty six trillions six hundreds and ten billions seventy four millions two hundreds and thirty height thousands nine hundreds and seventy five"
                ),
            };

            var converter = NumberToStringConverter.EnglishAmericanNumbers();

            foreach (var test in tests)
            {
                var value = converter.Convert(test.Number);
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }
    }
}
