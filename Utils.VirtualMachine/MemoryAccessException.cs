using System;

namespace Utils.VirtualMachine;

/// <summary>
/// The exception thrown when a virtual memory access violates the page's mapped access rights,
/// or when the target address is not mapped in the process's page table.
/// </summary>
public class MemoryAccessException : Exception
{
    /// <summary>Gets the virtual address at which the access violation occurred.</summary>
    public long VirtualAddress { get; }

    /// <summary>Gets the access type that was requested (read or write).</summary>
    public PageAccess RequestedAccess { get; }

    /// <summary>
    /// Gets the actual access rights of the mapped page, or <see langword="null"/> when the
    /// address was not mapped at all.
    /// </summary>
    public PageAccess? ActualAccess { get; }

    /// <summary>
    /// Initializes a new instance for a write attempt on a read-only page.
    /// </summary>
    /// <param name="virtualAddress">The virtual address that caused the violation.</param>
    /// <param name="requestedAccess">The access type that was requested.</param>
    /// <param name="actualAccess">The access rights actually granted for the mapped page.</param>
    public MemoryAccessException(long virtualAddress, PageAccess requestedAccess, PageAccess actualAccess)
        : base(BuildMessage(virtualAddress, requestedAccess, actualAccess))
    {
        VirtualAddress = virtualAddress;
        RequestedAccess = requestedAccess;
        ActualAccess = actualAccess;
    }

    /// <summary>
    /// Initializes a new instance for an access to an address that is not mapped in the
    /// process's page table.
    /// </summary>
    /// <param name="virtualAddress">The virtual address that caused the violation.</param>
    /// <param name="requestedAccess">The access type that was requested.</param>
    public MemoryAccessException(long virtualAddress, PageAccess requestedAccess)
        : base(BuildMessage(virtualAddress, requestedAccess, null))
    {
        VirtualAddress = virtualAddress;
        RequestedAccess = requestedAccess;
        ActualAccess = null;
    }

    private static string BuildMessage(long virtualAddress, PageAccess requestedAccess, PageAccess? actualAccess)
    {
        var requested = requestedAccess == PageAccess.ReadWrite ? "ReadWrite" : "ReadOnly";
        var actual = actualAccess.HasValue
            ? (actualAccess.Value == PageAccess.ReadOnly ? "ReadOnly" : "ReadWrite")
            : "not mapped";
        return $"Memory access violation at address 0x{virtualAddress:X}: requested {requested} but page is {actual}.";
    }
}
