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

    /// <summary>
    /// Gets a read-only view of all local variables stored in this frame.
    /// </summary>
    /// <remarks>
    /// This is a live view over the internal dictionary, not a snapshot. Enumerating this
    /// property while the frame is being modified concurrently or re-entrantly is not safe.
    /// For stable diagnostic snapshots, copy the result: <c>frame.Locals.ToDictionary(...)</c>.
    /// </remarks>
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
    /// Returns <see langword="true"/> when a local variable with the given name has been stored,
    /// regardless of its value or type.
    /// </summary>
    /// <param name="name">Variable name.</param>
    public bool ContainsLocal(string name) => _locals.ContainsKey(name);

    /// <summary>
    /// Attempts to retrieve a typed local variable.
    /// Returns <see langword="false"/> when the variable is absent; when the stored value is
    /// <see langword="null"/> and <typeparamref name="T"/> is a reference or nullable value type,
    /// sets <paramref name="value"/> to <see langword="null"/> and returns <see langword="true"/>.
    /// </summary>
    /// <typeparam name="T">Expected type of the value.</typeparam>
    /// <param name="name">Variable name.</param>
    /// <param name="value">
    /// The typed value when this method returns <see langword="true"/>;
    /// otherwise <see langword="default"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the variable exists and its value is compatible with
    /// <typeparamref name="T"/> (including a stored <see langword="null"/> for nullable types).
    /// <see langword="false"/> if the variable is absent or its non-null value cannot be assigned to <typeparamref name="T"/>.
    /// </returns>
    public bool TryGetLocal<T>(string name, out T? value)
    {
        if (!_locals.TryGetValue(name, out var raw))
        {
            value = default;
            return false;
        }
        if (raw is null)
        {
            // Null is compatible with reference types and nullable value types.
            value = default;
            return !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null;
        }
        if (raw is T t)
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
    /// <exception cref="KeyNotFoundException">Thrown when no variable with <paramref name="name"/> exists in this frame.</exception>
    /// <exception cref="InvalidCastException">Thrown when the stored value cannot be assigned to <typeparamref name="T"/>.</exception>
    public T? GetLocal<T>(string name)
    {
        if (!_locals.TryGetValue(name, out var raw))
            throw new KeyNotFoundException($"Local '{name}' not found in this frame.");
        if (raw is null)
        {
            if (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null)
                return default;
            throw new InvalidCastException($"Local '{name}' is null, which is not assignable to non-nullable value type {typeof(T).Name}.");
        }
        if (raw is T t) return t;
        throw new InvalidCastException($"Local '{name}' has type {raw.GetType().Name}, which is not assignable to {typeof(T).Name}.");
    }
}
