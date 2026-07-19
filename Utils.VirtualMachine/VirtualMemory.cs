using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Utils.VirtualMachine;

/// <summary>
/// Manages a pool of physical <see cref="VirtualPage"/> instances and a set of
/// <see cref="VirtualProcess{TAddress}"/> objects, each with their own virtual address space.
/// </summary>
/// <typeparam name="TAddress">
/// An integer type used for virtual addresses (e.g. <see cref="int"/>, <see cref="long"/>,
/// <see cref="uint"/>). Must implement <see cref="IBinaryInteger{TSelf}"/>.
/// </typeparam>
/// <remarks>
/// The master process (accessible via <see cref="MasterProcess"/>) automatically receives a
/// <see cref="PageAccess.ReadWrite"/> mapping for every page created by <see cref="AllocatePage"/>.
/// Additional processes created with <see cref="CreateProcess"/> start with an empty page table;
/// use <see cref="MapPage"/> to grant them access to specific pages.
/// </remarks>
public class VirtualMemory<TAddress> where TAddress : IBinaryInteger<TAddress>
{
    private readonly List<VirtualPage> _pages = [];
    private readonly List<VirtualProcess<TAddress>> _processes = [];
    private int _nextPageId;
    private int _nextProcessId;
    private TAddress _masterNextVirtualIndex = TAddress.Zero;

    /// <summary>Gets the size in bytes of each physical page.</summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets the master process. It automatically receives <see cref="PageAccess.ReadWrite"/>
    /// mappings for every page allocated via <see cref="AllocatePage"/>.
    /// </summary>
    public VirtualProcess<TAddress> MasterProcess { get; }

    /// <summary>Gets the list of all physical pages managed by this instance.</summary>
    public IReadOnlyList<VirtualPage> Pages => _pages;

    /// <summary>Gets the list of all processes, including the master process.</summary>
    public IReadOnlyList<VirtualProcess<TAddress>> Processes => _processes;

    /// <summary>
    /// Initializes a new <see cref="VirtualMemory{TAddress}"/> and creates the master process.
    /// </summary>
    /// <param name="pageSize">Size in bytes of each physical page. Defaults to <c>4096</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageSize"/> is less than one.</exception>
    public VirtualMemory(int pageSize = 4096)
    {
        if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be at least 1.");
        PageSize = pageSize;
        MasterProcess = new VirtualProcess<TAddress>(_nextProcessId++, pageSize, isMaster: true);
        _processes.Add(MasterProcess);
    }

    /// <summary>
    /// Allocates a new physical page and automatically maps it into the master process
    /// at the next sequential virtual page index with <see cref="PageAccess.ReadWrite"/> rights.
    /// </summary>
    /// <returns>The newly allocated page.</returns>
    public VirtualPage AllocatePage()
    {
        if (_nextPageId == int.MaxValue)
            throw new InvalidOperationException(
                $"Cannot allocate more pages: the page identifier counter has reached its maximum value ({int.MaxValue}).");
        TAddress nextIndex;
        try { nextIndex = checked(_masterNextVirtualIndex + TAddress.One); }
        catch (OverflowException)
        {
            throw new InvalidOperationException(
                "Cannot allocate more pages: the master process virtual address space is exhausted.");
        }
        var page = new VirtualPage(_nextPageId++, PageSize);
        _pages.Add(page);
        MasterProcess.MapPage(_masterNextVirtualIndex, page, PageAccess.ReadWrite);
        _masterNextVirtualIndex = nextIndex;
        return page;
    }

    /// <summary>
    /// Creates a new non-master process with an empty page table and registers it with this
    /// <see cref="VirtualMemory{TAddress}"/>.
    /// </summary>
    /// <returns>The new process.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the process identifier counter has reached its maximum value.</exception>
    public VirtualProcess<TAddress> CreateProcess()
    {
        if (_nextProcessId == int.MaxValue)
            throw new InvalidOperationException(
                $"Cannot create more processes: the process identifier counter has reached its maximum value ({int.MaxValue}).");
        var process = new VirtualProcess<TAddress>(_nextProcessId++, PageSize, isMaster: false);
        _processes.Add(process);
        return process;
    }

    /// <summary>
    /// Maps <paramref name="page"/> into <paramref name="process"/>'s virtual address space at
    /// <paramref name="virtualPageIndex"/> with the given <paramref name="access"/> rights.
    /// An existing mapping at that index is replaced.
    /// </summary>
    /// <param name="process">The process to map the page into.</param>
    /// <param name="page">The page to map. Must have been allocated by this instance.</param>
    /// <param name="virtualPageIndex">The virtual page index within the process's address space.</param>
    /// <param name="access">The access rights granted to the process for this page.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="process"/> or <paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="process"/> or <paramref name="page"/> was not created by this instance.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when <paramref name="process"/> has been freed.</exception>
    public void MapPage(VirtualProcess<TAddress> process, VirtualPage page, TAddress virtualPageIndex, PageAccess access)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(page);
        ThrowIfProcessFreed(process);
        EnsureOwnedProcess(process);
        if (!_pages.Contains(page))
            throw new ArgumentException("The page does not belong to this VirtualMemory instance.", nameof(page));
        process.MapPage(virtualPageIndex, page, access);
    }

    /// <summary>
    /// Removes the mapping at <paramref name="virtualPageIndex"/> from <paramref name="process"/>'s
    /// page table. Does nothing when the index is not mapped.
    /// </summary>
    /// <param name="process">The process whose mapping is to be removed.</param>
    /// <param name="virtualPageIndex">The virtual page index to unmap.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="process"/> was not created by this instance.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when <paramref name="process"/> has been freed.</exception>
    public void UnmapPage(VirtualProcess<TAddress> process, TAddress virtualPageIndex)
    {
        ArgumentNullException.ThrowIfNull(process);
        ThrowIfProcessFreed(process);
        EnsureOwnedProcess(process);
        process.UnmapPage(virtualPageIndex);
    }

    private static void ThrowIfProcessFreed(VirtualProcess<TAddress> process)
    {
        if (process.IsFreed)
            throw new ObjectDisposedException(
                $"VirtualProcess #{process.ProcessId}",
                "This process has been freed and can no longer be used for memory operations.");
    }

    private void EnsureOwnedProcess(VirtualProcess<TAddress> process)
    {
        if (!_processes.Contains(process))
            throw new ArgumentException(
                "The process does not belong to this VirtualMemory instance.", nameof(process));
    }

    /// <summary>
    /// Removes all page mappings from <paramref name="process"/> and unregisters it from this
    /// <see cref="VirtualMemory{TAddress}"/> instance. Call this when a process has terminated
    /// to reclaim its page table entries and avoid unbounded growth of the process list.
    /// </summary>
    /// <param name="process">The process to free.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="process"/> is the master process, or when it does not belong
    /// to this <see cref="VirtualMemory{TAddress}"/> instance.
    /// </exception>
    public void FreeProcess(VirtualProcess<TAddress> process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (process.IsMaster)
            throw new ArgumentException("The master process cannot be freed.", nameof(process));
        if (!_processes.Remove(process))
            throw new ArgumentException("The process does not belong to this VirtualMemory instance.", nameof(process));
        process.ClearAllMappings();
        process.MarkFreed();
    }
}
