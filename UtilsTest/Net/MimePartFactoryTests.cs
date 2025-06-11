using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;
using System.Xml.Linq;
using System.Text.Json;
using Utils.Net;

namespace UtilsTest.Net;

[TestClass]
public class MimePartFactoryTests
{
    [TestMethod]
    public void CreateTextPart()
    {
        var factory = MimePartFactory.Default;
        Assert.IsTrue(factory.TryCreatePart("Hello", out var part));
        Assert.IsNotNull(part);
        Assert.AreEqual("text/plain", part!.Headers["Content-Type"]);
        Assert.AreEqual("Hello", part.Body);
    }

    [TestMethod]
    public void CreateXmlPart()
    {
        var doc = new XDocument(new XElement("root"));
        var factory = MimePartFactory.Default;
        Assert.IsTrue(factory.TryCreatePart(doc, out var part));
        Assert.AreEqual("text/xml", part!.Headers["Content-Type"]);
        Assert.IsTrue(part.Body.Contains("<root"));
    }

    [TestMethod]
    public void CreateBinaryPart()
    {
        var bytes = Encoding.UTF8.GetBytes("abc");
        var factory = MimePartFactory.Default;
        Assert.IsTrue(factory.TryCreatePart(bytes, out var part));
        Assert.AreEqual("application/octet-stream", part!.Headers["Content-Type"]);
        CollectionAssert.AreEqual(bytes, Convert.FromBase64String(part.Body));
    }

    [TestMethod]
    public void CreateJsonPart()
    {
        using var doc = JsonDocument.Parse("{\"name\":\"test\"}");
        var factory = MimePartFactory.Default;
        Assert.IsTrue(factory.TryCreatePart(doc, out var part));
        Assert.AreEqual("application/json", part!.Headers["Content-Type"]);
        using var parsed = JsonDocument.Parse(part.Body);
        Assert.AreEqual("test", parsed.RootElement.GetProperty("name").GetString());
    }
}
