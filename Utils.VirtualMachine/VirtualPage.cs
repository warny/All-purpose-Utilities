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
    /// <remarks>
    /// <para>
    /// This is a zero-copy view over the page's backing array. Page permissions (read-only,
    /// read-write) are a cooperative convention enforced by <see cref="VirtualProcess{TAddress}"/>
    /// operations — they are <b>not</b> enforced by the <see cref="ReadOnlyMemory{T}"/> type itself.
    /// Advanced APIs such as <see cref="System.Runtime.InteropServices.MemoryMarshal.TryGetArray{T}"/>
    /// can recover the underlying <c>ArraySegment&lt;byte&gt;</c> and write to the page directly,
    /// bypassing all access checks.
    /// </para>
    /// <para>
    /// The entire virtual-machine memory model is single-threaded and cooperative. Do not share
    /// pages with untrusted or concurrent code without additional isolation.
    /// </para>
    /// </remarks>
    public ReadOnlyMemory<byte> AsReadOnlyMemory() => _data;

    /// <summary>Gets the raw backing array. Internal access only; never expose publicly.</summary>
    internal byte[] Data => _data;

    internal VirtualPage(int pageId, int pageSize)
    {
        PageId = pageId;
        _data = new byte[pageSize];
    }
}
