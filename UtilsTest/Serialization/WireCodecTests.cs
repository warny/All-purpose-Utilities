using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.IO.Serialization;

namespace UtilsTest.Serialization;

/// <summary>Verifies wire-codec resolution, framing enforcement, snapshots, and contract diagnostics.</summary>
[TestClass]
public sealed class WireCodecTests
{
    /// <summary>Verifies that an explicitly supplied reader converter wins over the built-in DateTime fallback.</summary>
    [TestMethod]
    public void Reader_CustomDateTimeConverter_OverridesBuiltInDateTimeCodec()
    {
        DateTime expected = new(123, DateTimeKind.Utc);
        Func<IReader, DateTime> converter = _ => expected;
        Reader reader = new(new MemoryStream(new byte[8]), [converter]);
        Assert.AreEqual(expected, reader.Read<DateTime>());
    }

    /// <summary>Verifies that an explicitly supplied writer converter wins over the built-in DateTime fallback.</summary>
    [TestMethod]
    public void Writer_CustomDateTimeConverter_OverridesBuiltInDateTimeCodec()
    {
        using MemoryStream stream = new();
        Action<IWriter, DateTime> converter = (writer, _) => writer.WriteByte(0xA5);
        new Writer(stream, [converter]).Write(DateTime.UnixEpoch);
        CollectionAssert.AreEqual(new byte[] { 0xA5 }, stream.ToArray());
    }

    /// <summary>Verifies that legacy DateTime converters also win for members compiled from reflection contracts.</summary>
    [TestMethod]
    public void ReflectionMember_CustomDateTimeConverter_OverridesBuiltInDateTimeCodec()
    {
        DateTime expected = new(456, DateTimeKind.Utc);
        Func<IReader, DateTime> read = _ => expected;
        Action<IWriter, DateTime> write = (writer, _) => writer.WriteByte(0x5A);
        using MemoryStream stream = new();
        new Writer(stream, [write]).Write(new LegacyEvent { Timestamp = DateTime.UnixEpoch });
        CollectionAssert.AreEqual(new byte[] { 0x5A }, stream.ToArray());
        stream.Position = 0;
        Assert.AreEqual(expected, new Reader(stream, [read]).Read<LegacyEvent>().Timestamp);
    }

    /// <summary>Verifies that an explicitly registered codec wins over an explicitly supplied legacy converter.</summary>
    [TestMethod]
    public void ExplicitDateTimeCodec_OverridesLegacyConverter()
    {
        SerializationOptions options = new();
        options.Codecs.Set(new TicksDateTimeCodec());
        Action<IWriter, DateTime> legacyWriter = (writer, _) => writer.WriteByte(0xFF);
        Func<IReader, DateTime> legacyReader = _ => DateTime.MaxValue;
        DateTime value = new(1234, DateTimeKind.Utc);
        using MemoryStream stream = new();
        new Writer(stream, options, [legacyWriter]).Write(value);
        CollectionAssert.AreEqual(new byte[] { 0xD2, 0x04, 0, 0, 0, 0, 0, 0 }, stream.ToArray());
        stream.Position = 0;
        DateTime copy = new Reader(stream, options, [legacyReader]).Read<DateTime>();
        Assert.AreEqual(1234L, copy.Ticks);
        Assert.AreEqual(DateTimeKind.Unspecified, copy.Kind);
    }

    /// <summary>Verifies member overrides take precedence over a global exact-type codec.</summary>
    [TestMethod]
    public void MemberDateTimeCodec_OverridesGlobalCodec()
    {
        SerializationOptions options = new();
        options.Codecs.Set(new TicksDateTimeCodec());
        Event value = new() { Created = new DateTime(1234, DateTimeKind.Utc), Timestamp = DateTime.UnixEpoch.AddMilliseconds(5) };
        using MemoryStream stream = new();
        new Writer(stream, options).Write(value);
        CollectionAssert.AreEqual(new byte[] { 0xD2, 0x04, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0 }, stream.ToArray());
        stream.Position = 0;
        Event copy = new Reader(stream, options).Read<Event>();
        Assert.AreEqual(1234L, copy.Created.Ticks);
        Assert.AreEqual(DateTimeKind.Unspecified, copy.Created.Kind);
        Assert.AreEqual(value.Timestamp, copy.Timestamp);
    }

    /// <summary>Verifies a ReaderWriter slice retains the parent's original exact codec snapshot.</summary>
    [TestMethod]
    public void ReaderWriterSlice_PreservesOriginalCodecSnapshot()
    {
        SerializationOptions options = new();
        options.Codecs.Set(new CustomIdCodec());
        using MemoryStream stream = new(new byte[32], writable: true);
        ReaderWriter parent = new(stream, options);
        options.Codecs.Set(new OffsetCustomIdCodec());
        ReaderWriter child = parent.Slice(8, 8);
        child.Writer.Write(new CustomId(42));
        CollectionAssert.AreEqual(new byte[] { 42, 0, 0, 0 }, stream.ToArray()[8..12]);
        child.Position = 0;
        Assert.AreEqual(new CustomId(42), child.Reader.Read<CustomId>());
    }

    /// <summary>Verifies an exact fixed codec succeeds on a forward-only destination.</summary>
    [TestMethod]
    public void FixedCodec_WritesExactSize_Succeeds()
    {
        SerializationOptions options = OptionsFor(new DeclaredSizeBlobCodec(3, 3));
        using ForwardOnlyStream stream = new();
        new Writer(stream, options).Write(new Blob([1, 2, 3]));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, stream.ToArray());
    }

    /// <summary>Verifies fixed framing rejects a codec that writes fewer bytes than declared.</summary>
    [TestMethod]
    public void FixedCodec_WritesTooFewBytes_IsRejected()
    {
        SerializationOptions options = OptionsFor(new DeclaredSizeBlobCodec(3, 2));
        using ForwardOnlyStream stream = new();
        Assert.ThrowsException<InvalidDataException>(() => new Writer(stream, options).Write(new Blob([1, 2, 3])));
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, stream.ToArray());
    }

    /// <summary>Verifies fixed framing stops a codec before it writes beyond its declaration.</summary>
    [TestMethod]
    public void FixedCodec_WritesTooManyBytes_IsRejected()
    {
        SerializationOptions options = OptionsFor(new DeclaredSizeBlobCodec(3, 4));
        using ForwardOnlyStream stream = new();
        Assert.ThrowsException<InvalidDataException>(() => new Writer(stream, options).Write(new Blob([1, 2, 3, 4])));
        Assert.AreEqual(0, stream.ToArray().Length);
    }

    /// <summary>Verifies a correct size provider produces a matching prefix and payload.</summary>
    [TestMethod]
    public void SizeProvider_ExactLength_Succeeds() => AssertSizeProvider(3, false, [3, 0, 0, 0, 1, 2, 3]);

    /// <summary>Verifies a size provider cannot silently underwrite its announced payload.</summary>
    [TestMethod]
    public void SizeProvider_WritesTooFewBytes_IsRejected() => AssertSizeProvider(2, true, [3, 0, 0, 0, 1, 2]);

    /// <summary>Verifies a size provider cannot write beyond its announced payload.</summary>
    [TestMethod]
    public void SizeProvider_WritesTooManyBytes_IsRejected() => AssertSizeProvider(4, true, [3, 0, 0, 0]);

    /// <summary>Verifies an invalid codec attribute is rejected with structured contract diagnostics.</summary>
    [TestMethod]
    public void InvalidWireCodecAttribute_IsRejectedAsContractError()
    {
        SerializationContractException error = Assert.ThrowsException<SerializationContractException>(() => new Writer(new MemoryStream()).Write(new InvalidCodecModel()));
        Assert.IsTrue(error.Diagnostics.Any(diagnostic => diagnostic.Code == "UIORT013"));
    }

    /// <summary>Verifies an invalid framing attribute is rejected with structured contract diagnostics.</summary>
    [TestMethod]
    public void InvalidWireFramingAttribute_IsRejectedAsContractError()
    {
        SerializationContractException error = Assert.ThrowsException<SerializationContractException>(() => new Writer(new MemoryStream()).Write(new InvalidFramingModel()));
        Assert.IsTrue(error.Diagnostics.Any(diagnostic => diagnostic.Code == "UIORT014"));
    }

    /// <summary>Verifies serialization option null guards and writer-only validation timing.</summary>
    [TestMethod]
    public void SerializationOptions_AreValidatedByRelevantConsumer()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new Reader(new MemoryStream(), (SerializationOptions)null!));
        Assert.ThrowsException<ArgumentNullException>(() => new Writer(new MemoryStream(), (SerializationOptions)null!));
        Assert.ThrowsException<ArgumentNullException>(() => new ReaderWriter(new MemoryStream(), null!));
        SerializationOptions invalidWriterOptions = new() { MaximumBufferedPayloadLength = 0 };
        _ = new Reader(new MemoryStream(), invalidWriterOptions);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Writer(new MemoryStream(), invalidWriterOptions));
    }

    /// <summary>Creates an options instance for a fixed Blob codec.</summary>
    private static SerializationOptions OptionsFor(IWireCodec<Blob> codec)
    {
        SerializationOptions options = new();
        options.Codecs.Set(codec);
        return options;
    }

    /// <summary>Executes one known-size payload assertion, including the documented non-atomic prefix behavior.</summary>
    private static void AssertSizeProvider(int bytesToWrite, bool shouldThrow, byte[] expected)
    {
        SerializationOptions options = new();
        options.Codecs.Set(new SizedBlobCodec(3, bytesToWrite), new Int32LengthWireFraming());
        using ForwardOnlyStream stream = new();
        Action action = () => new Writer(stream, options).Write(new Blob([1, 2, 3, 4]));
        if (shouldThrow) Assert.ThrowsException<InvalidDataException>(action); else action();
        CollectionAssert.AreEqual(expected, stream.ToArray());
    }

    /// <summary>Custom identifier used to prove exact codec snapshot behavior.</summary>
    private readonly record struct CustomId(int Value);

    /// <summary>Variable byte payload used by framing tests.</summary>
    private readonly record struct Blob(byte[] Value);

    /// <summary>Four-byte identifier codec A.</summary>
    private sealed class CustomIdCodec : IFixedWireCodec<CustomId>
    {
        public int Size => 4;
        public CustomId Read(IReader reader) => new(reader.Read<int>());
        public void Write(IWriter writer, CustomId value) => writer.Write(value.Value);
    }

    /// <summary>Distinguishable identifier codec B.</summary>
    private sealed class OffsetCustomIdCodec : IFixedWireCodec<CustomId>
    {
        public int Size => 4;
        public CustomId Read(IReader reader) => new(reader.Read<int>() - 1);
        public void Write(IWriter writer, CustomId value) => writer.Write(value.Value + 1);
    }

    /// <summary>Fixed codec whose actual byte count is controlled by the test.</summary>
    private sealed class DeclaredSizeBlobCodec(int declaredSize, int bytesToWrite) : IFixedWireCodec<Blob>
    {
        public int Size => declaredSize;
        public Blob Read(IReader reader) => new(reader.ReadBytes(declaredSize));
        public void Write(IWriter writer, Blob value) => writer.WriteBytes(value.Value.AsSpan(0, bytesToWrite));
    }

    /// <summary>Size-providing codec whose actual byte count is controlled by the test.</summary>
    private sealed class SizedBlobCodec(int announcedSize, int bytesToWrite) : IWireCodec<Blob>, IWireSizeProvider<Blob>
    {
        public Blob Read(IReader reader) => new(reader.ReadBytes(announcedSize));
        public void Write(IWriter writer, Blob value) => writer.WriteBytes(value.Value.AsSpan(0, bytesToWrite));
        public bool TryGetEncodedSize(Blob value, out int size) { size = announcedSize; return true; }
    }

    /// <summary>Reflection model using only the built-in DateTime fallback.</summary>
    private sealed class LegacyEvent { [Field(1)] public DateTime Timestamp { get; set; } }

    /// <summary>Reflection model combining a global DateTime codec and member override.</summary>
    private sealed class Event { [Field(1)] public DateTime Created { get; set; } [Field(2), WireCodec(typeof(UnixMillisecondsDateTimeCodec))] public DateTime Timestamp { get; set; } }

    /// <summary>Model with an incompatible codec attribute.</summary>
    private sealed class InvalidCodecModel { [Field(1), WireCodec(typeof(string))] public DateTime Timestamp { get; set; } }

    /// <summary>Model with an incompatible framing attribute.</summary>
    private sealed class InvalidFramingModel { [Field(1), WireFraming(typeof(string))] public DateTime Timestamp { get; set; } }

    /// <summary>Forward-only test stream whose seek and position members always throw.</summary>
    private sealed class ForwardOnlyStream : Stream
    {
        private readonly MemoryStream inner = new();
        public byte[] ToArray() => inner.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);
        public override void WriteByte(byte value) => inner.WriteByte(value);
    }
}
