using System;
using System.Collections.Generic;

namespace Utils.VirtualMachine;

/// <summary>
/// A lightweight call stack that stores only return addresses, with no per-frame local-variable
/// storage. Prefer this over <see cref="CallStack"/> when frames do not need local variables.
/// </summary>
public class SimpleCallStack : ICallStack
{
    private readonly Stack<int> _returnAddresses = new();

    /// <inheritdoc/>
    public int Depth => _returnAddresses.Count;

    /// <inheritdoc/>
    public bool IsEmpty => _returnAddresses.Count == 0;

    /// <inheritdoc/>
    public int MaxDepth { get; }

    /// <summary>
    /// Initializes a new instance with the specified maximum stack depth.
    /// </summary>
    /// <param name="maxDepth">Maximum number of frames allowed. Defaults to <c>512</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDepth"/> is less than one.</exception>
    public SimpleCallStack(int maxDepth = 512)
    {
        if (maxDepth < 1) throw new ArgumentOutOfRangeException(nameof(maxDepth), "MaxDepth must be at least 1.");
        MaxDepth = maxDepth;
    }

    /// <inheritdoc/>
    public void Call(int returnAddress)
    {
        if (returnAddress < 0)
            throw new ArgumentOutOfRangeException(nameof(returnAddress),
                "Return address must be non-negative. Negative values are reserved as termination sentinels.");
        if (_returnAddresses.Count >= MaxDepth)
            throw new InvalidOperationException($"Call stack overflow: maximum depth of {MaxDepth} exceeded.");
        _returnAddresses.Push(returnAddress);
    }

    /// <inheritdoc/>
    public int Return()
    {
        if (_returnAddresses.Count == 0) return -1;
        return _returnAddresses.Pop();
    }

    /// <inheritdoc/>
    /// <remarks><see cref="SimpleCallStack"/> does not support per-frame locals; this property always returns <see langword="null"/>.</remarks>
    public CallFrame? CurrentFrame => null;
}
