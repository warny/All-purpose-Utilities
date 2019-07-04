using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Arrays;

namespace UtilsTest.Array
{
	[TestClass]
	public class ArrayUtilsTest
	{
		[TestMethod]
		public void TestTrim()
		{
			var array = "abcdefghijklmnopqrstuvwxyz".ToArray();
			var result = array.Trim('y', 'z', 'a', 'b', 'm', 'n');
			string resultString = new string(result);

			Assert.AreEqual("cdefghijklmnopqrstuvwx", resultString);
		}

		[TestMethod]
		public void TestTrimLeft()
		{
			var array = "abcdefghijklmnopqrstuvwxyz".ToArray();
			var result = array.TrimStart('y', 'z', 'a', 'b', 'm', 'n');
			string resultString = new string(result);
			Assert.AreEqual("cdefghijklmnopqrstuvwxyz", resultString);
		}

		[TestMethod]
		public void TestTrimRight()
		{
			var array = "abcdefghijklmnopqrstuvwxyz".ToArray();
			var result = array.TrimEnd('y', 'z', 'a', 'b', 'm', 'n');
			string resultString = new string(result);
			Assert.AreEqual("abcdefghijklmnopqrstuvwx", resultString);
		}

	}
}
