using System;

namespace Utils.VirtualMachine;

/// <summary>
/// The exception thrown when a virtual memory access violates the page's mapped access rights,
/// or when the target address is not mapped or is invalid in the process's page table.
/// </summary>
public class MemoryAccessException : Exception
{
    /// <summary>
    /// Gets the virtual address at which the access violation occurred, formatted as a
    /// hexadecimal string (e.g. <c>"0x00001234"</c>). Using a string representation avoids
    /// narrowing-conversion overflow when the address type is wider than <see cref="long"/>
    /// (e.g. <see cref="ulong"/> addresses above <see cref="long.MaxValue"/>).
    /// </summary>
    public string VirtualAddressText { get; }

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
    /// <param name="virtualAddressText">The virtual address that caused the violation, as a hex string.</param>
    /// <param name="requestedAccess">The access type that was requested.</param>
    /// <param name="actualAccess">The access rights actually granted for the mapped page.</param>
    public MemoryAccessException(string virtualAddressText, PageAccess requestedAccess, PageAccess actualAccess)
        : base(BuildMessage(virtualAddressText, requestedAccess, actualAccess))
    {
        VirtualAddressText = virtualAddressText;
        RequestedAccess = requestedAccess;
        ActualAccess = actualAccess;
    }

    /// <summary>
    /// Initializes a new instance for an access to an address that is not mapped in the
    /// process's page table, or whose address value is invalid (e.g. negative for a signed
    /// address type).
    /// </summary>
    /// <param name="virtualAddressText">The virtual address that caused the violation, as a hex string.</param>
    /// <param name="requestedAccess">The access type that was requested.</param>
    public MemoryAccessException(string virtualAddressText, PageAccess requestedAccess)
        : base(BuildMessage(virtualAddressText, requestedAccess, null))
    {
        VirtualAddressText = virtualAddressText;
        RequestedAccess = requestedAccess;
        ActualAccess = null;
    }

    private static string BuildMessage(string virtualAddressText, PageAccess requestedAccess, PageAccess? actualAccess)
    {
        var requested = requestedAccess == PageAccess.ReadWrite ? "ReadWrite" : "ReadOnly";
        var actual = actualAccess.HasValue
            ? (actualAccess.Value == PageAccess.ReadOnly ? "ReadOnly" : "ReadWrite")
            : "not mapped";
        return $"Memory access violation at address {virtualAddressText}: requested {requested} but page is {actual}.";
    }
}
