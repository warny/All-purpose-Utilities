using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.IO.Serialization;

namespace UtilsTest.Serialization;

/// <summary>Verifies generic wire codecs, DateTime formats, framing, and precedence.</summary>
[TestClass]
public sealed class WireCodecTests
{
    /// <summary>Verifies that the default DateTime binary representation preserves every Kind.</summary>
    [TestMethod]
    public void DefaultDateTimePreservesKind()
    {
        foreach (DateTime value in new[] { new DateTime(638900000000000000, DateTimeKind.Utc), new DateTime(638900000000000000, DateTimeKind.Local), new DateTime(638900000000000000, DateTimeKind.Unspecified) })
        {
            using MemoryStream stream = new();
            new Writer(stream).Write(value);
            CollectionAssert.AreEqual(BitConverter.GetBytes(value.ToBinary()), stream.ToArray());
            stream.Position = 0;
            Assert.AreEqual(value.ToBinary(), new Reader(stream).Read<DateTime>().ToBinary());
        }
    }

    /// <summary>Verifies exact-type registration for a type unknown to the built-in serializers.</summary>
    [TestMethod]
    public void CustomTypeUsesRegisteredCodecSnapshot()
    {
        SerializationOptions options = new();
        options.Codecs.Set(new CustomIdCodec());
        using MemoryStream stream = new();
        Writer writer = new(stream, options);
        options.Codecs.Set(new OffsetCustomIdCodec());
        writer.Write(new CustomId(42));
        CollectionAssert.AreEqual(new byte[] { 42, 0, 0, 0 }, stream.ToArray());
        stream.Position = 0;
        SerializationOptions readOptions = new();
        readOptions.Codecs.Set(new CustomIdCodec());
        Assert.AreEqual(new CustomId(42), new Reader(stream, readOptions).Read<CustomId>());
    }

    /// <summary>Verifies member codec precedence over an exact global DateTime registration.</summary>
    [TestMethod]
    public void MemberCodecOverridesGlobalCodec()
    {
        SerializationOptions options = new();
        options.Codecs.Set(new TicksDateTimeCodec());
        Event value = new() { Created = new DateTime(1234, DateTimeKind.Utc), Timestamp = DateTime.UnixEpoch.AddMilliseconds(5) };
        using MemoryStream stream = new();
        new Writer(stream, options).Write(value);
        byte[] expected = [.. BitConverter.GetBytes(1234L), .. BitConverter.GetBytes(5L)];
        CollectionAssert.AreEqual(expected, stream.ToArray());
        stream.Position = 0;
        Event copy = new Reader(stream, options).Read<Event>();
        Assert.AreEqual(1234L, copy.Created.Ticks);
        Assert.AreEqual(DateTimeKind.Unspecified, copy.Created.Kind);
        Assert.AreEqual(value.Timestamp, copy.Timestamp);
    }

    /// <summary>Verifies known-size length framing on a writer whose seek members throw.</summary>
    [TestMethod]
    public void KnownLengthWritesForwardOnly()
    {
        SerializationOptions options = new();
        options.Codecs.Set(new BytesCodec(), new Int32LengthWireFraming());
        using ForwardOnlyStream stream = new();
        new Writer(stream, options).Write(new Blob([1, 2, 3]));
        CollectionAssert.AreEqual(new byte[] { 3, 0, 0, 0, 1, 2, 3 }, stream.ToArray());
    }

    /// <summary>Verifies that unknown forward-only payloads fail before touching the target.</summary>
    [TestMethod]
    public void UnknownLengthWithoutBufferingIsAtomic()
    {
        SerializationOptions options = new();
        options.Codecs.Set(new UnknownBytesCodec(), new Int32LengthWireFraming());
        using ForwardOnlyStream stream = new();
        Assert.ThrowsException<InvalidOperationException>(() => new Writer(stream, options).Write(new Blob([1, 2])));
        Assert.AreEqual(0, stream.ToArray().Length);
    }

    /// <summary>Verifies bounded staging of an unknown forward-only payload.</summary>
    [TestMethod]
    public void UnknownLengthCanUseBoundedBuffering()
    {
        SerializationOptions options = new() { VariablePayloadWritePolicy = VariablePayloadWritePolicy.AllowBuffering, MaximumBufferedPayloadLength = 3 };
        options.Codecs.Set(new UnknownBytesCodec(), new Int32LengthWireFraming());
        using ForwardOnlyStream stream = new();
        new Writer(stream, options).Write(new Blob([7, 8, 9]));
        CollectionAssert.AreEqual(new byte[] { 3, 0, 0, 0, 7, 8, 9 }, stream.ToArray());
    }

    private readonly record struct CustomId(int Value);
    private readonly record struct Blob(byte[] Value);
    private sealed class CustomIdCodec : IFixedWireCodec<CustomId> { public int Size => 4; public CustomId Read(IReader reader) => new(reader.Read<int>()); public void Write(IWriter writer, CustomId value) => writer.Write(value.Value); }
    private sealed class OffsetCustomIdCodec : IFixedWireCodec<CustomId> { public int Size => 4; public CustomId Read(IReader reader) => new(reader.Read<int>() - 1); public void Write(IWriter writer, CustomId value) => writer.Write(value.Value + 1); }
    private sealed class BytesCodec : IWireCodec<Blob>, IWireSizeProvider<Blob> { public Blob Read(IReader reader) => new(reader.ReadBytes(3)); public void Write(IWriter writer, Blob value) => writer.WriteBytes(value.Value); public bool TryGetEncodedSize(Blob value, out int size) { size = value.Value.Length; return true; } }
    private sealed class UnknownBytesCodec : IWireCodec<Blob> { public Blob Read(IReader reader) => new(reader.ReadBytes(2)); public void Write(IWriter writer, Blob value) => writer.WriteBytes(value.Value); }
    private sealed class Event { [Field(1)] public DateTime Created { get; set; } [Field(2), WireCodec(typeof(UnixMillisecondsDateTimeCodec))] public DateTime Timestamp { get; set; } }
    private sealed class ForwardOnlyStream : Stream { private readonly MemoryStream inner = new(); public byte[] ToArray() => inner.ToArray(); public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true; public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); } public override void Flush() => inner.Flush(); public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count); public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer); public override void WriteByte(byte value) => inner.WriteByte(value); }
}
