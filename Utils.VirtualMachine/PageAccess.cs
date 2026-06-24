namespace Utils.VirtualMachine;

/// <summary>
/// Specifies the access rights for a virtual memory page mapping.
/// </summary>
public enum PageAccess
{
    /// <summary>The page may only be read; write attempts throw <see cref="MemoryAccessException"/>.</summary>
    ReadOnly,

    /// <summary>The page may be both read and written.</summary>
    ReadWrite,
}
