using System;
using System.Collections.Generic;

namespace Utils.VirtualMachine;

/// <summary>
/// A frame-based call stack that stores a <see cref="CallFrame"/> per subroutine invocation,
/// providing access to per-frame local variables.
/// </summary>
/// <remarks>
/// Use <see cref="SimpleCallStack"/> when local-variable storage is not required and minimal
/// allocation overhead is preferred.
/// </remarks>
public class CallStack : ICallStack
{
    private readonly Stack<CallFrame> _frames = new();
    private int _maxDepth = 512;

    /// <inheritdoc/>
    public int Depth => _frames.Count;

    /// <inheritdoc/>
    public bool IsEmpty => _frames.Count == 0;

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

    /// <summary>Gets the frame at the top of the stack, or <see langword="null"/> when the stack is empty.</summary>
    public CallFrame? CurrentFrame => _frames.TryPeek(out var f) ? f : null;

    /// <inheritdoc/>
    public void Call(int returnAddress)
    {
        if (_frames.Count >= _maxDepth)
            throw new InvalidOperationException($"Call stack overflow: maximum depth of {_maxDepth} exceeded.");
        _frames.Push(new CallFrame(returnAddress));
    }

    /// <inheritdoc/>
    public int Return()
    {
        if (_frames.Count == 0)
            throw new InvalidOperationException("Call stack underflow: cannot return from an empty call stack.");
        return _frames.Pop().ReturnAddress;
    }

    /// <inheritdoc/>
    public bool TryReturn(out int returnAddress)
    {
        if (_frames.Count == 0) { returnAddress = 0; return false; }
        returnAddress = _frames.Pop().ReturnAddress;
        return true;
    }
}

/// <summary>
/// A <see cref="DefaultContext"/> that carries an <see cref="ICallStack"/> for processors that
/// implement subroutine calls.
/// </summary>
public class CallStackContext : DefaultContext
{
    /// <summary>Gets the call stack used to track subroutine return addresses.</summary>
    public ICallStack CallStack { get; }

    /// <summary>
    /// Initializes a new instance with a default <see cref="CallStack"/>.
    /// </summary>
    /// <param name="data">The byte array containing the instruction stream.</param>
    public CallStackContext(byte[] data) : base(data)
    {
        CallStack = new CallStack();
    }

    /// <summary>
    /// Initializes a new instance with a caller-supplied <see cref="ICallStack"/> implementation.
    /// </summary>
    /// <param name="data">The byte array containing the instruction stream.</param>
    /// <param name="callStack">The call stack to use.</param>
    public CallStackContext(byte[] data, ICallStack callStack) : base(data)
    {
        CallStack = callStack ?? throw new ArgumentNullException(nameof(callStack));
    }
}
