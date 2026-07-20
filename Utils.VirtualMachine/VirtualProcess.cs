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
/// <b>Atomicity:</b> <see cref="Read"/> and <see cref="Write"/> are fully atomic for
/// supported single-threaded use. All range validation occurs before any byte is transferred.
/// Either the entire requested range is successfully processed, or a
/// <see cref="MemoryAccessException"/> is thrown and no memory or destination buffer is modified.
/// Partial updates do not occur. This guarantee applies to the supported single-threaded use
/// model; it does not imply hardware atomicity or thread safety.
/// </para>
/// <para>
/// <b>Address range:</b> virtual address ranges are strictly contiguous. A range must not wrap
/// around the address space (e.g. from <c>uint.MaxValue</c> to <c>0</c>). Any operation whose
/// end address exceeds the maximum representable value of <typeparamref name="TAddress"/> is
/// rejected before copying any byte.
/// </para>
/// <para>
/// <b>Negative addresses</b> are always rejected for signed <typeparamref name="TAddress"/> types,
/// including for zero-length operations.
/// </para>
/// <para>
/// <b>Zero-length operations:</b> <see cref="Read"/> and <see cref="Write"/> with an empty span
/// succeed without requiring any page mapping, provided the start address is non-negative.
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
    /// Gets a snapshot of all current page mappings for this process as a list of
    /// (virtual page index, physical page, access rights) tuples.
    /// </summary>
    /// <remarks>
    /// The returned list is an immutable snapshot taken at the moment the property is accessed.
    /// Subsequent calls to <see cref="MapPage"/>, <see cref="UnmapPage"/>, or
    /// <see cref="VirtualMemory{TAddress}.FreeProcess"/> do not affect the returned list.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when the process has been freed.</exception>
    public IReadOnlyList<(TAddress VirtualPageIndex, VirtualPage Page, PageAccess Access)> Mappings
    {
        get
        {
            ThrowIfFreed();
            return _pageTable.Select(kv => (kv.Key, kv.Value.Page, kv.Value.Access)).ToArray();
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

    /// <summary>
    /// Removes all virtual page table entries that reference <paramref name="page"/>, without
    /// checking <see cref="IsFreed"/>. For use by <see cref="VirtualMemory{TAddress}.FreePage"/> only.
    /// </summary>
    internal void RemoveMappingsForPage(VirtualPage page)
    {
        var keysToRemove = _pageTable
            .Where(kv => ReferenceEquals(kv.Value.Page, page))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in keysToRemove)
            _pageTable.Remove(key);
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="page"/> is mapped into this process with at least <see cref="PageAccess.ReadOnly"/> access.</summary>
    internal bool ContainsPage(VirtualPage page)
        => _pageTable.Values.Any(entry => ReferenceEquals(entry.Page, page));

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
    /// </summary>
    /// <param name="virtualAddress">The virtual address to read from. Must be non-negative for signed address types.</param>
    /// <param name="destination">Buffer that receives the bytes.</param>
    /// <remarks>
    /// The operation is atomic: all validation is performed before any byte is copied. If any
    /// byte of the range is invalid (negative address, range overflow, unmapped page), no bytes
    /// are written to <paramref name="destination"/> and a <see cref="MemoryAccessException"/>
    /// is thrown. A zero-length <paramref name="destination"/> succeeds without requiring a mapping.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when the process has been freed.</exception>
    /// <exception cref="MemoryAccessException">
    /// Thrown when the address is negative (for signed types), when the range exceeds the maximum
    /// representable address, or when any byte of the range falls in an unmapped page.
    /// No bytes are copied in this case.
    /// </exception>
    public void Read(TAddress virtualAddress, Span<byte> destination)
    {
        ThrowIfFreed();
        var plan = BuildTransferPlan(virtualAddress, destination.Length, PageAccess.ReadOnly);
        int offset = 0;
        foreach (var segment in plan)
        {
            segment.Page.Data
                .AsSpan(segment.PageOffset, segment.Length)
                .CopyTo(destination.Slice(offset, segment.Length));
            offset += segment.Length;
        }
    }

    /// <summary>
    /// Writes <paramref name="source"/> bytes starting at <paramref name="virtualAddress"/>.
    /// Transparently crosses page boundaries.
    /// </summary>
    /// <param name="virtualAddress">The virtual address to write to. Must be non-negative for signed address types.</param>
    /// <param name="source">Bytes to write.</param>
    /// <remarks>
    /// The operation is atomic: all validation is performed before any byte is written. If any
    /// byte of the range is invalid (negative address, range overflow, unmapped page, read-only
    /// page), no physical page is modified and a <see cref="MemoryAccessException"/> is thrown.
    /// A zero-length <paramref name="source"/> succeeds without requiring a mapping.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when the process has been freed.</exception>
    /// <exception cref="MemoryAccessException">
    /// Thrown when the address is negative (for signed types), when the range exceeds the maximum
    /// representable address, when any byte of the range falls in an unmapped page, or when any
    /// touched page has <see cref="PageAccess.ReadOnly"/> rights.
    /// No bytes are written in this case.
    /// </exception>
    public void Write(TAddress virtualAddress, ReadOnlySpan<byte> source)
    {
        ThrowIfFreed();
        var plan = BuildTransferPlan(virtualAddress, source.Length, PageAccess.ReadWrite);
        int offset = 0;
        foreach (var segment in plan)
        {
            source.Slice(offset, segment.Length)
                .CopyTo(segment.Page.Data.AsSpan(segment.PageOffset, segment.Length));
            offset += segment.Length;
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
    /// A validated segment of a multi-page transfer: the physical page to read from or write to,
    /// the byte offset within that page, and the number of bytes in this fragment.
    /// </summary>
    private readonly record struct MemorySegment(VirtualPage Page, int PageOffset, int Length);

    /// <summary>
    /// Validates the full address range and collects one <see cref="MemorySegment"/> per touched
    /// page. All checks complete before any byte is transferred; the returned array is ready to
    /// execute without further validation.
    /// </summary>
    /// <param name="startAddress">The first byte's virtual address.</param>
    /// <param name="length">Number of bytes to transfer. Must be non-negative.</param>
    /// <param name="requiredAccess">
    /// <see cref="PageAccess.ReadOnly"/> for reads, <see cref="PageAccess.ReadWrite"/> for writes.
    /// </param>
    /// <returns>
    /// An array of segments covering the requested range in address order. Empty when
    /// <paramref name="length"/> is zero.
    /// </returns>
    /// <exception cref="MemoryAccessException">
    /// Thrown when:
    /// <list type="bullet">
    ///   <item><paramref name="startAddress"/> is negative for a signed address type;</item>
    ///   <item>the range <c>[startAddress, startAddress + length − 1]</c> wraps around or exceeds
    ///   the maximum representable <typeparamref name="TAddress"/> value;</item>
    ///   <item>any touched page is not mapped in this process's page table;</item>
    ///   <item><paramref name="requiredAccess"/> is <see cref="PageAccess.ReadWrite"/> and any
    ///   touched page has <see cref="PageAccess.ReadOnly"/> rights.</item>
    /// </list>
    /// No state is mutated when this method throws.
    /// </exception>
    private MemorySegment[] BuildTransferPlan(TAddress startAddress, int length, PageAccess requiredAccess)
    {
        // Negative addresses are always invalid, even for zero-length operations.
        if (TAddress.IsNegative(startAddress))
            throw new MemoryAccessException(FormatAddress(startAddress), requiredAccess);

        // Zero-length: succeed immediately without requiring any mapping.
        if (length == 0)
            return [];

        // Validate the full address range before traversing mappings.
        // Required: startAddress + (length − 1) must be representable by TAddress.
        // Using checked arithmetic catches both signed overflow (int, long, sbyte) and
        // unsigned wrap-around (uint, ulong, byte). This is the same pattern already used
        // in VirtualMemory.AllocatePage.
        try
        {
            var lengthMinusOne = TAddress.CreateChecked(length - 1);
            _ = checked(startAddress + lengthMinusOne);
        }
        catch (OverflowException)
        {
            throw new MemoryAccessException(FormatAddress(startAddress), requiredAccess);
        }

        // Collect one segment per touched page.
        // Loop termination: `remaining` decreases by at least 1 per iteration (bytesInPage ≥ 1
        // since _pageSize ≥ 1 and byteOffset < _pageSize). At most `length` iterations total.
        // Address arithmetic: the pre-validated range guarantees no intermediate address overflows.
        int estimatedSegments = _pageSize > 1 ? (length - 1) / _pageSize + 2 : length;
        var segments = new List<MemorySegment>(capacity: estimatedSegments);
        int remaining = length;
        TAddress currentAddress = startAddress;

        while (remaining > 0)
        {
            var (pageIndex, byteOffset) = Decompose(currentAddress);
            if (!_pageTable.TryGetValue(pageIndex, out var entry))
                throw new MemoryAccessException(FormatAddress(currentAddress), requiredAccess);
            if (requiredAccess == PageAccess.ReadWrite && entry.Access == PageAccess.ReadOnly)
                throw new MemoryAccessException(FormatAddress(currentAddress), requiredAccess, PageAccess.ReadOnly);

            int bytesInPage = Math.Min(remaining, _pageSize - byteOffset);
            segments.Add(new MemorySegment(entry.Page, byteOffset, bytesInPage));
            remaining -= bytesInPage;
            currentAddress += TAddress.CreateChecked(bytesInPage);
        }

        return [.. segments];
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
