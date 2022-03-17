using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class ObjectFluentTest
	{
		[TestMethod]
		public void IsNullTest()
		{
			var tests = new (object obj, bool expectedResult, object expectedValue)[] {
				(null, true, null),
				("", false, ""),
				(1, false, 1)
			};

			foreach (var test in tests)
			{
				var result = test.obj.IsNull();
				Assert.AreEqual(test.expectedResult, result.Success);
				Assert.AreEqual(test.expectedValue, result.Value);
			}

		}
		
		[TestMethod]
		public void IsNotNullTest()
		{
			var tests = new (object obj, bool expectedResult, object expectedValue)[] {
				(null, false, null),
				("", true, ""),
				(1, true, 1)
			};

			foreach (var test in tests)
			{
				var result = test.obj.IsNotNull();
				Assert.AreEqual(test.expectedResult, result.Success);
				Assert.AreEqual(test.expectedValue, result.Value);
			}
		}

		[TestMethod]
		public void IsNullOrEmptyToNullTest()
		{
			var tests = new (string obj, string expectedValue)[] {
				(null, null),
				("", null),
				("   ", "   "),
				("abcd", "abcd")
			};

			foreach (var test in tests)
			{
				var result = test.obj.NullOrEmptyIsNull();
				Assert.AreEqual(test.expectedValue, result);
			}
		}

		[TestMethod]
		public void IsNullOrWhiteSpaceToNullTest()
		{
			var tests = new (string obj, string expectedValue)[] {
				(null, null),
				("", null),
				("   ", null),
				("abcd", "abcd")
			};

			foreach (var test in tests)
			{
				var result = test.obj.NullOrWhiteSpaceIsNull();
				Assert.AreEqual(test.expectedValue, result);
			}
		}

		[TestMethod]
		public void ComparisonsTest()
		{
			var tests = new (object obj1, object obj2, bool expectedE, bool expectedG, bool expectedGT, bool expectedL, bool expectedLT)[] {
				(1, 1, true, false, true, false, true),
				(1, 2, false, false, false, true, true),
				(2, 1, false, true, true, false, false),
				("a", "a", true, false, true, false, true),
				("a", "b", false, false, false, true, true),
				("b", "a", false, true, true, false, false),
			};

			foreach (var test in tests)
			{
				Assert.AreEqual(test.expectedE, test.obj1.IsEqualTo(test.obj2).Success, $"{test.obj1} == {test.obj2}");
				Assert.AreEqual(test.expectedG, test.obj1.IsGreaterThan(test.obj2).Success, $"{test.obj1} > {test.obj2}");
				Assert.AreEqual(test.expectedGT, test.obj1.IsGreaterOrEqualsThan(test.obj2).Success, $"{test.obj1} >= {test.obj2}");
				Assert.AreEqual(test.expectedL, test.obj1.IsLowerThan(test.obj2).Success, $"{test.obj1} < {test.obj2}");
				Assert.AreEqual(test.expectedLT, test.obj1.IsLowerOrEqualsThan(test.obj2).Success, $"{test.obj1} <= {test.obj2}");
			}

		}


	}
}
