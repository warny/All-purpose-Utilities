using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Objects;

/// <summary>
/// Base class for creating a type that automatically check at runtime its value specific constraints
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class ConstrainedValue<T>
{
    public T Value { get; }
    public ConstrainedValue(T value) {
        CheckValue(value);
        Value = value; 
    }

    protected abstract void CheckValue(T value);

    public override string ToString() { return Value.ToString(); }

    public static implicit operator T (ConstrainedValue<T> constrainedValue) => constrainedValue.Value;
}
