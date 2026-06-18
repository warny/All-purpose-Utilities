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
    private int _maxDepth = 512;

    /// <inheritdoc/>
    public int Depth => _returnAddresses.Count;

    /// <inheritdoc/>
    public bool IsEmpty => _returnAddresses.Count == 0;

    /// <inheritdoc/>
    public int MaxDepth
    {
        get => _maxDepth;
        set
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "MaxDepth must be at least 1.");
            _maxDepth = value;
        }
    }

    /// <inheritdoc/>
    public void Call(int returnAddress)
    {
        if (_returnAddresses.Count >= _maxDepth)
            throw new InvalidOperationException($"Call stack overflow: maximum depth of {_maxDepth} exceeded.");
        _returnAddresses.Push(returnAddress);
    }

    /// <inheritdoc/>
    public int Return()
    {
        if (_returnAddresses.Count == 0)
            throw new InvalidOperationException("Call stack underflow: cannot return from an empty call stack.");
        return _returnAddresses.Pop();
    }

    /// <inheritdoc/>
    public bool TryReturn(out int returnAddress)
    {
        if (_returnAddresses.Count == 0) { returnAddress = 0; return false; }
        returnAddress = _returnAddresses.Pop();
        return true;
    }
}
