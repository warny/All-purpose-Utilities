using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Thrown exclusively by <see cref="INumberReader"/> implementations when the instruction
/// stream ends before all requested bytes can be read. Propagates only as far as the
/// <c>TryDispatch</c> catch block in <see cref="VirtualProcessor{T}"/>, where it is always
/// re-raised as a <see cref="VirtualProcessorException"/>. Never observable by callers.
/// </summary>
internal sealed class EndOfBytecodeException : Exception
{
    /// <summary>Instruction-stream address at which the read was attempted.</summary>
    internal int Address { get; }

    /// <summary>Number of bytes that were requested but unavailable.</summary>
    internal int RequestedWidth { get; }

    internal EndOfBytecodeException(int address, int requestedWidth)
        : base($"Unexpected end of bytecode at address {address}: expected {requestedWidth} byte(s).")
    {
        Address = address;
        RequestedWidth = requestedWidth;
    }
}
