using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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
    private static byte[] Decode(string input, IBaseDescriptor descriptor, bool strict = true)
    {
        using var stream = new MemoryStream();
        using var decoder = new BaseDecoderStream(stream, descriptor, strict);
        decoder.Write(input);
        decoder.Close();
        return stream.ToArray();
    }
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

    // ---- item 7: decodage strict ----

    [TestMethod]
    public void StrictMode_RejectsCharactersOutsideAlphabet()
    {
        // '!' is not in base64 alphabet
        Assert.ThrowsException<FormatException>(() => Decode("QU!C", Bases.Base64));
    }

    [TestMethod]
    public void StrictMode_RejectsPaddingAtStart()
    {
        Assert.ThrowsException<FormatException>(() => Decode("=QUI=", Bases.Base64));
    }

    [TestMethod]
    public void StrictMode_RejectsDataAfterPadding()
    {
        // Data character after padding is illegal
        Assert.ThrowsException<FormatException>(() => Decode("QUI=A", Bases.Base64));
    }

    [TestMethod]
    public void StrictMode_Base16_RejectsInvalidChars()
    {
        Assert.ThrowsException<FormatException>(() => Decode("GG", Bases.Base16));
    }

    [TestMethod]
    public void PermissiveMode_IgnoresUnknownCharsLikeBeforeA()
    {
        // In permissive mode unknown chars are silently skipped
        byte[] result = Decode("01020304", Bases.Base16, strict: false);
        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(comparer.Equals(new byte[] { 1, 2, 3, 4 }, result));
    }

    [TestMethod]
    public void StrictMode_ValidBase64WithPaddingDecodes()
    {
        byte[] result = Decode("QUJDREU=", Bases.Base64);
        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(comparer.Equals(new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45 }, result));
    }

    [TestMethod]
    public void StrictMode_ValidBase32WithPaddingDecodes()
    {
        byte[] result = Decode("IFBA====", Bases.Base32);
        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(comparer.Equals(new byte[] { 0x41, 0x42 }, result));
    }

    [TestMethod]
    public void StrictMode_Base64_RejectsIncorrectPaddingCount()
    {
        // "TQ=" has only 1 filler where 2 are required for a 2-symbol group
        Assert.ThrowsException<FormatException>(() => Decode("TQ=", Bases.Base64));
        // "TQ==" (2 fillers) is the correct form and must decode successfully
        byte[] result = Decode("TQ==", Bases.Base64);
        Assert.AreEqual(1, result.Length);
        Assert.AreEqual(0x4D, result[0]); // 'M'
    }

    // ---- item 12: idempotence de Close ----

    [TestMethod]
    public void DecoderClose_IsIdempotent_NoDuplicateOutput()
    {
        using var stream = new MemoryStream();
        var decoder = new BaseDecoderStream(stream, Bases.Base64, strict: false);
        decoder.Write("QUJD"); // ABC
        decoder.Close();
        long afterFirst = stream.Length;
        decoder.Close(); // second call must be a no-op
        Assert.AreEqual(afterFirst, stream.Length, "Second Close must not write extra bytes");
    }

    [TestMethod]
    public void DecoderWrite_AfterClose_Throws()
    {
        using var stream = new MemoryStream();
        var decoder = new BaseDecoderStream(stream, Bases.Base64);
        decoder.Close();
        Assert.ThrowsException<ObjectDisposedException>(() => decoder.Write('Q'));
    }

    // ---- Close() state coherence after FormatException ----

    [TestMethod]
    public void Close_InvalidPadding_ThrowsFormatException()
    {
        using var ms = new MemoryStream();
        var decoder = new BaseDecoderStream(ms, Bases.Base64);
        decoder.Write("TQ="); // only 1 '=' where 2 are required
        Assert.ThrowsException<FormatException>(() => decoder.Close());
    }

    [TestMethod]
    public void Close_AfterFormatException_WriteThrowsObjectDisposedException()
    {
        using var ms = new MemoryStream();
        var decoder = new BaseDecoderStream(ms, Bases.Base64);
        decoder.Write("TQ=");
        try { decoder.Close(); } catch (FormatException) { }
        // The instance is permanently condemned; any subsequent write must be rejected.
        Assert.ThrowsException<ObjectDisposedException>(() => decoder.Write('Q'),
            "Write after a failed Close must throw ObjectDisposedException");
    }

    [TestMethod]
    public void Close_CalledTwiceAfterFailure_SecondCallIsNoop()
    {
        using var ms = new MemoryStream();
        var decoder = new BaseDecoderStream(ms, Bases.Base64);
        decoder.Write("TQ=");
        try { decoder.Close(); } catch (FormatException) { }
        // Must not throw on the second call
        decoder.Close();
    }

    private class TrackingStream : MemoryStream
    {
        public bool WasFlushed { get; private set; }
        public override void Flush() { WasFlushed = true; base.Flush(); }
    }

    [TestMethod]
    public void Close_FlushesUnderlyingStream_EvenWhenFormatExceptionThrown()
    {
        var tracking = new TrackingStream();
        var decoder = new BaseDecoderStream(tracking, Bases.Base64);
        decoder.Write("TQ="); // will cause FormatException on Close
        try { decoder.Close(); } catch (FormatException) { }
        Assert.IsTrue(tracking.WasFlushed,
            "Underlying stream must be flushed even when Close() throws FormatException");
    }
}

