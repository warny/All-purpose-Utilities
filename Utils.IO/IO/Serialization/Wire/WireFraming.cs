using System;

namespace Utils.IO.Serialization;

/// <summary>Describes payload delimitation independently from value encoding.</summary>
public interface IWireFraming
{
    /// <summary>Gets the framing category.</summary>
    WireFramingKind Kind { get; }
}

/// <summary>Describes a fixed-size payload.</summary>
public sealed class FixedWireFraming : IWireFraming
{
    /// <summary>Initializes fixed framing with the exact positive byte count.</summary>
    public FixedWireFraming(int size)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        Size = size;
    }

    /// <summary>Gets the exact payload size.</summary>
    public int Size { get; }

    /// <inheritdoc />
    public WireFramingKind Kind => WireFramingKind.Fixed;
}

/// <summary>Declares a codec-owned, self-delimited payload.</summary>
public sealed class CodecOwnedWireFraming : IWireFraming
{
    /// <inheritdoc />
    public WireFramingKind Kind => WireFramingKind.CodecOwned;
}

/// <summary>Reads and writes payload lengths without encoding payload values.</summary>
public interface IWireLengthFraming : IWireFraming
{
    /// <summary>Gets whether every prefix has a constant width.</summary>
    bool HasFixedPrefixSize { get; }
    /// <summary>Gets the fixed prefix width, or zero for variable-width prefixes.</summary>
    int PrefixSize { get; }
    /// <summary>Reads and validates a non-negative payload length.</summary>
    int ReadLength(IReader reader);
    /// <summary>Writes a representable non-negative payload length.</summary>
    void WriteLength(IWriter writer, int length);
}

/// <summary>Uses a signed 32-bit payload-length prefix.</summary>
public sealed class Int32LengthWireFraming : IWireLengthFraming
{
    /// <inheritdoc />
    public WireFramingKind Kind => WireFramingKind.LengthPrefixed;
    /// <inheritdoc />
    public bool HasFixedPrefixSize => true;
    /// <inheritdoc />
    public int PrefixSize => sizeof(int);
    /// <inheritdoc />
    public int ReadLength(IReader reader)
    {
        int length = reader.Read<int>();
        if (length < 0) throw new InvalidOperationException($"Negative wire payload length {length} is invalid.");
        return length;
    }
    /// <inheritdoc />
    public void WriteLength(IWriter writer, int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        writer.Write(length);
    }
}
