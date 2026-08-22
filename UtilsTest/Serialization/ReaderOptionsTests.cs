using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Numerics;
using Utils.IO.Serialization;

namespace UtilsTest.Serialization;

/// <summary>Verifies configurable limits for length-prefixed reader payloads.</summary>
[TestClass]
public sealed class ReaderOptionsTests
{
    /// <summary>Ensures default readers preserve round trips larger than the former 16 MiB limit.</summary>
    [TestMethod]
    public void DefaultOptions_StringLargerThanFormerLimit_RoundTrips()
    {
        string expected = new('x', (16 * 1024 * 1024) + 1);
        using var stream = new MemoryStream();
        new Writer(stream).Write(expected);
        stream.Position = 0;

        Assert.AreEqual(expected, new Reader(stream).Read<string>());
    }

    /// <summary>Ensures an explicitly configured limit rejects a larger string payload.</summary>
    [TestMethod]
    public void ExplicitLimit_StringAboveLimit_IsRejected()
    {
        using var stream = CreateStringPayload("12345");
        var reader = new Reader(stream, new ReaderOptions { MaximumPayloadLength = 4 });

        Assert.ThrowsException<InvalidDataException>(() => reader.Read<string>());
    }

    /// <summary>Ensures the configured payload limit also applies to arbitrary-precision integers.</summary>
    [TestMethod]
    public void ExplicitLimit_BigIntegerAboveLimit_IsRejected()
    {
        using var stream = new MemoryStream();
        new Writer(stream).Write(new BigInteger(new byte[] { 1, 2, 3, 4, 5 }));
        stream.Position = 0;
        var reader = new Reader(stream, new ReaderOptions { MaximumPayloadLength = 4 });

        Assert.ThrowsException<InvalidDataException>(() => reader.Read<BigInteger>());
    }

    /// <summary>Ensures a payload exactly equal to the configured limit remains valid.</summary>
    [TestMethod]
    public void ExplicitLimit_ExactLength_IsAccepted()
    {
        using var stream = CreateStringPayload("1234");
        var reader = new Reader(stream, new ReaderOptions { MaximumPayloadLength = 4 });

        Assert.AreEqual("1234", reader.Read<string>());
    }

    /// <summary>Ensures zero permits only empty payloads and negative limits are rejected at construction.</summary>
    [TestMethod]
    public void InvalidAndZeroLimits_AreHandledExplicitly()
    {
        using var emptyStream = CreateStringPayload(string.Empty);
        Assert.AreEqual(string.Empty, new Reader(emptyStream, new ReaderOptions { MaximumPayloadLength = 0 }).Read<string>());
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            new Reader(new MemoryStream(), new ReaderOptions { MaximumPayloadLength = -1 }));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            new Reader(new MemoryStream(), new ReaderOptions { MaximumReadBytes = -1 }));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            new Reader(new MemoryStream(), new ReaderOptions { MaximumCollectionLength = -1 }));

        Reader noCollections = new(new MemoryStream(), new ReaderOptions { MaximumCollectionLength = 0 });
        Assert.AreEqual(0, noCollections.ReadArray<int>(0).Length);
        Assert.ThrowsException<InvalidDataException>(() => noCollections.ReadArray<int>(1));
    }

    /// <summary>Serializes a string into a positioned memory stream for limit tests.</summary>
    private static MemoryStream CreateStringPayload(string value)
    {
        var stream = new MemoryStream();
        new Writer(stream).Write(value);
        stream.Position = 0;
        return stream;
    }
}
