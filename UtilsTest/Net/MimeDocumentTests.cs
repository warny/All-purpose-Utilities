using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Utils.Net;

namespace UtilsTest.Net;

[TestClass]
public class MimeDocumentTests
{
        [TestMethod]
        public void ParseAndWriteSimpleMultipartDocument()
        {
                const string input = "Content-Type: multipart/mixed; boundary=\"b\"\n\n--b\nContent-Type: text/plain\n\nHello\n--b\nContent-Type: text/plain\n\nWorld\n--b--\n";
                var doc = MimeReader.Read(input);
                Assert.AreEqual(2, doc.Parts.Count);
                Assert.AreEqual("Hello\n", doc.Parts[0].Body);
                Assert.AreEqual("World\n", doc.Parts[1].Body);

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
}
