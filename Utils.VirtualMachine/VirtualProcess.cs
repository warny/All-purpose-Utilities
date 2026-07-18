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

    internal void MapPage(TAddress virtualPageIndex, VirtualPage page, PageAccess access)
        => _pageTable[virtualPageIndex] = (page, access);

    internal void UnmapPage(TAddress virtualPageIndex)
        => _pageTable.Remove(virtualPageIndex);

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
    /// </summary>
    /// <param name="virtualAddress">The virtual address to read from.</param>
    /// <param name="destination">Buffer that receives the bytes.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the process has been freed.</exception>
    /// <exception cref="MemoryAccessException">
    /// Thrown when any byte of the range falls in an unmapped page.
    /// </exception>
    public void Read(TAddress virtualAddress, Span<byte> destination)
    {
        ThrowIfFreed();
        int remaining = destination.Length;
        int destOffset = 0;
        TAddress currentAddress = virtualAddress;

        while (remaining > 0)
        {
            var (pageIndex, byteOffset) = Decompose(currentAddress);
            if (!_pageTable.TryGetValue(pageIndex, out var entry))
                throw new MemoryAccessException(long.CreateChecked(currentAddress), PageAccess.ReadOnly);

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
    /// </summary>
    /// <param name="virtualAddress">The virtual address to write to.</param>
    /// <param name="source">Bytes to write.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the process has been freed.</exception>
    /// <exception cref="MemoryAccessException">
    /// Thrown when any byte of the range falls in an unmapped page or in a
    /// <see cref="PageAccess.ReadOnly"/> page.
    /// </exception>
    public void Write(TAddress virtualAddress, ReadOnlySpan<byte> source)
    {
        ThrowIfFreed();
        int remaining = source.Length;
        int srcOffset = 0;
        TAddress currentAddress = virtualAddress;

        while (remaining > 0)
        {
            var (pageIndex, byteOffset) = Decompose(currentAddress);
            if (!_pageTable.TryGetValue(pageIndex, out var entry))
                throw new MemoryAccessException(long.CreateChecked(currentAddress), PageAccess.ReadWrite);
            if (entry.Access == PageAccess.ReadOnly)
                throw new MemoryAccessException(long.CreateChecked(currentAddress), PageAccess.ReadWrite, PageAccess.ReadOnly);

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
    public PageAccess? GetAccess(TAddress virtualAddress)
    {
        ThrowIfFreed();
        var (pageIndex, _) = Decompose(virtualAddress);
        return _pageTable.TryGetValue(pageIndex, out var entry) ? entry.Access : null;
    }

    private (TAddress pageIndex, int offset) Decompose(TAddress virtualAddress)
    {
        var ps = TAddress.CreateChecked(_pageSize);
        return (virtualAddress / ps, int.CreateChecked(virtualAddress % ps));
    }
}
