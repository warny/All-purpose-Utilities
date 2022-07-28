﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics;

namespace UtilsTest.Objects
{
	[TestClass]
	public class NumberToStringConverterTest
	{
		[TestMethod]
		public void From1To999Test()
		{
			(long Number, string Expected)[] tests = new (long Number, string Expected)[] {
				(1, "un"),
				(2, "deux"),
				(11, "onze"),
				(20, "vingt"),
				(21, "vingt et un"),
				(22, "vingt deux"),
				(60, "soixante"),
				(61, "soixante et un"),
				(62, "soixante deux"),
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
			
			var converter = new NumberToStringConverter();

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
				(99160, "quatre-vingt dix neuf mille cent soixante"),
				(99161, "quatre-vingt dix neuf mille cent soixante et un"),
				(99162, "quatre-vingt dix neuf mille cent soixante deux"),
				(99200, "quatre-vingt dix neuf mille deux cents"),
				(99201, "quatre-vingt dix neuf mille deux cent un"),
				(99221, "quatre-vingt dix neuf mille deux cent vingt et un"),
				(99222, "quatre-vingt dix neuf mille deux cent vingt deux"),
				(99260, "quatre-vingt dix neuf mille deux cent soixante"),
				(99261, "quatre-vingt dix neuf mille deux cent soixante et un"),
				(99262, "quatre-vingt dix neuf mille deux cent soixante deux"),
			};

			var converter = new NumberToStringConverter();

			foreach (var test in tests)
			{
				Assert.AreEqual(test.Expected, converter.Convert(test.Number));
			}
		}

		[TestMethod]
		public void BiggerTest()
		{
			(long Number, string Expected)[] tests = new (long Number, string Expected)[] {
				(401000, "quatre cent un mille"),
				(999999, "neuf cent quatre-vingt dix neuf mille neuf cent quatre-vingt dix neuf"),
				(999999999, "neuf cent quatre-vingt dix neuf million neuf cent quatre-vingt dix neuf mille neuf cent quatre-vingt dix neuf"),
			};

			var converter = new NumberToStringConverter();

			foreach (var test in tests)
			{
				Assert.AreEqual(test.Expected, converter.Convert(test.Number));
			}
		}

	}
}
