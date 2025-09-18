using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Objects;

/// <summary>
/// Base class for creating types that validate their value against runtime constraints.
/// </summary>
/// <typeparam name="T">The wrapped value type.</typeparam>
public abstract class ConstrainedValue<T>
{
    /// <summary>
    /// Gets the validated value.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Initialises a new instance of the <see cref="ConstrainedValue{T}"/> class.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    protected ConstrainedValue(T value)
    {
        CheckValue(value);
        Value = value;
    }

    /// <summary>
    /// Validates the provided <paramref name="value"/> according to the derived class rules.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    protected abstract void CheckValue(T value);

    /// <inheritdoc />
    public override string ToString()
    {
        return Value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Converts a <see cref="ConstrainedValue{T}"/> to its underlying <typeparamref name="T"/> value.
    /// </summary>
    /// <param name="constrainedValue">The constrained value to unwrap.</param>
    public static implicit operator T(ConstrainedValue<T> constrainedValue) => constrainedValue.Value;
}
