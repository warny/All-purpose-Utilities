using System;
using System.Collections.Generic;

namespace Utils.VirtualMachine;

/// <summary>
/// Represents a single activation frame on a <see cref="CallStack"/>, carrying the return address
/// and a per-frame local-variable store keyed by name.
/// </summary>
public sealed class CallFrame
{
    private readonly Dictionary<string, object?> _locals = [];

    /// <summary>Gets the instruction-stream offset to resume when this frame is popped.</summary>
    public int ReturnAddress { get; }

    /// <summary>Gets a read-only view of all local variables stored in this frame.</summary>
    public IReadOnlyDictionary<string, object?> Locals => _locals;

    internal CallFrame(int returnAddress) => ReturnAddress = returnAddress;

    /// <summary>
    /// Stores a local variable, overwriting any previous value under the same name.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="value">Value to store.</param>
    public void SetLocal(string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        _locals[name] = value;
    }

    /// <summary>
    /// Attempts to retrieve a typed local variable.
    /// </summary>
    /// <typeparam name="T">Expected type of the value.</typeparam>
    /// <param name="name">Variable name.</param>
    /// <param name="value">
    /// The typed value when this method returns <see langword="true"/>;
    /// otherwise <see langword="default"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the variable exists and its value is assignable to <typeparamref name="T"/>.
    /// </returns>
    public bool TryGetLocal<T>(string name, out T? value)
    {
        if (_locals.TryGetValue(name, out var raw) && raw is T t)
        {
            value = t;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Retrieves a typed local variable, throwing if the variable is absent or has an incompatible type.
    /// </summary>
    /// <typeparam name="T">Expected type of the value.</typeparam>
    /// <param name="name">Variable name.</param>
    /// <returns>The typed value stored under <paramref name="name"/>.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no variable with <paramref name="name"/> exists in this frame, or when the
    /// stored value cannot be cast to <typeparamref name="T"/>.
    /// </exception>
    public T GetLocal<T>(string name)
        => TryGetLocal<T>(name, out var v) ? v! : throw new KeyNotFoundException($"Local '{name}' not found or not assignable to {typeof(T).Name}.");
}
