using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Utils.IO.Serialization;

namespace UtilsTest.Serialization;

/// <summary>Verifies IO-04 aggregate wire budgets and collection allocation limits.</summary>
[TestClass]
public sealed class Io04BoundedParsingTests
{
    /// <summary>Verifies one payload, an exact boundary, a second payload failure, and a zero budget.</summary>
    [TestMethod]
    public void AggregateBudget_BoundariesAreEnforcedAcrossPayloads()
    {
        using MemoryStream exact = Strings("abc");
        Assert.AreEqual("abc", new Reader(exact, new ReaderOptions { MaximumReadBytes = 7 }).Read<string>());

        using MemoryStream pair = Strings("a", "b");
        Reader reader = new(pair, new ReaderOptions { MaximumPayloadLength = 1, MaximumReadBytes = 9 });
        Assert.AreEqual("a", reader.Read<string>());
        Assert.ThrowsException<InvalidDataException>(() => reader.Read<string>());

        Assert.ThrowsException<InvalidDataException>(() => new Reader(new MemoryStream([1]), new ReaderOptions { MaximumReadBytes = 0 }).Read<byte>());
    }

    /// <summary>Verifies reflection members and nested contracts consume one shared operation budget.</summary>
    [TestMethod]
    public void ReflectionAndNestedContracts_ShareAggregateBudget()
    {
        using MemoryStream pair = Strings("one", "two");
        Reader reflection = new(pair, new ReaderOptions { MaximumPayloadLength = 3, MaximumReadBytes = 13 });
        Assert.ThrowsException<InvalidDataException>(() => reflection.Read<PayloadContainer>());

        using MemoryStream nested = Strings("one", "two");
        Reader nestedReader = new(nested, new ReaderOptions { MaximumReadBytes = 13 });
        Assert.ThrowsException<InvalidDataException>(() => nestedReader.Read<OuterContainer>());
    }

    /// <summary>Verifies a source-generated member reader consumes the owning Reader's shared budget.</summary>
    [TestMethod]
    public void GeneratedContract_SharesAggregateBudgetAcrossMembers()
    {
        using MemoryStream pair = Strings("one", "two");
        Reader reader = new(pair, new ReaderOptions { MaximumPayloadLength = 3, MaximumReadBytes = 13 });
        Assert.ThrowsException<InvalidDataException>(() => reader.ReadUtilsTest_Serialization_Io04BoundedParsingTests_GeneratedPayloadContainer_c4aa081c());
    }

    /// <summary>Verifies collection counts are checked before allocation and exact configured counts work.</summary>
    [TestMethod]
    public void Collections_ValidateCountsAndKnownWireSizeBeforeAllocation()
    {
        Reader reader = new(new MemoryStream(new byte[8]), new ReaderOptions { MaximumCollectionLength = 2, MaximumReadBytes = 8 });
        Assert.ThrowsException<InvalidDataException>(() => reader.ReadArray<int>(-1));
        Assert.ThrowsException<InvalidDataException>(() => reader.ReadArray<int>(3));
        Assert.AreEqual(2, reader.ReadArray<int>(2).Length);

        Reader overflowSafe = new(new MemoryStream(), new ReaderOptions { MaximumReadBytes = 1, MaximumCollectionLength = int.MaxValue });
        Assert.ThrowsException<InvalidDataException>(() => overflowSafe.ReadArray<decimal>(int.MaxValue));
    }

    /// <summary>Verifies slices share depletion with their parent and rereads do not restore budget.</summary>
    [TestMethod]
    public void ReaderAndReaderWriterSlices_ShareAggregateBudget()
    {
        using MemoryStream stream = new([1, 2]);
        Reader root = new(stream, new ReaderOptions { MaximumReadBytes = 1 });
        Assert.AreEqual((byte)1, root.Slice(0, 1).Read<byte>());
        Assert.ThrowsException<InvalidDataException>(() => root.Read<byte>());

        using MemoryStream pairStream = new([1, 2]);
        ReaderWriter pair = new(pairStream, new ReaderOptions { MaximumReadBytes = 1 }, new SerializationOptions());
        Assert.AreEqual((byte)1, pair.Slice(0, 1).Reader.Read<byte>());
        Assert.ThrowsException<InvalidDataException>(() => pair.Reader.Read<byte>());
    }

    /// <summary>Verifies sequential non-seekable input uses the same deterministic byte accounting.</summary>
    [TestMethod]
    public void AggregateBudget_WorksOnForwardOnlyStream()
    {
        using ForwardOnlyReadStream stream = new([1, 2]);
        Reader reader = new(stream, new ReaderOptions { MaximumReadBytes = 1 });
        Assert.AreEqual((byte)1, reader.Read<byte>());
        Assert.ThrowsException<InvalidDataException>(() => reader.Read<byte>());
    }

    /// <summary>Verifies fixed, codec-owned, and staged length-prefixed codecs cannot bypass or double-debit budgets.</summary>
    [TestMethod]
    public void Codecs_ConsumePhysicalWireBytesExactlyOnce()
    {
        SerializationOptions lengthOptions = Options(new BlobCodec(4), new Int32LengthWireFraming());
        Reader exact = new(new MemoryStream([4, 0, 0, 0, 1, 2, 3, 4]), new ReaderOptions { MaximumReadBytes = 8 }, lengthOptions);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, exact.Read<Blob>().Bytes);

        TrackingCodec tracking = new();
        SerializationOptions rejectedOptions = Options(tracking, new Int32LengthWireFraming());
        using MemoryStream rejectedStream = new([4, 0, 0, 0, 1, 2, 3, 4]);
        Reader rejected = new(rejectedStream, new ReaderOptions { MaximumReadBytes = 7 }, rejectedOptions);
        Assert.ThrowsException<InvalidDataException>(() => rejected.Read<Blob>());
        Assert.IsFalse(tracking.WasRead);
        Assert.AreEqual(4, rejectedStream.Position);

        SerializationOptions fixedOptions = Options(new FixedBlobCodec(), new FixedWireFraming(4));
        Assert.ThrowsException<InvalidDataException>(() => new Reader(new MemoryStream([1, 2, 3, 4]), new ReaderOptions { MaximumReadBytes = 3 }, fixedOptions).Read<Blob>());

        SerializationOptions ownedOptions = Options(new BlobCodec(4), new CodecOwnedWireFraming());
        Assert.ThrowsException<InvalidDataException>(() => new Reader(new MemoryStream([1, 2, 3, 4]), new ReaderOptions { MaximumReadBytes = 3 }, ownedOptions).Read<Blob>());

        SerializationOptions nestedOptions = new();
        nestedOptions.Codecs.Set(new OuterBlobCodec(), new Int32LengthWireFraming());
        nestedOptions.Codecs.Set(new NestedBlobCodec(), new Int32LengthWireFraming());
        Reader nested = new(new MemoryStream([8, 0, 0, 0, 4, 0, 0, 0, 1, 2, 3, 4]), new ReaderOptions { MaximumReadBytes = 12 }, nestedOptions);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, nested.Read<OuterBlob>().Value.Bytes);
    }

    /// <summary>Verifies null aggregate limits preserve normal multi-payload round trips.</summary>
    [TestMethod]
    public void NullAggregateLimits_PreserveExistingRoundTrips()
    {
        using MemoryStream stream = Strings("first", "second");
        Reader reader = new(stream, new ReaderOptions());
        Assert.AreEqual("first", reader.Read<string>());
        Assert.AreEqual("second", reader.Read<string>());
    }

    /// <summary>Creates a stream containing sequential historical string payloads.</summary>
    private static MemoryStream Strings(params string[] values)
    {
        MemoryStream stream = new();
        Writer writer = new(stream);
        foreach (string value in values) writer.Write(value);
        stream.Position = 0;
        return stream;
    }

    /// <summary>Creates codec options for a test value and framing policy.</summary>
    private static SerializationOptions Options(IWireCodec<Blob> codec, IWireFraming framing)
    {
        SerializationOptions options = new();
        options.Codecs.Set(codec, framing);
        return options;
    }

    /// <summary>Reflection contract containing two independently length-prefixed payloads.</summary>
    public sealed class PayloadContainer
    {
        /// <summary>Gets or sets the first payload.</summary>
        [Field(1)] public string A { get; set; } = string.Empty;
        /// <summary>Gets or sets the second payload.</summary>
        [Field(2)] public string B { get; set; } = string.Empty;
    }

    /// <summary>Generated contract containing two independently length-prefixed payloads.</summary>
    [GenerateReaderWriter]
    public sealed class GeneratedPayloadContainer
    {
        /// <summary>Gets or sets the first payload.</summary>
        [Field(1)] public string A { get; set; } = string.Empty;
        /// <summary>Gets or sets the second payload.</summary>
        [Field(2)] public string B { get; set; } = string.Empty;
    }

    /// <summary>Nested reflection contract used to verify tree-wide accounting.</summary>
    public sealed class OuterContainer
    {
        /// <summary>Gets or sets the first child.</summary>
        [Field(1)] public InnerContainer First { get; set; } = new();
        /// <summary>Gets or sets the second child.</summary>
        [Field(2)] public InnerContainer Second { get; set; } = new();
    }

    /// <summary>Nested payload holder.</summary>
    public sealed class InnerContainer
    {
        /// <summary>Gets or sets the payload.</summary>
        [Field(1)] public string Value { get; set; } = string.Empty;
    }

    /// <summary>Binary value used by codec budget tests.</summary>
    private readonly record struct Blob(byte[] Bytes);

    /// <summary>Nested codec value.</summary>
    private readonly record struct NestedBlob(byte[] Bytes);

    /// <summary>Outer codec value.</summary>
    private readonly record struct OuterBlob(NestedBlob Value);

    /// <summary>Reads a nested length-prefixed codec through the staged outer reader.</summary>
    private sealed class OuterBlobCodec : IWireCodec<OuterBlob>
    {
        /// <inheritdoc />
        public OuterBlob Read(IReader reader) => new(reader.Read<NestedBlob>());
        /// <inheritdoc />
        public void Write(IWriter writer, OuterBlob value) => writer.Write(value.Value);
    }

    /// <summary>Reads the inner four-byte payload.</summary>
    private sealed class NestedBlobCodec : IWireCodec<NestedBlob>
    {
        /// <inheritdoc />
        public NestedBlob Read(IReader reader) => new(reader.ReadBytes(4));
        /// <inheritdoc />
        public void Write(IWriter writer, NestedBlob value) => writer.WriteBytes(value.Bytes);
    }

    /// <summary>Codec that reads an exact caller-selected number of bytes.</summary>
    private class BlobCodec(int length) : IWireCodec<Blob>
    {
        /// <inheritdoc />
        public virtual Blob Read(IReader reader) => new(reader.ReadBytes(length));
        /// <inheritdoc />
        public void Write(IWriter writer, Blob value) => writer.WriteBytes(value.Bytes);
    }

    /// <summary>Fixed four-byte codec.</summary>
    private sealed class FixedBlobCodec : BlobCodec, IFixedWireCodec<Blob>
    {
        /// <summary>Initializes the fixed codec.</summary>
        public FixedBlobCodec() : base(4) { }
        /// <inheritdoc />
        public int Size => 4;
    }

    /// <summary>Codec that records whether payload decoding started.</summary>
    private sealed class TrackingCodec : BlobCodec
    {
        /// <summary>Initializes the tracking codec.</summary>
        public TrackingCodec() : base(4) { }
        /// <summary>Gets whether decoding started.</summary>
        public bool WasRead { get; private set; }
        /// <inheritdoc />
        public override Blob Read(IReader reader) { WasRead = true; return base.Read(reader); }
    }

    /// <summary>Non-seekable stream used to prove accounting does not inspect positions.</summary>
    private sealed class ForwardOnlyReadStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        /// <inheritdoc />
        public override bool CanSeek => false;
        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();
        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }
}
