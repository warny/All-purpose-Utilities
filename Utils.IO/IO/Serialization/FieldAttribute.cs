using System;

namespace Utils.IO.Serialization;

/// <summary>
/// Marks a field or property as part of the binary serialization process.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class FieldAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldAttribute"/> class.
    /// </summary>
    /// <param name="order">Order in which the member is serialized.</param>
    public FieldAttribute(int order)
    {
        Order = order;
    }

    /// <summary>
    /// Gets the serialization order for the member.
    /// </summary>
    public int Order { get; }
}
