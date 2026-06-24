using System;
using System.Numerics;

namespace Utils.VirtualMachine;

/// <summary>
/// A <see cref="DefaultContext"/> that associates an instruction stream with a specific
/// <see cref="VirtualProcess{TAddress}"/>, allowing the executing processor to perform
/// virtual memory reads and writes on behalf of that process.
/// </summary>
/// <typeparam name="TAddress">
/// The integer address type used by the associated process. Must implement
/// <see cref="IBinaryInteger{TSelf}"/>.
/// </typeparam>
public class VirtualMemoryContext<TAddress> : DefaultContext
    where TAddress : IBinaryInteger<TAddress>
{
    /// <summary>Gets the virtual process associated with this execution context.</summary>
    public VirtualProcess<TAddress> Process { get; }

    /// <summary>
    /// Initializes a new instance with an external instruction buffer and an associated process.
    /// Use this overload when instructions reside in a managed buffer (ROM, pre-loaded code, etc.)
    /// rather than in a virtual memory page.
    /// </summary>
    /// <param name="instructionData">The instruction stream.</param>
    /// <param name="process">The process that owns the execution context.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="process"/> is <see langword="null"/>.</exception>
    public VirtualMemoryContext(ReadOnlyMemory<byte> instructionData, VirtualProcess<TAddress> process)
        : base(instructionData)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
    }

    /// <summary>
    /// Initializes a new instance whose instruction stream is the content of a
    /// <see cref="VirtualPage"/>. The page's current byte content is exposed read-only via
    /// <see cref="Context.Data"/>.
    /// </summary>
    /// <param name="instructionPage">The page whose bytes serve as the instruction stream.</param>
    /// <param name="process">The process that owns the execution context.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="instructionPage"/> or <paramref name="process"/> is <see langword="null"/>.
    /// </exception>
    public VirtualMemoryContext(VirtualPage instructionPage, VirtualProcess<TAddress> process)
        : base((instructionPage ?? throw new ArgumentNullException(nameof(instructionPage))).AsReadOnlyMemory())
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
    }
}
