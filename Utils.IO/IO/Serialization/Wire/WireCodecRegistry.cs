using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Utils.IO.Serialization;

/// <summary>Stores exact-type wire codec registrations before a reader or writer takes a snapshot.</summary>
public sealed class WireCodecRegistry
{
    private readonly Dictionary<Type, WireCodecRegistration> registrations = [];

    /// <summary>Registers both directions for an exact type.</summary>
    public void Set<T>(IWireCodec<T> codec, IWireFraming? framing = null) => Set(codec, codec, framing);
    /// <summary>Registers a reader direction for an exact type while preserving any writer registration.</summary>
    public void SetReader<T>(IWireReader<T> reader, IWireFraming? framing = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        registrations.TryGetValue(typeof(T), out WireCodecRegistration? old);
        registrations[typeof(T)] = new(typeof(T), reader, old?.Writer, framing ?? old?.Framing);
    }
    /// <summary>Registers a writer direction for an exact type while preserving any reader registration.</summary>
    public void SetWriter<T>(IWireWriter<T> writer, IWireFraming? framing = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        registrations.TryGetValue(typeof(T), out WireCodecRegistration? old);
        registrations[typeof(T)] = new(typeof(T), old?.Reader, writer, framing ?? old?.Framing);
    }
    /// <summary>Registers independent reader and writer directions for an exact type.</summary>
    public void Set<T>(IWireReader<T>? reader, IWireWriter<T>? writer, IWireFraming? framing = null)
    {
        if (reader is null && writer is null) throw new ArgumentException("At least one codec direction is required.");
        registrations[typeof(T)] = new(typeof(T), reader, writer, framing);
    }
    /// <summary>Creates an immutable registry snapshot.</summary>
    internal IReadOnlyDictionary<Type, WireCodecRegistration> Snapshot() => registrations.ToImmutableDictionary();
}

/// <summary>Configures shared wire contracts and bounded variable-payload staging.</summary>
public sealed class SerializationOptions
{
    /// <summary>Gets exact-type codec registrations.</summary>
    public WireCodecRegistry Codecs { get; } = new();
    /// <summary>Gets or sets the policy for unknown-size payloads. The default forbids implicit buffering.</summary>
    public VariablePayloadWritePolicy VariablePayloadWritePolicy { get; set; } = VariablePayloadWritePolicy.RequireKnownLength;
    /// <summary>Gets or sets the positive maximum staged payload size.</summary>
    public int MaximumBufferedPayloadLength { get; set; } = 1024 * 1024;
}

/// <summary>Stores one non-generic exact-type registration.</summary>
internal sealed record WireCodecRegistration(Type ValueType, object? Reader, object? Writer, IWireFraming? Framing) : IWireCodecDescriptor;
