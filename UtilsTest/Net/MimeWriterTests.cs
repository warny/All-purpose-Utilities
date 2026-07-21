using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="MimeWriter"/> focusing on the ASCII-compatibility guard added in #23.
/// </summary>
[TestClass]
public class MimeWriterTests
{
    // ------------------------------------------------------------------ helpers

    private static MimeDocument CreateSimpleDocument()
    {
        var doc = new MimeDocument();
        doc.Headers["Content-Type"] = "text/plain";
        var part = new MimePart();
        part.Headers["Content-Type"] = "text/plain; charset=utf-8";
        part.Body = "Hello, MIME!";
        doc.Parts.Add(part);
        return doc;
    }

    // ------------------------------------------------------------------ #23 ASCII-compatibility guard

    [TestMethod]
    public void Write_WithUtf8Encoding_Succeeds()
    {
        var doc = CreateSimpleDocument();
        using var ms = new MemoryStream();
        // UTF-8 is ASCII-compatible — must not throw.
        MimeWriter.Write(doc, ms, Encoding.UTF8);
        Assert.IsTrue(ms.Length > 0, "No bytes were written.");
    }

    [TestMethod]
    public void Write_WithNullEncoding_DefaultsToUtf8AndSucceeds()
    {
        var doc = CreateSimpleDocument();
        using var ms = new MemoryStream();
        MimeWriter.Write(doc, ms);  // encoding = null → UTF-8
        Assert.IsTrue(ms.Length > 0, "No bytes were written with default encoding.");
    }

    [TestMethod]
    public void Write_WithAsciiEncoding_Succeeds()
    {
        var doc = CreateSimpleDocument();
        using var ms = new MemoryStream();
        MimeWriter.Write(doc, ms, Encoding.ASCII);
        Assert.IsTrue(ms.Length > 0);
    }

    [TestMethod]
    public void Write_WithUtf16Encoding_ThrowsArgumentException()
    {
        // UTF-16 (Unicode) is not ASCII-compatible: 'A' encodes as two bytes (0x41 0x00).
        // It must be rejected with ArgumentException (#23).
        var doc = CreateSimpleDocument();
        using var ms = new MemoryStream();
        Assert.ThrowsExactly<ArgumentException>(() => MimeWriter.Write(doc, ms, Encoding.Unicode));
    }

    [TestMethod]
    public void Write_WithUtf32Encoding_ThrowsArgumentException()
    {
        var doc = CreateSimpleDocument();
        using var ms = new MemoryStream();
        Assert.ThrowsExactly<ArgumentException>(() => MimeWriter.Write(doc, ms, Encoding.UTF32));
    }

    [TestMethod]
#pragma warning disable SYSLIB0001 // UTF-7 is obsolete but must still be rejected
    public void Write_WithUtf7Encoding_ThrowsArgumentException()
    {
        // UTF-7 preserves most ASCII code points but encodes '+' (0x2B) differently,
        // which corrupts MIME boundary markers. The full-range check must reject it (#23).
        var doc = CreateSimpleDocument();
        using var ms = new MemoryStream();
        Assert.ThrowsExactly<ArgumentException>(() => MimeWriter.Write(doc, ms, Encoding.UTF7));
    }
#pragma warning restore SYSLIB0001

    [TestMethod]
    public void Write_ToStream_ProducesValidAsciiFraming()
    {
        // The boundary, header names, and colon-separated values must all be pure ASCII bytes.
        var doc = CreateSimpleDocument();
        using var ms = new MemoryStream();
        MimeWriter.Write(doc, ms, Encoding.UTF8);

        ms.Position = 0;
        string text = new StreamReader(ms, Encoding.UTF8).ReadToEnd();

        // Every byte in MIME framing lines must be in the printable ASCII range.
        foreach (char ch in text)
        {
            if (ch == '\r' || ch == '\n') continue;
            // Only the body content may venture outside ASCII in a properly encoded MIME doc;
            // for our simple ASCII body, all characters must be <= 0x7E.
            Assert.IsTrue(ch <= 0x7E,
                $"Non-ASCII character U+{(int)ch:X4} found in MIME output.");
        }
    }
}
