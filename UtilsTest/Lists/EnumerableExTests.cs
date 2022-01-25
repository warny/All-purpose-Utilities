using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace UtilsTest.Lists
{
	[TestClass]
	public class EnumerableExTests
	{
		[TestMethod]
		public void GetValueOrDefaultTest()
		{
			Dictionary<string, string> test = new Dictionary<string, string>();
			test.Add("a", "1");
			test.Add("b", "2");
			test.Add("c", "3");
			test.Add("d", "4");

			Assert.AreEqual("2", test.GetValueOrDefault("b", "0"));
			Assert.AreEqual("0", test.GetValueOrDefault("e", "0"));
		}
	}
}
