using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using Utils.Net;

namespace UtilsTest.Net;

[TestClass]
public class MimeDocumentTests
{
    [TestMethod]
    public void ParseAndWriteSimpleMultipartDocument()
    {
        const string input =
            """
					Content-Type: multipart/mixed; boundary="b"

					--b
					Content-Type: text/plain

					Hello
					--b
					Content-Type: text/plain

					World
					--b--

					""";
        var doc = MimeReader.Read(input.ReplaceLineEndings());
        Assert.AreEqual(2, doc.Parts.Count);
        Assert.AreEqual("Hello" + Environment.NewLine, doc.Parts[0].Body);
        Assert.AreEqual("World" + Environment.NewLine, doc.Parts[1].Body);

        using var ms = new MemoryStream();
        MimeWriter.Write(doc, ms);
        ms.Position = 0;
        using var reader = new StreamReader(ms);
        var output = reader.ReadToEnd();
        var doc2 = MimeReader.Read(output);
        Assert.AreEqual(2, doc2.Parts.Count);
        Assert.AreEqual(doc.Parts[0].Body, doc2.Parts[0].Body);
        Assert.AreEqual(doc.Parts[1].Body, doc2.Parts[1].Body);
    }

    [TestMethod]
    public void MimeTypeParseToString()
    {
        var mt = MimeType.Parse("text/plain; charset=utf-8");
        Assert.AreEqual("text", mt.Type);
        Assert.AreEqual("plain", mt.SubType);
        Assert.IsTrue(mt.TryGetParameter("charset", out var cs));
        Assert.AreEqual("utf-8", cs);
        Assert.AreEqual("text/plain; charset=utf-8", mt.ToString());
    }

    [TestMethod]
    public void MimeTypeEqualityOperators()
    {
        var a = MimeType.Parse("text/plain; charset=utf-8");
        var b = MimeType.Parse("text/plain; charset=utf-8");
        var c = MimeType.Parse("text/plain; charset=ascii");

        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
        Assert.IsFalse(a == c);
    }

    [TestMethod]
    public void MimePartEqualityOperators()
    {
        var p1 = new MimePart();
        p1.Headers["Content-Type"] = "text/plain";
        p1.Body = "Hello";

        var p2 = new MimePart(p1);

        Assert.IsTrue(p1 == p2);
        p2.Body = "World";
        Assert.IsTrue(p1 != p2);
    }

    [TestMethod]
    public void MimeTypeStaticFactories()
    {
        var plain = MimeType.CreateTextPlain();
        Assert.AreEqual("text", plain.Type);
        Assert.AreEqual("plain", plain.SubType);
        Assert.IsTrue(plain.TryGetParameter("charset", out var cs1));
        Assert.AreEqual("utf-8", cs1);

        var json = MimeType.CreateApplicationJson();
        Assert.AreEqual("application", json.Type);
        Assert.AreEqual("json", json.SubType);
        Assert.IsTrue(json.TryGetParameter("charset", out var cs2));
        Assert.AreEqual("utf-8", cs2);

        var multi = MimeType.CreateMultipart("mixed", "b");
        Assert.AreEqual("multipart", multi.Type);
        Assert.AreEqual("mixed", multi.SubType);
        Assert.IsTrue(multi.TryGetParameter("boundary", out var b));
        Assert.AreEqual("b", b);
    }

    [TestMethod]
    public void MimePartConverterText()
    {
        var part = new MimePart
        {
            Body = "Hello",
        };
        part.Headers["Content-Type"] = "text/plain";

        var conv = MimePartConverter.Default;
        Assert.IsTrue(conv.TryConvertTo<string>(part, out var str));
        Assert.AreEqual("Hello", str);
        Assert.IsTrue(conv.TryConvertTo<TextReader>(part, out var reader));
        Assert.AreEqual("Hello", reader.ReadToEnd());

        var mime = MimeType.Parse(part.Headers["Content-Type"]);
        Assert.IsTrue(mime.IsCompatibleWith<string>());
        Assert.IsTrue(mime.IsCompatibleWith<TextReader>());
        Assert.IsFalse(mime.IsCompatibleWith<byte[]>());
    }

    [TestMethod]
    public void MimePartConverterBinary()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("Hello");
        var part = new MimePart
        {
            Body = Convert.ToBase64String(bytes),
        };
        part.Headers["Content-Type"] = "application/octet-stream";

        var conv = MimePartConverter.Default;
        Assert.IsTrue(conv.TryConvertTo<byte[]>(part, out var arr));
        CollectionAssert.AreEqual(bytes, arr);
        Assert.IsTrue(conv.TryConvertTo<Stream>(part, out var stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        CollectionAssert.AreEqual(bytes, ms.ToArray());

        var mime = MimeType.Parse(part.Headers["Content-Type"]);
        Assert.IsTrue(mime.IsCompatibleWith<byte[]>());
        Assert.IsTrue(mime.IsCompatibleWith<Stream>());
        Assert.IsFalse(mime.IsCompatibleWith<string>());
    }

    [TestMethod]
    public void MimePartConverterMultipart()
    {
        var child = new MimeDocument();
        var cp = new MimePart();
        cp.Headers["Content-Type"] = "text/plain";
        cp.Body = "Hi";
        child.Parts.Add(cp);
        child.Headers["Content-Type"] = "multipart/mixed; boundary=bb";

        var text = MimeWriter.Write(child);

        var part = new MimePart
        {
            Body = text
        };
        part.Headers["Content-Type"] = "multipart/mixed; boundary=bb";

        var conv = MimePartConverter.Default;
        Assert.IsTrue(conv.TryConvertTo<MimeDocument>(part, out var doc));
        Assert.AreEqual(1, doc.Parts.Count);

        var mime = MimeType.Parse(part.Headers["Content-Type"]);
        Assert.IsTrue(mime.IsCompatibleWith<MimeDocument>());
        Assert.IsFalse(mime.IsCompatibleWith<byte[]>());
    }
}
