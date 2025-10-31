﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics;
using Utils.Numerics;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterFRTests
    {
        [TestMethod]
        public void From1To999Test()
        {
            (long Number, string Expected)[] tests = new (long Number, string Expected)[] {
                (-1, "moins un"),
                (0, "zéro"),
                (1, "un"),
                (2, "deux"),
                (11, "onze"),
                (20, "vingt"),
                (21, "vingt et un"),
                (22, "vingt deux"),
                (60, "soixante"),
                (61, "soixante et un"),
                (62, "soixante deux"),
                (72, "septante deux"),
                (82, "huitante deux"),
                (92, "nonante deux"),
                (111, "cent onze"),
                (121, "cent vingt et un"),
                (122, "cent vingt deux"),
                (160, "cent soixante"),
                (161, "cent soixante et un"),
                (162, "cent soixante deux"),
                (200, "deux cents"),
                (201, "deux cent un"),
                (221, "deux cent vingt et un"),
                (222, "deux cent vingt deux"),
                (260, "deux cent soixante"),
                (261, "deux cent soixante et un"),
                (262, "deux cent soixante deux"),
            };

            var converter = NumberToStringConverter.GetConverter("FR-ch");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void From1000To9999Test()
        {
            (long Number, string Expected)[] tests = new (long Number, string Expected)[] {
                (1000, "mille"),
                (1001, "mille un"),
                (1002, "mille deux"),
                (1011, "mille onze"),
                (1020, "mille vingt"),
                (1021, "mille vingt et un"),
                (1022, "mille vingt deux"),
                (1060, "mille soixante"),
                (1061, "mille soixante et un"),
                (1062, "mille soixante deux"),
                (1111, "mille cent onze"),
                (1121, "mille cent vingt et un"),
                (1122, "mille cent vingt deux"),
                (1160, "mille cent soixante"),
                (1161, "mille cent soixante et un"),
                (1162, "mille cent soixante deux"),
                (1200, "mille deux cents"),
                (1201, "mille deux cent un"),
                (1221, "mille deux cent vingt et un"),
                (1222, "mille deux cent vingt deux"),
                (1260, "mille deux cent soixante"),
                (1261, "mille deux cent soixante et un"),
                (1262, "mille deux cent soixante deux"),
            };

            var converter = NumberToStringConverter.GetConverter("FR-ch");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void From10000To99999Test()
        {
            (long Number, string Expected)[] tests = new (long Number, string Expected)[] {
                (12000, "douze mille"),
                (12001, "douze mille un"),
                (12002, "douze mille deux"),
                (12011, "douze mille onze"),
                (12020, "douze mille vingt"),
                (12021, "douze mille vingt et un"),
                (12022, "douze mille vingt deux"),
                (12060, "douze mille soixante"),
                (12061, "douze mille soixante et un"),
                (12062, "douze mille soixante deux"),
                (12111, "douze mille cent onze"),
                (12121, "douze mille cent vingt et un"),
                (12122, "douze mille cent vingt deux"),
                (99160, "nonante neuf mille cent soixante"),
                (99161, "nonante neuf mille cent soixante et un"),
                (99162, "nonante neuf mille cent soixante deux"),
                (99200, "nonante neuf mille deux cents"),
                (99201, "nonante neuf mille deux cent un"),
                (99221, "nonante neuf mille deux cent vingt et un"),
                (99222, "nonante neuf mille deux cent vingt deux"),
                (99260, "nonante neuf mille deux cent soixante"),
                (99261, "nonante neuf mille deux cent soixante et un"),
                (99262, "nonante neuf mille deux cent soixante deux"),
            };

            var converter = NumberToStringConverter.GetConverter("FR-be");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void BiggerTest()
        {
            (long Number, string Expected)[] tests = new (long Number, string Expected)[] {
                (-401000, "moins quatre cent un mille"),
                (401000, "quatre cent un mille"),
                (999999, "neuf cent nonante neuf mille neuf cent nonante neuf"),
                (1000000, "un million"),
                (999999999, "neuf cent nonante neuf millions neuf cent nonante neuf mille neuf cent nonante neuf"),
            };

            var converter = NumberToStringConverter.GetConverter("FR-be");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

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
