using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Utils.IO.BaseEncoding;

namespace UtilsTest.BaseEncoding;

/// <summary>
/// Tests for <see cref="BaseEncoderStream"/>.
/// </summary>
[TestClass]
public class BaseEncoderStreamTests
{
    /// <summary>
    /// Encodes a simple byte sequence to hexadecimal.
    /// </summary>
    [TestMethod]
    public void Base16Test1()
    {
        byte[] source = { 1, 2, 3, 4 };
        string target = "01020304";

        var stringWriter = new StringWriter();
        var stream = new BaseEncoderStream(stringWriter, Bases.Base16);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual(target, stringWriter.ToString());
    }

    /// <summary>
    /// Encodes ASCII bytes to hexadecimal.
    /// </summary>
    [TestMethod]
    public void Base16Test2()
    {
        byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };
        string target = "4142434445";

        var stringWriter = new StringWriter();
        var stream = new BaseEncoderStream(stringWriter, Bases.Base16);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual(target, stringWriter.ToString());
    }

    /// <summary>
    /// Encodes bytes to base-32 without padding.
    /// </summary>
    [TestMethod]
    public void Base32Test1()
    {
        byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };
        string target = "IFBEGRCF";

        var stringWriter = new StringWriter();
        var stream = new BaseEncoderStream(stringWriter, Bases.Base32);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual(target, stringWriter.ToString());
    }

    /// <summary>
    /// Encodes bytes to base-32 with padding.
    /// </summary>
    [TestMethod]
    public void Base32Test2()
    {
        byte[] source = { 0x41, 0x42 };
        string target = "IFBA====";

        var stringWriter = new StringWriter();
        var stream = new BaseEncoderStream(stringWriter, Bases.Base32);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual(target, stringWriter.ToString());
    }

    /// <summary>
    /// Encodes three bytes to a padded base-32 string.
    /// </summary>
    [TestMethod]
    public void Base32Test3()
    {
        byte[] source = { 0x41, 0x42, 0x43 };
        string target = "IFBEG===";

        var stringWriter = new StringWriter();
        var stream = new BaseEncoderStream(stringWriter, Bases.Base32);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual(target, stringWriter.ToString());
    }

    /// <summary>
    /// Encodes bytes to base-64 with padding.
    /// </summary>
    [TestMethod]
    public void Base64Test1()
    {
        byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };
        string target = "QUJDREU=";

        var stringWriter = new StringWriter();
        var stream = new BaseEncoderStream(stringWriter, Bases.Base64);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual(target, stringWriter.ToString());
    }

    /// <summary>
    /// Encodes two bytes to base-64 with padding.
    /// </summary>
    [TestMethod]
    public void Base64Test2()
    {
        byte[] source = { 0x41, 0x42 };
        string target = "QUI=";

        var stringWriter = new StringWriter();
        var stream = new BaseEncoderStream(stringWriter, Bases.Base64);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual(target, stringWriter.ToString());
    }

    /// <summary>
    /// Encodes three bytes to base-64 without padding.
    /// </summary>
    [TestMethod]
    public void Base64Test3()
    {
        byte[] source = { 0x41, 0x42, 0x43 };
        string target = "QUJD";

        var stringWriter = new StringWriter();
        var stream = new BaseEncoderStream(stringWriter, Bases.Base64);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual(target, stringWriter.ToString());
    }

    // ---- item 12: idempotence de Close ----

    [TestMethod]
    public void EncoderClose_IsIdempotent_NoDuplicateOutput()
    {
        byte[] source = { 0x41, 0x42 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64);
        stream.Write(source, 0, source.Length);
        stream.Close();
        string after1 = sw.ToString();
        stream.Close(); // second close must be a no-op
        string after2 = sw.ToString();
        Assert.AreEqual(after1, after2, "Second Close must not emit extra output");
    }

    [TestMethod]
    public void EncoderWrite_AfterClose_Throws()
    {
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64);
        stream.Close();
        Assert.ThrowsException<ObjectDisposedException>(() => stream.Write(new byte[] { 1 }, 0, 1));
    }

    // ---- item 13: off-by-one du wrapping ----

    [TestMethod]
    public void LineWrapping_ExactWidth_DoesNotExceedLimit()
    {
        // With maxDataWidth=4, each line should have exactly 4 chars
        byte[] source = new byte[6]; // encodes to 8 base64 chars (without padding)
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base16, maxDataWidth: 4);
        stream.Write(source, 0, source.Length);
        stream.Close();
        string output = sw.ToString();
        string[] lines = output.Split(System.Environment.NewLine, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
            Assert.IsTrue(line.Length <= 4, $"Line '{line}' exceeds maxDataWidth=4");
    }

    // ---- item 30: span, async, disposal ----

    [TestMethod]
    public void WriteSpan_ProducesSameOutputAsArray()
    {
        byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64);
        stream.Write(new ReadOnlySpan<byte>(source));
        stream.Close();
        Assert.AreEqual("QUJDREU=", sw.ToString());
    }

    [TestMethod]
    public async Task WriteAsync_ProducesSameOutputAsArray()
    {
        byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64);
        await stream.WriteAsync(source, 0, source.Length, CancellationToken.None);
        stream.Close();
        Assert.AreEqual("QUJDREU=", sw.ToString());
    }

    [TestMethod]
    public async Task FragmentedWrites_GiveSameOutputAsSingleWrite()
    {
        byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };

        var swSingle = new StringWriter();
        var single = new BaseEncoderStream(swSingle, Bases.Base64);
        single.Write(source, 0, source.Length);
        single.Close();

        var swFragmented = new StringWriter();
        var fragmented = new BaseEncoderStream(swFragmented, Bases.Base64);
        await fragmented.WriteAsync(source, 0, 2, CancellationToken.None);
        await fragmented.WriteAsync(source, 2, 3, CancellationToken.None);
        fragmented.Close();

        Assert.AreEqual(swSingle.ToString(), swFragmented.ToString());
    }

    [TestMethod]
    public async Task FlushAsync_DoesNotThrow()
    {
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64);
        await stream.WriteAsync(new byte[] { 1, 2, 3 }, 0, 3, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
        stream.Close();
    }

    [TestMethod]
    public async Task DisposeAsync_FinalizesEncoding()
    {
        byte[] source = { 0x41, 0x42 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64);
        stream.Write(source, 0, source.Length);
        await stream.DisposeAsync();
        // DisposeAsync must have finalized (padded) the output through Close.
        Assert.AreEqual("QUI=", sw.ToString());
    }

    [TestMethod]
    public async Task WriteAsync_AfterClose_Throws()
    {
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64);
        stream.Close();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
            () => stream.WriteAsync(new byte[] { 1 }, 0, 1, CancellationToken.None));
    }

    [TestMethod]
    public async Task WriteAsync_CancelledBeforeCall_ThrowsAndLeavesStateUnchanged()
    {
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => stream.WriteAsync(new byte[] { 1, 2, 3 }, 0, 3, cts.Token));
        Assert.AreEqual(0, sw.ToString().Length, "No output must be produced when cancelled before encoding.");
    }

    [TestMethod]
    public async Task DisposeAsync_IsIdempotent()
    {
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64);
        stream.Write(new byte[] { 0x41 }, 0, 1);
        await stream.DisposeAsync();
        // Second call must not throw ObjectDisposedException or any other exception.
        await stream.DisposeAsync();
    }

    // ---- IO-12: constructor validation of maxDataWidth/indent ----

    /// <summary>Verifies every accepted <c>maxDataWidth</c>/<c>indent</c> combination constructs without throwing.</summary>
    [TestMethod]
    public void Constructor_AcceptsValidMaxDataWidthAndIndent()
    {
        using var sw = new StringWriter();
        _ = new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: -1);
        _ = new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: 1);
        _ = new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: int.MaxValue);
        _ = new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: 4, indent: 0);
        _ = new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: 4, indent: 2);
        // Indent has no observable effect when wrapping is disabled, but must still be accepted.
        _ = new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: -1, indent: 3);
    }

    /// <summary>Verifies <c>maxDataWidth</c> values other than -1 or a positive integer fail at construction time.</summary>
    [TestMethod]
    public void Constructor_RejectsInvalidMaxDataWidth()
    {
        using var sw = new StringWriter();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: -2));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: int.MinValue));
    }

    /// <summary>Verifies a negative <c>indent</c> fails at construction time, not on the first wrap.</summary>
    [TestMethod]
    public void Constructor_RejectsNegativeIndent()
    {
        using var sw = new StringWriter();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: 4, indent: -1));
    }

    // ---- IO-12: separator-between-lines formatting contract ----

    /// <summary>Verifies output that exactly fills one line has no trailing separator.</summary>
    [TestMethod]
    public void LineWrapping_ExactWidth_NoTrailingSeparator()
    {
        byte[] source = { 0x01, 0x02 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base16, maxDataWidth: 4);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual("0102", sw.ToString());
    }

    /// <summary>Verifies two exact full lines are joined by exactly one separator, with none trailing.</summary>
    [TestMethod]
    public void LineWrapping_TwoFullLines_ExactlyOneSeparatorBetweenThem()
    {
        byte[] source = { 0x01, 0x02, 0x03, 0x04 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base16, maxDataWidth: 4);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual("0102" + Bases.Base16.Separator + "0304", sw.ToString());
    }

    /// <summary>Verifies a full line followed by a shorter final line is joined by exactly one separator.</summary>
    [TestMethod]
    public void LineWrapping_PartialFinalLine_ExactlyOneSeparator()
    {
        byte[] source = { 0x01, 0x02, 0x03 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base16, maxDataWidth: 4);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual("0102" + Bases.Base16.Separator + "03", sw.ToString());
    }

    /// <summary>Verifies indentation is written only after a real inter-line separator, never at the start or end of the output.</summary>
    [TestMethod]
    public void LineWrapping_Indent_AppearsOnlyAfterSeparator()
    {
        byte[] source = { 0x01, 0x02, 0x03, 0x04 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base16, maxDataWidth: 4, indent: 2);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual("0102" + Bases.Base16.Separator + "  0304", sw.ToString());
    }

    /// <summary>Verifies width 1 separates every character with exactly one separator and never trails.</summary>
    [TestMethod]
    public void LineWrapping_WidthOne_SeparatesEveryCharacter()
    {
        byte[] source = { 0x01 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base16, maxDataWidth: 1);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual("0" + Bases.Base16.Separator + "1", sw.ToString());
    }

    /// <summary>Verifies a very large width behaves as effectively unwrapped for a realistic input.</summary>
    [TestMethod]
    public void LineWrapping_VeryLargeWidth_BehavesAsUnwrapped()
    {
        byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base16, maxDataWidth: int.MaxValue);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual("4142434445", sw.ToString());
    }

    /// <summary>
    /// Verifies the final residual symbol written by <see cref="BaseEncoderStream.Close"/> participates in
    /// wrapping exactly like an ordinary symbol: two ordinary symbols fill the line, and the final partial
    /// symbol alone starts a new line.
    /// </summary>
    [TestMethod]
    public void LineWrapping_FinalResidualSymbol_ParticipatesInWrapping()
    {
        byte[] source = { 0x41, 0x42 }; // Base64("AB") = "QUI=": Q,U are ordinary symbols; I is the final residual symbol; = is filler.
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: 2);
        stream.Write(source, 0, source.Length);
        stream.Close();
        Assert.AreEqual("QU" + Bases.Base64.Separator + "I=", sw.ToString());
    }

    /// <summary>Verifies filler/padding characters obey the same line-width contract and may span multiple lines.</summary>
    [TestMethod]
    public void LineWrapping_Padding_ParticipatesInWrapping()
    {
        byte[] source = { 0x41, 0x42 }; // Base32("AB") = "IFBA====" unwrapped.
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base32, maxDataWidth: 3);
        stream.Write(source, 0, source.Length);
        stream.Close();
        string separator = Bases.Base32.Separator;
        Assert.AreEqual("IFB" + separator + "A==" + separator + "==", sw.ToString());
    }

    /// <summary>Verifies an empty input with finite width and nonzero indent produces no output whatsoever.</summary>
    [TestMethod]
    public void LineWrapping_EmptyInput_ProducesEmptyOutput()
    {
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: 4, indent: 2);
        stream.Close();
        Assert.AreEqual(string.Empty, sw.ToString());
    }

    /// <summary>Verifies wrapping state is independent of how the input is split across multiple <c>Write</c> calls.</summary>
    [TestMethod]
    public void LineWrapping_FragmentedWrites_MatchSingleWrite()
    {
        byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };

        var swSingle = new StringWriter();
        var single = new BaseEncoderStream(swSingle, Bases.Base64, maxDataWidth: 3);
        single.Write(source, 0, source.Length);
        single.Close();

        var swFragmented = new StringWriter();
        var fragmented = new BaseEncoderStream(swFragmented, Bases.Base64, maxDataWidth: 3);
        fragmented.Write(source, 0, 1);
        fragmented.Write(source, 1, 1);
        fragmented.Write(source, 2, 3);
        fragmented.Close();

        Assert.AreEqual(swSingle.ToString(), swFragmented.ToString());
    }

    /// <summary>Verifies wrapped, padded output still decodes back to the original bytes through <see cref="BaseDecoderStream"/>.</summary>
    [TestMethod]
    public void LineWrapping_WrappedPaddedOutput_RoundTripsThroughDecoder()
    {
        byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base64, maxDataWidth: 3, indent: 2);
        stream.Write(source, 0, source.Length);
        stream.Close();

        using var target = new MemoryStream();
        using (var decoder = new BaseDecoderStream(target, Bases.Base64))
        {
            decoder.Write(sw.ToString());
            decoder.Flush();
        }

        CollectionAssert.AreEqual(source, target.ToArray());
    }

    /// <summary>Verifies a second <see cref="BaseEncoderStream.Close"/> on wrapped, padded output is a strict no-op.</summary>
    [TestMethod]
    public void LineWrapping_Close_IsIdempotent()
    {
        byte[] source = { 0x41, 0x42 };
        var sw = new StringWriter();
        var stream = new BaseEncoderStream(sw, Bases.Base32, maxDataWidth: 3, indent: 1);
        stream.Write(source, 0, source.Length);
        stream.Close();
        string after1 = sw.ToString();
        stream.Close();
        Assert.AreEqual(after1, sw.ToString(), "A second Close must not add filler, separator, indentation or duplicate the final symbol.");
    }

    /// <summary>Verifies <see cref="BaseEncoderStream.DisposeAsync"/> finalizes wrapped output with the same formatting contract as <see cref="BaseEncoderStream.Close"/>.</summary>
    [TestMethod]
    public async Task LineWrapping_DisposeAsync_UsesSameFormattingContractAsClose()
    {
        byte[] source = { 0x41, 0x42 };

        var swClose = new StringWriter();
        var streamClose = new BaseEncoderStream(swClose, Bases.Base64, maxDataWidth: 2);
        streamClose.Write(source, 0, source.Length);
        streamClose.Close();

        var swDisposeAsync = new StringWriter();
        var streamDisposeAsync = new BaseEncoderStream(swDisposeAsync, Bases.Base64, maxDataWidth: 2);
        streamDisposeAsync.Write(source, 0, source.Length);
        await streamDisposeAsync.DisposeAsync();

        Assert.AreEqual(swClose.ToString(), swDisposeAsync.ToString());
    }

    // ---- IO-13: unsupported operations throw NotSupportedException ----

    /// <summary>Verifies setting <see cref="Stream.Position"/> throws <see cref="NotSupportedException"/>, matching <c>CanSeek == false</c>.</summary>
    [TestMethod]
    public void PositionSetter_ThrowsNotSupportedException()
    {
        using var sw = new StringWriter();
        using var stream = new BaseEncoderStream(sw, Bases.Base64);
        Assert.ThrowsException<NotSupportedException>(() => stream.Position = 0);
    }

    /// <summary>Verifies <see cref="Stream.Read(byte[], int, int)"/> throws <see cref="NotSupportedException"/>, matching <c>CanRead == false</c>.</summary>
    [TestMethod]
    public void Read_ThrowsNotSupportedException()
    {
        using var sw = new StringWriter();
        using var stream = new BaseEncoderStream(sw, Bases.Base64);
        Assert.ThrowsException<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
    }

    /// <summary>Verifies <see cref="Stream.Seek"/> throws <see cref="NotSupportedException"/>, matching <c>CanSeek == false</c>.</summary>
    [TestMethod]
    public void Seek_ThrowsNotSupportedException()
    {
        using var sw = new StringWriter();
        using var stream = new BaseEncoderStream(sw, Bases.Base64);
        Assert.ThrowsException<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    /// <summary>Verifies <see cref="Stream.SetLength"/> throws <see cref="NotSupportedException"/>, matching <c>CanSeek == false</c>.</summary>
    [TestMethod]
    public void SetLength_ThrowsNotSupportedException()
    {
        using var sw = new StringWriter();
        using var stream = new BaseEncoderStream(sw, Bases.Base64);
        Assert.ThrowsException<NotSupportedException>(() => stream.SetLength(10));
    }

    /// <summary>Verifies the advertised capability flags remain consistent with the unsupported operations above.</summary>
    [TestMethod]
    public void CapabilityFlags_MatchUnsupportedOperations()
    {
        using var sw = new StringWriter();
        using var stream = new BaseEncoderStream(sw, Bases.Base64);
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    // ---- IO-12: BaseDescriptorBase.ToString shares the same validation contract ----

    /// <summary>Verifies <see cref="IBaseConverter.ToString(byte[], int, int)"/> rejects invalid formatting arguments deterministically, since it constructs a <see cref="BaseEncoderStream"/> internally.</summary>
    [TestMethod]
    public void BaseDescriptorToString_RejectsInvalidFormattingArguments()
    {
        byte[] data = { 0x41, 0x42 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Bases.Base64.ToString(data, 0, 0));
    }
}

