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

    /// <inheritdoc/>
    public int Depth => _frames.Count;

    /// <inheritdoc/>
    public bool IsEmpty => _frames.Count == 0;

    /// <inheritdoc/>
    public int MaxDepth { get; }

    /// <summary>Gets the frame at the top of the stack, or <see langword="null"/> when the stack is empty.</summary>
    public CallFrame? CurrentFrame => _frames.TryPeek(out var f) ? f : null;

    /// <summary>
    /// Initializes a new instance with the specified maximum stack depth.
    /// </summary>
    /// <param name="maxDepth">Maximum number of frames allowed. Defaults to <c>512</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDepth"/> is less than one.</exception>
    public CallStack(int maxDepth = 512)
    {
        if (maxDepth < 1) throw new ArgumentOutOfRangeException(nameof(maxDepth), "MaxDepth must be at least 1.");
        MaxDepth = maxDepth;
    }

    /// <summary>
    /// Initializes a new instance from a <see cref="VirtualMachineLimits"/>, using
    /// <see cref="VirtualMachineLimits.MaxCallStackDepth"/> as the maximum depth.
    /// </summary>
    /// <param name="limits">The limits policy to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is <see langword="null"/>.</exception>
    public CallStack(VirtualMachineLimits limits)
        : this((limits ?? throw new ArgumentNullException(nameof(limits))).MaxCallStackDepth)
    {
    }

    /// <inheritdoc/>
    public void Call(int returnAddress)
    {
        if (returnAddress < 0)
            throw new ArgumentOutOfRangeException(nameof(returnAddress),
                "Return address must be non-negative. Negative values are reserved as termination sentinels.");
        if (_frames.Count >= MaxDepth)
            throw new VmLimitExceededException(VmLimitKind.CallStackDepth, MaxDepth, _frames.Count + 1L);
        _frames.Push(new CallFrame(returnAddress));
    }

    /// <inheritdoc/>
    public int Return()
    {
        if (_frames.Count == 0) return -1;
        return _frames.Pop().ReturnAddress;
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
    /// <param name="data">The byte data containing the instruction stream.</param>
    public CallStackContext(ReadOnlyMemory<byte> data) : base(data)
    {
        CallStack = new CallStack();
    }

    /// <summary>
    /// Initializes a new instance with limits from a <see cref="VirtualMachineLimits"/>.
    /// Uses <see cref="VirtualMachineLimits.MaxCallStackDepth"/> and
    /// <see cref="VirtualMachineLimits.MaxOperandStackDepth"/>.
    /// </summary>
    /// <param name="data">The byte data containing the instruction stream.</param>
    /// <param name="limits">The limits policy to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is <see langword="null"/>.</exception>
    public CallStackContext(ReadOnlyMemory<byte> data, VirtualMachineLimits limits) : base(data, (limits ?? throw new ArgumentNullException(nameof(limits))).MaxOperandStackDepth)
    {
        CallStack = new CallStack(limits);
    }

    /// <summary>
    /// Initializes a new instance with a caller-supplied <see cref="ICallStack"/> implementation.
    /// </summary>
    /// <param name="data">The byte data containing the instruction stream.</param>
    /// <param name="callStack">The call stack to use.</param>
    public CallStackContext(ReadOnlyMemory<byte> data, ICallStack callStack) : base(data)
    {
        CallStack = callStack ?? throw new ArgumentNullException(nameof(callStack));
    }
}
