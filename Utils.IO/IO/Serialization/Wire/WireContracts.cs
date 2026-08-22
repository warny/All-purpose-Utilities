using System;

namespace Utils.IO.Serialization;

/// <summary>Decodes a value payload from sequential wire data.</summary>
public interface IWireReader<T>
{
    /// <summary>Decodes one value from the supplied sequential reader.</summary>
    T Read(IReader reader);
}

/// <summary>Encodes a value payload to sequential wire data.</summary>
public interface IWireWriter<T>
{
    /// <summary>Encodes one value to the supplied sequential writer.</summary>
    void Write(IWriter writer, T value);
}

/// <summary>Combines the independent wire reader and writer directions.</summary>
public interface IWireCodec<T> : IWireReader<T>, IWireWriter<T> { }

/// <summary>Declares the exact payload size of a fixed-width codec.</summary>
public interface IFixedWireCodec<T> : IWireCodec<T>
{
    /// <summary>Gets the positive payload size in bytes.</summary>
    int Size { get; }
}

/// <summary>Optionally computes a variable payload size before encoding.</summary>
public interface IWireSizeProvider<T>
{
    /// <summary>Attempts to compute the encoded payload size without encoding it.</summary>
    bool TryGetEncodedSize(T value, out int size);
}

/// <summary>Identifies the value type represented by a registry descriptor.</summary>
public interface IWireCodecDescriptor
{
    /// <summary>Gets the exact registered value type.</summary>
    Type ValueType { get; }
}

/// <summary>Identifies how a codec payload is delimited.</summary>
public enum WireFramingKind
{
    /// <summary>The payload occupies an exact positive number of bytes.</summary>
    Fixed,
    /// <summary>A length prefix precedes the payload.</summary>
    LengthPrefixed,
    /// <summary>The codec itself recognizes the end of its payload.</summary>
    CodecOwned
}

/// <summary>Controls staging of payloads whose encoded size is unknown.</summary>
public enum VariablePayloadWritePolicy
{
    /// <summary>Requires a known size or safe fixed-prefix backpatching.</summary>
    RequireKnownLength,
    /// <summary>Allows explicitly bounded in-memory staging.</summary>
    AllowBuffering
}
