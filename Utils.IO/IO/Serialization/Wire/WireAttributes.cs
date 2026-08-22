using System;

namespace Utils.IO.Serialization;

/// <summary>Selects a concrete parameterless wire reader and/or writer for one serialized member.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class WireCodecAttribute : Attribute
{
    /// <summary>Initializes a member codec selection.</summary>
    public WireCodecAttribute(Type codecType) => CodecType = codecType ?? throw new ArgumentNullException(nameof(codecType));
    /// <summary>Gets the selected codec implementation type.</summary>
    public Type CodecType { get; }
}

/// <summary>Selects a concrete parameterless framing implementation for one serialized member.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class WireFramingAttribute : Attribute
{
    /// <summary>Initializes a member framing selection.</summary>
    public WireFramingAttribute(Type framingType) => FramingType = framingType ?? throw new ArgumentNullException(nameof(framingType));
    /// <summary>Gets the selected framing implementation type.</summary>
    public Type FramingType { get; }
}
