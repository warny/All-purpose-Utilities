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
/// <para>
/// <b>Thread safety:</b> this class is not thread-safe. All allocations, mappings, and process
/// operations must occur on the same thread. When the same <see cref="VirtualPage"/> is mapped
/// into multiple processes, concurrent reads or writes from different threads are not synchronized
/// and produce undefined behaviour.
/// </para>
/// </remarks>
public class VirtualMemory<TAddress> where TAddress : IBinaryInteger<TAddress>
{
    private readonly List<VirtualPage> _pages = [];
    private readonly List<VirtualProcess<TAddress>> _processes = [];
    private int _nextPageId;
    private int _nextProcessId;
    private TAddress _masterNextVirtualIndex = TAddress.Zero;
    private readonly int _maxPhysicalPages;
    private readonly int _maxMemoryProcesses;

    /// <summary>Gets the size in bytes of each physical page.</summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets the master process. It automatically receives <see cref="PageAccess.ReadWrite"/>
    /// mappings for every page allocated via <see cref="AllocatePage"/>.
    /// </summary>
    public VirtualProcess<TAddress> MasterProcess { get; }

    /// <summary>
    /// Gets the list of all physical pages managed by this instance.
    /// </summary>
    /// <remarks>This is a live view over the internal page list. Allocating new pages while
    /// enumerating may modify the list; take a snapshot (<c>.ToList()</c>) for stable enumeration.
    /// </remarks>
    public IReadOnlyList<VirtualPage> Pages => _pages;

    /// <summary>
    /// Gets the list of all processes, including the master process.
    /// </summary>
    /// <remarks>This is a live view over the internal process list. Creating or freeing processes
    /// while enumerating may modify the list; take a snapshot (<c>.ToList()</c>) for stable
    /// enumeration.
    /// </remarks>
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
        _maxPhysicalPages = int.MaxValue;
        _maxMemoryProcesses = int.MaxValue;
        MasterProcess = new VirtualProcess<TAddress>(_nextProcessId++, pageSize, isMaster: true);
        _processes.Add(MasterProcess);
    }

    /// <summary>
    /// Initializes a new <see cref="VirtualMemory{TAddress}"/> with limits from a
    /// <see cref="VirtualMachineLimits"/>, and creates the master process.
    /// </summary>
    /// <param name="pageSize">Size in bytes of each physical page. Defaults to <c>4096</c>.</param>
    /// <param name="limits">The limits policy to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageSize"/> is less than one.</exception>
    public VirtualMemory(int pageSize, VirtualMachineLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be at least 1.");
        PageSize = pageSize;
        _maxPhysicalPages = limits.MaxPhysicalPages;
        _maxMemoryProcesses = limits.MaxMemoryProcesses;
        MasterProcess = new VirtualProcess<TAddress>(_nextProcessId++, pageSize, isMaster: true);
        _processes.Add(MasterProcess);
    }

    /// <summary>
    /// Allocates a new physical page and automatically maps it into the master process
    /// at the next sequential virtual page index with <see cref="PageAccess.ReadWrite"/> rights.
    /// </summary>
    /// <returns>The newly allocated page.</returns>
    /// <exception cref="VmLimitExceededException">Thrown when the soft page limit has been reached.</exception>
    /// <exception cref="VmInvalidOperationException">
    /// Thrown when the hard page identifier counter has reached its maximum value, or when the
    /// master process virtual address space is exhausted.
    /// </exception>
    public VirtualPage AllocatePage()
    {
        // Soft limit: check before any mutation.
        if (_pages.Count >= _maxPhysicalPages)
            throw new VmLimitExceededException(VmLimitKind.PhysicalPageCount, _maxPhysicalPages, _pages.Count + 1L);
        // Hard limit.
        if (_nextPageId == int.MaxValue)
            throw new VmInvalidOperationException(
                $"Cannot allocate more pages: the page identifier counter has reached its maximum value ({int.MaxValue}).");
        TAddress nextIndex;
        try { nextIndex = checked(_masterNextVirtualIndex + TAddress.One); }
        catch (OverflowException)
        {
            throw new VmInvalidOperationException(
                "Cannot allocate more pages: the master process virtual address space is exhausted.");
        }
        // All checks passed — mutate.
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
    /// <exception cref="VmLimitExceededException">Thrown when the soft process limit has been reached.</exception>
    /// <exception cref="VmInvalidOperationException">Thrown when the hard process identifier counter has reached its maximum value.</exception>
    public VirtualProcess<TAddress> CreateProcess()
    {
        // Soft limit: check before hard id-counter limit.
        if (_processes.Count >= _maxMemoryProcesses)
            throw new VmLimitExceededException(VmLimitKind.MemoryProcessCount, _maxMemoryProcesses, _processes.Count + 1L);
        if (_nextProcessId == int.MaxValue)
            throw new VmInvalidOperationException(
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
    /// Frees a physical page, removing all mappings to it from every process in this instance
    /// and marking it as freed. Freed pages are removed from <see cref="Pages"/> and their
    /// backing memory is no longer accessible via <see cref="VirtualPage.AsReadOnlyMemory"/>.
    /// </summary>
    /// <param name="page">The page to free. Must have been allocated by this instance.</param>
    /// <param name="force">
    /// When <see langword="true"/>, removes all mappings in non-master processes before freeing.
    /// When <see langword="false"/> (default), throws <see cref="InvalidOperationException"/> if
    /// the page is still mapped into any non-master process.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="page"/> was not allocated by this instance.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when <paramref name="page"/> has already been freed.</exception>
    /// <exception cref="VmInvalidOperationException">
    /// Thrown when <paramref name="force"/> is <see langword="false"/> and the page is still
    /// mapped into one or more non-master processes. Unmap the page first or use <c>force: true</c>.
    /// </exception>
    public void FreePage(VirtualPage page, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.IsFreed)
            throw new ObjectDisposedException(
                $"VirtualPage #{page.PageId}", "The page has already been freed.");
        if (!_pages.Contains(page))
            throw new ArgumentException(
                "The page does not belong to this VirtualMemory instance.", nameof(page));

        // Enumerate non-master processes that still map this page.
        var nonMasterWithPage = _processes
            .Where(p => !p.IsMaster && p.ContainsPage(page))
            .ToList();

        if (!force && nonMasterWithPage.Count > 0)
            throw new VmInvalidOperationException(
                $"Cannot free page #{page.PageId}: it is still mapped into " +
                $"{nonMasterWithPage.Count} non-master process(es). " +
                "Unmap the page from all processes first, or call FreePage with force: true.");

        // If forced, remove from non-master processes.
        foreach (var proc in nonMasterWithPage)
            proc.RemoveMappingsForPage(page);

        // Always remove from the master process.
        MasterProcess.RemoveMappingsForPage(page);

        _pages.Remove(page);
        page.MarkFreed();
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
