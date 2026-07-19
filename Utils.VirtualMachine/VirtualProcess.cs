using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Utils.VirtualMachine;

/// <summary>
/// Represents a virtual process with its own page table mapping virtual page indices to physical
/// <see cref="VirtualPage"/> instances and their per-process access rights.
/// </summary>
/// <typeparam name="TAddress">
/// An integer type used for virtual addresses (e.g. <see cref="int"/>, <see cref="long"/>,
/// <see cref="uint"/>). Must implement <see cref="IBinaryInteger{TSelf}"/>.
/// </typeparam>
/// <remarks>
/// Instances are created exclusively by <see cref="VirtualMemory{TAddress}"/>. Call
/// <see cref="Read"/> or <see cref="Write"/> to access memory; both methods handle cross-page
/// operations transparently.
/// <para>
/// <see cref="Read"/> and <see cref="Write"/> are fully atomic: either the entire range is
/// validated and transferred, or no bytes are copied and a <see cref="MemoryAccessException"/>
/// is thrown. Partial updates do not occur.
/// </para>
/// <para>
/// Negative addresses are always rejected for signed <typeparamref name="TAddress"/> types.
/// </para>
/// <para>
/// <b>Thread safety:</b> this class is not thread-safe. All reads, writes, and mapping
/// operations must occur on the same thread. Sharing a page between multiple processes does not
/// provide any synchronization; concurrent access from multiple <see cref="VirtualProcess{TAddress}"/>
/// or <see cref="VirtualMemory{TAddress}"/> instances produces undefined behaviour.
/// </para>
/// </remarks>
public class VirtualProcess<TAddress> where TAddress : IBinaryInteger<TAddress>
{
    private readonly Dictionary<TAddress, (VirtualPage Page, PageAccess Access)> _pageTable = [];
    private readonly int _pageSize;
    private bool _isFreed;

    /// <summary>Gets the unique identifier assigned to this process.</summary>
    public int ProcessId { get; }

    /// <summary>
    /// Gets a value indicating whether this is the master process. The master process receives
    /// automatic <see cref="PageAccess.ReadWrite"/> mappings for every page allocated by
    /// <see cref="VirtualMemory{TAddress}.AllocatePage"/>.
    /// </summary>
    public bool IsMaster { get; }

    /// <summary>
    /// Gets a value indicating whether this process has been freed via
    /// <see cref="VirtualMemory{TAddress}.FreeProcess"/>. Freed processes reject all subsequent
    /// memory operations.
    /// </summary>
    public bool IsFreed => _isFreed;

    /// <summary>
    /// Gets all current page mappings for this process as an enumerable of
    /// (virtual page index, physical page, access rights) tuples.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the process has been freed.</exception>
    public IEnumerable<(TAddress VirtualPageIndex, VirtualPage Page, PageAccess Access)> Mappings
    {
        get
        {
            ThrowIfFreed();
            return _pageTable.Select(kv => (kv.Key, kv.Value.Page, kv.Value.Access));
        }
    }

    internal VirtualProcess(int processId, int pageSize, bool isMaster)
    {
        ProcessId = processId;
        _pageSize = pageSize;
        IsMaster = isMaster;
    }

    internal void MarkFreed() => _isFreed = true;

    /// <summary>Clears all page-table entries without checking <see cref="IsFreed"/>. For use by <see cref="VirtualMemory{TAddress}.FreeProcess"/> only.</summary>
    internal void ClearAllMappings() => _pageTable.Clear();

    internal void MapPage(TAddress virtualPageIndex, VirtualPage page, PageAccess access)
    {
        ThrowIfFreed();
        _pageTable[virtualPageIndex] = (page, access);
    }

    internal void UnmapPage(TAddress virtualPageIndex)
    {
        ThrowIfFreed();
        _pageTable.Remove(virtualPageIndex);
    }

    private void ThrowIfFreed()
    {
        if (_isFreed)
            throw new ObjectDisposedException(
                $"VirtualProcess #{ProcessId}",
                "This process has been freed and can no longer be used for memory operations.");
    }

    /// <summary>
    /// Reads <paramref name="destination"/><c>.Length</c> bytes starting at
    /// <paramref name="virtualAddress"/>. Transparently crosses page boundaries.
    /// The operation is atomic: either all bytes are copied or none are (the destination
    /// buffer is not modified when a <see cref="MemoryAccessException"/> is thrown).
    /// </summary>
    /// <param name="virtualAddress">The virtual address to read from. Must be non-negative for signed address types.</param>
    /// <param name="destination">Buffer that receives the bytes.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the process has been freed.</exception>
    /// <exception cref="MemoryAccessException">
    /// Thrown when the address is negative (for signed types) or when any byte of the range
    /// falls in an unmapped page. No bytes are copied in this case.
    /// </exception>
    public void Read(TAddress virtualAddress, Span<byte> destination)
    {
        ThrowIfFreed();
        // Validate the entire range first; no bytes are copied until all pages are confirmed mapped.
        ValidateRange(virtualAddress, destination.Length, PageAccess.ReadOnly);

        int remaining = destination.Length;
        int destOffset = 0;
        TAddress currentAddress = virtualAddress;
        while (remaining > 0)
        {
            var (pageIndex, byteOffset) = Decompose(currentAddress);
            var entry = _pageTable[pageIndex];
            int bytesInPage = Math.Min(remaining, _pageSize - byteOffset);
            entry.Page.Data.AsSpan(byteOffset, bytesInPage).CopyTo(destination.Slice(destOffset, bytesInPage));
            remaining -= bytesInPage;
            destOffset += bytesInPage;
            currentAddress += TAddress.CreateChecked(bytesInPage);
        }
    }

    /// <summary>
    /// Writes <paramref name="source"/> bytes starting at <paramref name="virtualAddress"/>.
    /// Transparently crosses page boundaries.
    /// The operation is atomic: either all bytes are written or none are (memory is not
    /// modified when a <see cref="MemoryAccessException"/> is thrown).
    /// </summary>
    /// <param name="virtualAddress">The virtual address to write to. Must be non-negative for signed address types.</param>
    /// <param name="source">Bytes to write.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the process has been freed.</exception>
    /// <exception cref="MemoryAccessException">
    /// Thrown when the address is negative (for signed types), when any byte of the range falls
    /// in an unmapped page, or when any touched page has <see cref="PageAccess.ReadOnly"/> rights.
    /// No bytes are written in this case.
    /// </exception>
    public void Write(TAddress virtualAddress, ReadOnlySpan<byte> source)
    {
        ThrowIfFreed();
        // Validate the entire range first; no bytes are written until all pages are confirmed writable.
        ValidateRange(virtualAddress, source.Length, PageAccess.ReadWrite);

        int remaining = source.Length;
        int srcOffset = 0;
        TAddress currentAddress = virtualAddress;
        while (remaining > 0)
        {
            var (pageIndex, byteOffset) = Decompose(currentAddress);
            var entry = _pageTable[pageIndex];
            int bytesInPage = Math.Min(remaining, _pageSize - byteOffset);
            source.Slice(srcOffset, bytesInPage).CopyTo(entry.Page.Data.AsSpan(byteOffset, bytesInPage));
            remaining -= bytesInPage;
            srcOffset += bytesInPage;
            currentAddress += TAddress.CreateChecked(bytesInPage);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="virtualAddress"/> falls within a mapped page.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the process has been freed.</exception>
    /// <exception cref="MemoryAccessException">Thrown when the address is negative for a signed address type.</exception>
    public bool IsAccessible(TAddress virtualAddress)
    {
        ThrowIfFreed();
        var (pageIndex, _) = Decompose(virtualAddress);
        return _pageTable.ContainsKey(pageIndex);
    }

    /// <summary>
    /// Returns the access rights for the page that contains <paramref name="virtualAddress"/>,
    /// or <see langword="null"/> when the address is not mapped.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the process has been freed.</exception>
    /// <exception cref="MemoryAccessException">Thrown when the address is negative for a signed address type.</exception>
    public PageAccess? GetAccess(TAddress virtualAddress)
    {
        ThrowIfFreed();
        var (pageIndex, _) = Decompose(virtualAddress);
        return _pageTable.TryGetValue(pageIndex, out var entry) ? entry.Access : null;
    }

    /// <summary>
    /// Validates that every page touched by <paramref name="length"/> bytes starting at
    /// <paramref name="startAddress"/> is mapped and has at least <paramref name="requiredAccess"/>.
    /// Throws <see cref="MemoryAccessException"/> on the first violation without modifying any state.
    /// </summary>
    private void ValidateRange(TAddress startAddress, int length, PageAccess requiredAccess)
    {
        if (length == 0) return;
        TAddress currentAddress = startAddress;
        int remaining = length;
        while (remaining > 0)
        {
            var (pageIndex, byteOffset) = Decompose(currentAddress);
            if (!_pageTable.TryGetValue(pageIndex, out var entry))
                throw new MemoryAccessException(FormatAddress(currentAddress), requiredAccess);
            if (requiredAccess == PageAccess.ReadWrite && entry.Access == PageAccess.ReadOnly)
                throw new MemoryAccessException(FormatAddress(currentAddress), requiredAccess, PageAccess.ReadOnly);
            int bytesInPage = Math.Min(remaining, _pageSize - byteOffset);
            remaining -= bytesInPage;
            currentAddress += TAddress.CreateChecked(bytesInPage);
        }
    }

    /// <summary>
    /// Decomposes a virtual address into a page index and byte offset within that page.
    /// Throws <see cref="MemoryAccessException"/> when <paramref name="virtualAddress"/> is
    /// negative for a signed address type.
    /// </summary>
    private (TAddress pageIndex, int offset) Decompose(TAddress virtualAddress)
    {
        if (TAddress.IsNegative(virtualAddress))
            throw new MemoryAccessException(
                FormatAddress(virtualAddress), PageAccess.ReadOnly);
        var ps = TAddress.CreateChecked(_pageSize);
        return (virtualAddress / ps, int.CreateChecked(virtualAddress % ps));
    }

    /// <summary>
    /// Formats a virtual address as a lossless hexadecimal string for use in
    /// <see cref="MemoryAccessException"/>. Avoids narrowing-conversion overflow when
    /// <typeparamref name="TAddress"/> is wider than <see cref="long"/>.
    /// </summary>
    private static string FormatAddress(TAddress address)
        => $"0x{address:X}";
}
