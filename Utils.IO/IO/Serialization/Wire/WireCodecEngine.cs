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
            codec.Write(owner, value);
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

    /// <summary>Writes a fixed payload through a counting facade and verifies its declared size.</summary>
    private static void WriteFixed<T>(Writer owner, IWireWriter<T> codec, T value, int expected)
    {
        if (expected <= 0) throw new InvalidOperationException("Fixed framing size must be positive.");
        codec.Write(owner, value);
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
        Writer buffered = owner.CreateCodecWriter(staging);
        codec.Write(buffered, value);
        if (staging.Length > owner.MaximumBufferedPayloadLength)
            throw new InvalidOperationException($"Buffered payload exceeds {owner.MaximumBufferedPayloadLength} bytes.");
        framing.WriteLength(owner, checked((int)staging.Length));
        owner.WriteBytes(staging.GetBuffer().AsSpan(0, (int)staging.Length));
    }

}
