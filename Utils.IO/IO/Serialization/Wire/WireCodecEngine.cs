using System;
using System.Collections.Generic;
using System.IO;

namespace Utils.IO.Serialization;

/// <summary>Executes codecs while keeping value encoding, framing, and stream capabilities separate.</summary>
internal static class WireCodecEngine
{
    /// <summary>Reads a framed payload and prevents fixed or length-prefixed codecs from reading beyond it.</summary>
    internal static T Read<T>(Reader owner, IWireReader<T> codec, IWireFraming framing)
    {
        if (framing is CodecOwnedWireFraming) return codec.Read(owner);
        int length = framing switch
        {
            FixedWireFraming fixedFraming => fixedFraming.Size,
            IWireLengthFraming lengthFraming => lengthFraming.ReadLength(owner),
            _ => throw new InvalidOperationException("Unsupported wire framing.")
        };
        byte[] payload = owner.ReadBytes(length);
        if (payload.Length != length) throw new EndOfStreamException($"Expected {length} codec payload bytes, received {payload.Length}.");
        using MemoryStream stream = new(payload, writable: false);
        Reader bounded = owner.CreateCodecReader(stream);
        T value = codec.Read(bounded);
        if (stream.Position != stream.Length) throw new InvalidDataException($"Codec consumed {stream.Position} of {stream.Length} framed bytes.");
        return value;
    }

    /// <summary>
    /// Writes framed data without assuming seekability. Known sizes are prefixed first; an unknown size may use
    /// fixed-width backpatching on a seekable destination. Otherwise only explicit bounded buffering is allowed.
    /// Variable-width prefixes are never backpatched by reserving an assumed width.
    /// </summary>
    internal static void Write<T>(Writer owner, IWireWriter<T> codec, IWireFraming framing, T value)
    {
        if (framing is CodecOwnedWireFraming)
        {
            codec.Write(owner, value);
            return;
        }
        if (framing is FixedWireFraming fixedFraming)
        {
            WriteFixed(owner, codec, value, fixedFraming.Size);
            return;
        }
        if (framing is not IWireLengthFraming lengthFraming) throw new InvalidOperationException("Unsupported wire framing.");
        if (TryGetSize(codec, value, out int knownSize))
        {
            lengthFraming.WriteLength(owner, knownSize);
            WriteExactly(owner, codec, value, knownSize);
            return;
        }
        if (lengthFraming.HasFixedPrefixSize && owner.Stream.CanSeek)
        {
            Backpatch(owner, codec, lengthFraming, value);
            return;
        }
        if (owner.WritePolicy != VariablePayloadWritePolicy.AllowBuffering)
            throw new InvalidOperationException("The payload size is unknown and cannot be written without explicit buffering.");
        Buffer(owner, codec, lengthFraming, value);
    }

    /// <summary>Writes a fixed payload and verifies its exact declared size without buffering or seeking.</summary>
    private static void WriteFixed<T>(Writer owner, IWireWriter<T> codec, T value, int expected)
    {
        if (expected <= 0) throw new InvalidOperationException("Fixed framing size must be positive.");
        WriteExactly(owner, codec, value, expected);
    }

    /// <summary>Runs a codec through a bounded counting stream that rejects overflow before it reaches the target.</summary>
    private static void WriteExactly<T>(Writer owner, IWireWriter<T> codec, T value, int expected)
    {
        using BoundedWriteStream boundedStream = new(owner.Stream, expected);
        Writer boundedWriter = owner.CreateCodecWriter(boundedStream);
        codec.Write(boundedWriter, value);
        if (boundedStream.BytesWritten != expected)
            throw new InvalidDataException($"Wire codec wrote {boundedStream.BytesWritten} bytes; exactly {expected} bytes were required.");
    }

    /// <summary>Obtains a safe non-negative precomputed payload size when the codec supports it.</summary>
    private static bool TryGetSize<T>(IWireWriter<T> codec, T value, out int size)
    {
        if (codec is IWireSizeProvider<T> provider && provider.TryGetEncodedSize(value, out size))
        {
            if (size < 0) throw new InvalidOperationException("A codec returned a negative encoded size.");
            return true;
        }
        size = 0;
        return false;
    }

    /// <summary>Reserves a fixed prefix, streams the payload, and patches the measured length.</summary>
    private static void Backpatch<T>(Writer owner, IWireWriter<T> codec, IWireLengthFraming framing, T value)
    {
        long prefixPosition = owner.Stream.Position;
        owner.WriteBytes(new byte[framing.PrefixSize]);
        long payloadStart = owner.Stream.Position;
        codec.Write(owner, value);
        long end = owner.Stream.Position;
        long measured = end - payloadStart;
        if (measured > int.MaxValue) throw new InvalidOperationException("Wire payload is too large.");
        owner.Stream.Position = prefixPosition;
        framing.WriteLength(owner, (int)measured);
        if (owner.Stream.Position != payloadStart) throw new InvalidOperationException("Length framing wrote an unexpected fixed prefix size.");
        owner.Stream.Position = end;
    }

    /// <summary>Stages a payload within the configured bound, then atomically appends prefix and payload to the target.</summary>
    private static void Buffer<T>(Writer owner, IWireWriter<T> codec, IWireLengthFraming framing, T value)
    {
        using MemoryStream staging = new();
        using BoundedWriteStream boundedStaging = new(staging, owner.MaximumBufferedPayloadLength);
        Writer buffered = owner.CreateCodecWriter(boundedStaging);
        codec.Write(buffered, value);
        framing.WriteLength(owner, boundedStaging.BytesWritten);
        owner.WriteBytes(staging.GetBuffer().AsSpan(0, boundedStaging.BytesWritten));
    }

    /// <summary>Forwards writes while counting bytes and rejecting data beyond a declared payload boundary.</summary>
    private sealed class BoundedWriteStream : Stream
    {
        private readonly Stream target;
        private readonly int maximumLength;

        /// <summary>Initializes a non-owning bounded view over the target stream.</summary>
        internal BoundedWriteStream(Stream target, int maximumLength)
        {
            this.target = target;
            this.maximumLength = maximumLength;
        }

        /// <summary>Gets the number of bytes successfully forwarded.</summary>
        internal int BytesWritten { get; private set; }

        /// <inheritdoc />
        public override bool CanRead => false;
        /// <inheritdoc />
        public override bool CanSeek => false;
        /// <inheritdoc />
        public override bool CanWrite => target.CanWrite;
        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();
        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <inheritdoc />
        public override void Flush() => target.Flush();
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>Forwards an array write only when it fits entirely within the declared boundary.</summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureAvailable(count);
            target.Write(buffer, offset, count);
            BytesWritten += count;
        }

        /// <summary>Forwards a span write only when it fits entirely within the declared boundary.</summary>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureAvailable(buffer.Length);
            target.Write(buffer);
            BytesWritten += buffer.Length;
        }

        /// <summary>Forwards a byte only when it fits within the declared boundary.</summary>
        public override void WriteByte(byte value)
        {
            EnsureAvailable(1);
            target.WriteByte(value);
            BytesWritten++;
        }

        /// <summary>Rejects an attempted write before any bytes beyond the declared payload reach the target.</summary>
        private void EnsureAvailable(int count)
        {
            if (count < 0 || count > maximumLength - BytesWritten)
                throw new InvalidDataException($"Wire codec attempted to write beyond its declared {maximumLength}-byte payload.");
        }
    }

}
