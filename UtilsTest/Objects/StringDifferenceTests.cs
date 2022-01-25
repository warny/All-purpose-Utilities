using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class StringDifferenceTests
	{
		[TestMethod]
		public void Equality()
		{
			string string1 = "abcdef";
			string string2 = "abcdef";

			StringDifference difference = new StringDifference(string1, string2);

			var results = difference.ToArray();

			Assert.AreEqual(1, results.Length);

			Assert.AreEqual(results[0].Status, StringComparisonStatus.Unchanged);
			Assert.AreEqual(results[0].String, string1);
		}

		[TestMethod]
		public void AddToEnd()
		{
			string string1 = "abcdef";
			string string2 = "abcdef123";

			StringDifference difference = new StringDifference(string1, string2);

			var results = difference.ToArray();

			Assert.AreEqual(2, results.Length);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[0].Status);
			Assert.AreEqual("abcdef", results[0].String);

			Assert.AreEqual(StringComparisonStatus.Added, results[1].Status);
			Assert.AreEqual("123", results[1].String);
		}

		[TestMethod]
		public void AddToStart()
		{
			string string1 = "abcdef";
			string string2 = "123abcdef";

			StringDifference difference = new StringDifference(string1, string2);

			var results = difference.ToArray();

			Assert.AreEqual(2, results.Length);

			Assert.AreEqual(StringComparisonStatus.Added, results[0].Status);
			Assert.AreEqual("123", results[0].String);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[1].Status);
			Assert.AreEqual("abcdef", results[1].String);

		}

		[TestMethod]
		public void AddToMiddle()
		{
			string string1 = "abcdef";
			string string2 = "abc123def";

			StringDifference difference = new StringDifference(string1, string2);

			var results = difference.ToArray();

			Assert.AreEqual(3, results.Length);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[0].Status);
			Assert.AreEqual("abc", results[0].String);

			Assert.AreEqual(StringComparisonStatus.Added, results[1].Status);
			Assert.AreEqual("123", results[1].String);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[2].Status);
			Assert.AreEqual("def", results[2].String);
		}


		[TestMethod]
		public void RemoveFromEnd()
		{
			string string1 = "abcdef123";
			string string2 = "abcdef";

			StringDifference difference = new StringDifference(string1, string2);

			var results = difference.ToArray();

			Assert.AreEqual(2, results.Length);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[0].Status);
			Assert.AreEqual("abcdef", results[0].String);

			Assert.AreEqual(StringComparisonStatus.Removed, results[1].Status);
			Assert.AreEqual("123", results[1].String);
		}

		[TestMethod]
		public void RemoveFromStart()
		{
			string string1 = "123abcdef";
			string string2 = "abcdef";

			StringDifference difference = new StringDifference(string1, string2);

			var results = difference.ToArray();

			Assert.AreEqual(2, results.Length);

			Assert.AreEqual(StringComparisonStatus.Removed, results[0].Status);
			Assert.AreEqual("123", results[0].String);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[1].Status);
			Assert.AreEqual("abcdef", results[1].String);

		}

		[TestMethod]
		public void RemoveFromMiddle()
		{
			string string1 = "abc123def";
			string string2 = "abcdef";

			StringDifference difference = new StringDifference(string1, string2);

			var results = difference.ToArray();

			Assert.AreEqual(3, results.Length);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[0].Status);
			Assert.AreEqual("abc", results[0].String);

			Assert.AreEqual(StringComparisonStatus.Removed, results[1].Status);
			Assert.AreEqual("123", results[1].String);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[2].Status);
			Assert.AreEqual("def", results[2].String);
		}


		[TestMethod]
		public void ReplaceInEnd()
		{
			string string1 = "abcdef123";
			string string2 = "abcdef456";

			StringDifference difference = new StringDifference(string1, string2);

			var results = difference.ToArray();

			Assert.AreEqual(3, results.Length);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[0].Status);
			Assert.AreEqual("abcdef", results[0].String);

			Assert.AreEqual(StringComparisonStatus.Added, results[1].Status);
			Assert.AreEqual("456", results[1].String);

			Assert.AreEqual(StringComparisonStatus.Removed, results[2].Status);
			Assert.AreEqual("123", results[2].String);
		}

		[TestMethod]
		public void ReplaceInStart()
		{
			string string1 = "123abcdef";
			string string2 = "456abcdef";

			StringDifference difference = new StringDifference(string1, string2);

			var results = difference.ToArray();

			Assert.AreEqual(3, results.Length);

			Assert.AreEqual(StringComparisonStatus.Added, results[0].Status);
			Assert.AreEqual("456", results[0].String);

			Assert.AreEqual(StringComparisonStatus.Removed, results[1].Status);
			Assert.AreEqual("123", results[1].String);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[2].Status);
			Assert.AreEqual("abcdef", results[2].String);

		}


		[TestMethod]
		public void ReplacedInMiddle()
		{
			string string1 = "abc123def";
			string string2 = "abc456def";

			StringDifference difference = new StringDifference(string1, string2);

			var results = difference.ToArray();

			Assert.AreEqual(4, results.Length);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[0].Status);
			Assert.AreEqual("abc", results[0].String);

			Assert.AreEqual(StringComparisonStatus.Added, results[1].Status);
			Assert.AreEqual("456", results[1].String);

			Assert.AreEqual(StringComparisonStatus.Removed, results[2].Status);
			Assert.AreEqual("123", results[2].String);

			Assert.AreEqual(StringComparisonStatus.Unchanged, results[3].Status);
			Assert.AreEqual("def", results[3].String);
		}

	}
}
