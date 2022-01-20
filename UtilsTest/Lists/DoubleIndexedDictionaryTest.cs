using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Utils.Collections;

namespace UtilsTest.Lists
{
	[TestClass]
	public class DoubleIndexedDictionaryTest
	{
		[TestMethod]
		public void DictionaryTest1()
		{
			DoubleIndexedDictionary<int, string> d = new DoubleIndexedDictionary<int, string>();
			d.Add(1, "A");
			d.Add(2, "B");
			d.Add(3, "C");

			Assert.AreEqual("A", d[1]);
			Assert.AreEqual("B", d[2]);
			Assert.AreEqual("C", d[3]);
			Assert.AreEqual(1, d["A"]);
			Assert.AreEqual(2, d["B"]);
			Assert.AreEqual(3, d["C"]);
			Assert.ThrowsException<KeyNotFoundException>(() => d[4]);
			Assert.ThrowsException<ArgumentException>(() => d.Add(1, "D"));
			Assert.ThrowsException<ArgumentException>(() => d[1] = "B");

			try
			{
				d[4] = "D";
			}
			catch
			{
				Assert.Inconclusive("Can't assign inexisting value to inexisting key");
			}

			try
			{
				d[1] = "Z";
				d[1] = "A";
			}
			catch
			{
				Assert.Inconclusive("Can't assign inexisting value to existing key");
			}

			try
			{
				d[5] = "E";
			}
			catch
			{
				Assert.Inconclusive("Can't assign inexisting value to inexisting key");
			}

			try
			{
				d["A"] = 9999;
				d["A"] = 1;
			}
			catch
			{
				Assert.Inconclusive("Can't assign indexisting value to existing key");
			}

			Assert.AreEqual("A", d[1]);
			Assert.AreEqual("B", d[2]);
			Assert.AreEqual("C", d[3]);
			Assert.AreEqual("D", d[4]);
			Assert.AreEqual("E", d[5]);
			Assert.AreEqual(1, d["A"]);
			Assert.AreEqual(2, d["B"]);
			Assert.AreEqual(3, d["C"]);
			Assert.AreEqual(4, d["D"]);
			Assert.AreEqual(5, d["E"]);


		}
	}
}
