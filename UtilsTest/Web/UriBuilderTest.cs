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
			Assert.AreEqual("/", builder.AbsolutePath);
		}

		[TestMethod]
		public void ReadSimpleServerWithAuthentication()
		{
			var builder = new Utils.Web.UriBuilder("https://olivier:marty@example.com");
			Assert.AreEqual("example.com", builder.Host);
			Assert.AreEqual("https", builder.Scheme);
			Assert.AreEqual("olivier", builder.Username);
			Assert.AreEqual("marty", builder.Password);
			Assert.AreEqual("/", builder.AbsolutePath);
		}

		[TestMethod]
		public void ReadSimpleServerWithEscapedAuthentication()
		{
			var builder = new Utils.Web.UriBuilder("ftp://%6Flivier:%6Darty@example.com");
			Assert.AreEqual("example.com", builder.Host);
			Assert.AreEqual("ftp", builder.Scheme);
			Assert.AreEqual("olivier", builder.Username);
			Assert.AreEqual("marty", builder.Password);
			Assert.AreEqual("/", builder.AbsolutePath);
		}

		[TestMethod]
		public void ReadQueryString()
		{
			var builder = new Utils.Web.UriBuilder("http://example.com?key1=value1&key2=value2");
			Assert.AreEqual("example.com", builder.Host);
			Assert.AreEqual("http", builder.Scheme);
			Assert.AreEqual("/", builder.AbsolutePath);
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
			Assert.AreEqual("/", builder.AbsolutePath);
			Assert.AreEqual("value1", builder.QueryString["key1"]);
			Assert.AreEqual("value2", builder.QueryString["key2"]);
			Assert.AreEqual("value3", builder.QueryString["key3"]);
			Assert.AreEqual("http://example.com/?key1=value1&key2=value2&key3=value3", builder.ToString());
		}

		[TestMethod]
		public void ModifyQueryStringAddMultipleValues()
		{
			var builder = new Utils.Web.UriBuilder("http://example.com/?key1=value1&key2=value2a&key2=value2b");
			builder.QueryString.Add("key3", "value3a");
			builder.QueryString.Add("key3", "value3b");

			Assert.AreEqual("example.com", builder.Host);
			Assert.AreEqual("http", builder.Scheme);
			Assert.AreEqual("/", builder.AbsolutePath);
			Assert.AreEqual("value1", builder.QueryString["key1"]);
			var values2 = builder.QueryString.GetValues("key2");
			Assert.AreEqual("value2a", values2[0]);
			Assert.AreEqual("value2b", values2[1]);
			var values3 = builder.QueryString.GetValues("key3");
			Assert.AreEqual("value3a", values3[0]);
			Assert.AreEqual("value3b", values3[1]);
			Assert.AreEqual("http://example.com/?key1=value1&key2=value2a&key2=value2b&key3=value3a&key3=value3b", builder.ToString());
		}

		[TestMethod]
		public void ModifyQueryStringReplaceValues()
		{
			var builder = new Utils.Web.UriBuilder("http://example.com/?key1=value1&key2=value2a&key2=value2b");
			builder.QueryString["key2"] = "value2";
			builder.QueryString["key3"] = "value3";

			Assert.AreEqual("example.com", builder.Host);
			Assert.AreEqual("http", builder.Scheme);
			Assert.AreEqual("/", builder.AbsolutePath);
			Assert.AreEqual("value1", builder.QueryString["key1"]);
			var values2 = builder.QueryString.GetValues("key2");
			Assert.AreEqual("value2", values2[0]);
			var values3 = builder.QueryString.GetValues("key3");
			Assert.AreEqual("value3", values3[0]);
			Assert.AreEqual("http://example.com/?key1=value1&key2=value2&key3=value3", builder.ToString());
		}

		[TestMethod]
		public void ModifyQueryStringRemoveMultipleValues()
		{
			var builder = new Utils.Web.UriBuilder("http://example.com/test.html?key1=value1&key2=value2a&key2=value2b&key3=value3a&key3=value3b");
			builder.QueryString.Remove("key2");

			Assert.AreEqual("example.com", builder.Host);
			Assert.AreEqual("http", builder.Scheme);
			Assert.AreEqual("/test.html", builder.AbsolutePath);
			Assert.AreEqual("value1", builder.QueryString["key1"]);
			var values2 = builder.QueryString.GetValues("key2");
			Assert.IsNull(values2);
			var values3 = builder.QueryString.GetValues("key3");
			Assert.AreEqual("value3a", values3[0]);
			Assert.AreEqual("value3b", values3[1]);
			Assert.AreEqual("http://example.com/test.html?key1=value1&key3=value3a&key3=value3b", builder.ToString());
		}

		[TestMethod]
		public void ModifyLoginPassword()
		{
			var builder = new Utils.Web.UriBuilder("http://example.com");
			builder.Username = "username";
			builder.Password = "password";

			Assert.AreEqual("username", builder.Username);
			Assert.AreEqual("password", builder.Password);
			Assert.AreEqual("http://username:password@example.com/", builder.ToString());
		}
	}
}
