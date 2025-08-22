using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Utils.Arrays;
using Utils.IO.BaseEncoding;
using Utils.Collections;

namespace UtilsTest.BaseEncoding;

/// <summary>
/// Tests for <see cref="BaseDecoderStream"/>.
/// </summary>
[TestClass]
public class BaseDecoderStreamTests
{
    /// <summary>
    /// Decodes a simple sequence of hexadecimal characters.
    /// </summary>
    [TestMethod]
    public void Base16Test1()
    {
        string source = "01020304";
        byte[] target = { 1, 2, 3, 4 };

        var stream = new MemoryStream();
        var decoder = new BaseDecoderStream(stream, Bases.Base16);
        decoder.Write(source);
        decoder.Close();

        byte[] result = stream.ToArray();

        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(comparer.Equals(target, result));
    }

    /// <summary>
    /// Decodes hexadecimal characters representing ASCII letters.
    /// </summary>
    [TestMethod]
    public void Base16Test2()
    {
        string source = "4142434445";
        byte[] target = { 0x41, 0x42, 0x43, 0x44, 0x45 };

        var stream = new MemoryStream();
        var decoder = new BaseDecoderStream(stream, Bases.Base16);
        decoder.Write(source);
        decoder.Close();

        byte[] result = stream.ToArray();

        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(comparer.Equals(target, result));
    }

    /// <summary>
    /// Decodes a base-32 sequence without padding.
    /// </summary>
    [TestMethod]
    public void Base32Test1()
    {
        string source = "IFBEGRCF";
        byte[] target = { 0x41, 0x42, 0x43, 0x44, 0x45 };

        var stream = new MemoryStream();
        var decoder = new BaseDecoderStream(stream, Bases.Base32);
        decoder.Write(source);
        decoder.Close();

        byte[] result = stream.ToArray();

        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(comparer.Equals(target, result));
    }

    /// <summary>
    /// Decodes a padded base-32 sequence.
    /// </summary>
    [TestMethod]
    public void Base32Test2()
    {
        string source = "IFBA====";
        byte[] target = { 0x41, 0x42 };

        var stream = new MemoryStream();
        var decoder = new BaseDecoderStream(stream, Bases.Base32);
        decoder.Write(source);
        decoder.Close();

        byte[] result = stream.ToArray();

        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(comparer.Equals(target, result));
    }

    /// <summary>
    /// Decodes a base-64 sequence with padding.
    /// </summary>
    [TestMethod]
    public void Base64Test1()
    {
        string source = "QUJDREU=";
        byte[] target = { 0x41, 0x42, 0x43, 0x44, 0x45 };

        var stream = new MemoryStream();
        var decoder = new BaseDecoderStream(stream, Bases.Base64);
        decoder.Write(source);
        decoder.Close();

        byte[] result = stream.ToArray();

        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(comparer.Equals(target, result));
    }

    /// <summary>
    /// Decodes a base-64 sequence representing two bytes.
    /// </summary>
    [TestMethod]
    public void Base64Test2()
    {
        string source = "QUI=";
        byte[] target = { 0x41, 0x42 };

        var stream = new MemoryStream();
        var decoder = new BaseDecoderStream(stream, Bases.Base64);
        decoder.Write(source);
        decoder.Close();

        byte[] result = stream.ToArray();

        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(comparer.Equals(target, result));
    }
}

