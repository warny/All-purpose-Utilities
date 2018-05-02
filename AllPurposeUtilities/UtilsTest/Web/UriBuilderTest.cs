using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UtilsTest.Web
{
	[TestClass]
	public class UriBuilderTest
	{
		[TestMethod]
		public void ReadSimpleServer()
		{
			var builder = new Utils.Web.UriBuilder("http://example.com");
			Assert.AreEqual("example.com", builder.Host);
			Assert.AreEqual("http", builder.Scheme);
		}

		[TestMethod]
		public void ReadSimpleServerWithAuthentication()
		{
			var builder = new Utils.Web.UriBuilder("https://olivier:marty@example.com");
			Assert.AreEqual("example.com", builder.Host);
			Assert.AreEqual("https", builder.Scheme);
			Assert.AreEqual("olivier", builder.Username);
			Assert.AreEqual("marty", builder.Password);
		}

		[TestMethod]
		public void ReadSimpleServerWithEscapedAuthentication()
		{
			var builder = new Utils.Web.UriBuilder("ftp://%6Flivier:%6Darty@example.com");
			Assert.AreEqual("example.com", builder.Host);
			Assert.AreEqual("ftp", builder.Scheme);
			Assert.AreEqual("olivier", builder.Username);
			Assert.AreEqual("marty", builder.Password);
		}

		[TestMethod]
		public void ReadQueryString()
		{
			var builder = new Utils.Web.UriBuilder("http://example.com?key1=value1&key2=value2");
			Assert.AreEqual("example.com", builder.Host);
			Assert.AreEqual("http", builder.Scheme);
			Assert.AreEqual("value1", builder.QueryString["key1"]);
			Assert.AreEqual("value2", builder.QueryString["key2"]);
		}

		[TestMethod]
		public void ModifyQueryStringAddValue()
		{
			var builder = new Utils.Web.UriBuilder("http://example.com/?key1=value1&key2=value2");
			builder.QueryString.Add("key3", "value3");

			Assert.AreEqual("example.com", builder.Host);
			Assert.AreEqual("http", builder.Scheme);
			Assert.AreEqual("value1", builder.QueryString["key1"]);
			Assert.AreEqual("value2", builder.QueryString["key2"]);
			Assert.AreEqual("value3", builder.QueryString["key3"]);
			Assert.AreEqual("http://example.com/?key1=value1&key2=value2&key3=value3", builder.ToString());
		}

	}
}
