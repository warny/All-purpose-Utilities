using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Represents a fixed-size physical memory page managed by a <see cref="VirtualMemory{TAddress}"/>.
/// Pages are created exclusively by <see cref="VirtualMemory{TAddress}.AllocatePage"/> and mapped
/// into process virtual address spaces with per-process access rights.
/// </summary>
public sealed class VirtualPage
{
    private readonly byte[] _data;

    /// <summary>Gets the unique identifier assigned to this page by its owning <see cref="VirtualMemory{TAddress}"/>.</summary>
    public int PageId { get; }

    /// <summary>Gets the size of this page in bytes.</summary>
    public int Size => _data.Length;

    /// <summary>
    /// Returns the page content as a <see cref="ReadOnlyMemory{T}"/>, suitable for use as
    /// <see cref="Context.Data"/> when the page holds an instruction stream.
    /// </summary>
    public ReadOnlyMemory<byte> AsReadOnlyMemory() => _data;

    /// <summary>Gets the raw backing array. Internal access only; never expose publicly.</summary>
    internal byte[] Data => _data;

    internal VirtualPage(int pageId, int pageSize)
    {
        PageId = pageId;
        _data = new byte[pageSize];
    }
}
