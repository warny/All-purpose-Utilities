using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using System.Text.Json;
using Utils.Net;

namespace UtilsTest.Net;

[TestClass]
public class MimePartConverterTests
{
    [TestMethod]
    public void ConvertTextPart()
    {
        var part = new MimePart { Body = "Hello" };
        part.Headers["Content-Type"] = "text/plain";

        var converter = MimePartConverter.Default;
        Assert.IsTrue(converter.CanConvertTo<string>(MimeType.Parse(part.Headers["Content-Type"]!)));
        Assert.IsTrue(converter.TryConvertTo<string>(part, out var text));
        Assert.AreEqual("Hello", text);
        Assert.IsTrue(converter.TryConvertTo<TextReader>(part, out var reader));
        Assert.AreEqual("Hello", reader.ReadToEnd());
    }

    [TestMethod]
    public void ConvertXmlPart()
    {
        var part = new MimePart { Body = "<root/>" };
        part.Headers["Content-Type"] = "text/xml";
        var converter = MimePartConverter.Default;
        Assert.IsTrue(converter.TryConvertTo<XDocument>(part, out var doc));
        Assert.AreEqual("root", doc.Root!.Name.LocalName);
    }

    [TestMethod]
    public void ConvertBinaryPart()
    {
        var bytes = Encoding.UTF8.GetBytes("abc");
        var part = new MimePart { Body = Convert.ToBase64String(bytes) };
        part.Headers["Content-Type"] = "application/octet-stream";
        var converter = MimePartConverter.Default;
        Assert.IsTrue(converter.TryConvertTo<byte[]>(part, out var arr));
        CollectionAssert.AreEqual(bytes, arr);
        Assert.IsTrue(converter.TryConvertTo<Stream>(part, out var stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        CollectionAssert.AreEqual(bytes, ms.ToArray());
    }

    [TestMethod]
    public void ConvertJsonPart()
    {
        var part = new MimePart { Body = "{\"name\":\"test\"}" };
        part.Headers["Content-Type"] = "application/json";
        var converter = MimePartConverter.Default;
        Assert.IsTrue(converter.CanConvertTo<System.Text.Json.JsonDocument>(MimeType.Parse(part.Headers["Content-Type"]!)));
        Assert.IsTrue(converter.TryConvertTo<System.Text.Json.JsonDocument>(part, out var doc));
        Assert.AreEqual("test", doc.RootElement.GetProperty("name").GetString());
    }
}
