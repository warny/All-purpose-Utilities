using System;

namespace Utils.IO.Serialization;

/// <summary>Encodes DateTime using the framework binary representation and preserves Kind semantics.</summary>
public sealed class DotNetBinaryDateTimeCodec : IFixedWireCodec<DateTime>
{
    /// <inheritdoc />
    public int Size => sizeof(long);
    /// <inheritdoc />
    public DateTime Read(IReader reader) => DateTime.FromBinary(reader.Read<long>());
    /// <inheritdoc />
    public void Write(IWriter writer, DateTime value) => writer.Write(value.ToBinary());
}

/// <summary>Encodes DateTime ticks and decodes them with Unspecified kind for legacy interoperability.</summary>
public sealed class TicksDateTimeCodec : IFixedWireCodec<DateTime>
{
    /// <inheritdoc />
    public int Size => sizeof(long);
    /// <inheritdoc />
    public DateTime Read(IReader reader) => new(reader.Read<long>(), DateTimeKind.Unspecified);
    /// <inheritdoc />
    public void Write(IWriter writer, DateTime value) => writer.Write(value.Ticks);
}

/// <summary>Encodes signed whole seconds from the Unix UTC epoch and decodes with Utc kind.</summary>
public sealed class UnixSecondsDateTimeCodec : IFixedWireCodec<DateTime>
{
    /// <inheritdoc />
    public int Size => sizeof(long);
    /// <inheritdoc />
    public DateTime Read(IReader reader) => DateTimeOffset.FromUnixTimeSeconds(reader.Read<long>()).UtcDateTime;
    /// <inheritdoc />
    public void Write(IWriter writer, DateTime value) => writer.Write(new DateTimeOffset(value.ToUniversalTime()).ToUnixTimeSeconds());
}

/// <summary>Encodes signed whole milliseconds from the Unix UTC epoch and decodes with Utc kind.</summary>
public sealed class UnixMillisecondsDateTimeCodec : IFixedWireCodec<DateTime>
{
    /// <inheritdoc />
    public int Size => sizeof(long);
    /// <inheritdoc />
    public DateTime Read(IReader reader) => DateTimeOffset.FromUnixTimeMilliseconds(reader.Read<long>()).UtcDateTime;
    /// <inheritdoc />
    public void Write(IWriter writer, DateTime value) => writer.Write(new DateTimeOffset(value.ToUniversalTime()).ToUnixTimeMilliseconds());
}

/// <summary>Encodes DateTime using the framework OLE Automation Double representation.</summary>
public sealed class OleAutomationDateTimeCodec : IFixedWireCodec<DateTime>
{
    /// <inheritdoc />
    public int Size => sizeof(double);
    /// <inheritdoc />
    public DateTime Read(IReader reader) => DateTime.FromOADate(reader.Read<double>());
    /// <inheritdoc />
    public void Write(IWriter writer, DateTime value) => writer.Write(value.ToOADate());
}

/// <summary>Encodes UTC Windows FILETIME and decodes it as a Utc DateTime.</summary>
public sealed class FileTimeDateTimeCodec : IFixedWireCodec<DateTime>
{
    /// <inheritdoc />
    public int Size => sizeof(long);
    /// <inheritdoc />
    public DateTime Read(IReader reader) => DateTime.FromFileTimeUtc(reader.Read<long>());
    /// <inheritdoc />
    public void Write(IWriter writer, DateTime value) => writer.Write(value.ToFileTimeUtc());
}
