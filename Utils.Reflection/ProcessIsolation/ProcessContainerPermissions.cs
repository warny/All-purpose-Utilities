namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// Represents coarse-grained permissions for child processes started through a process container.
/// </summary>
public sealed class ProcessContainerPermissions
{
    /// <summary>
    /// Gets the default permission set used for plugin worker isolation.
    /// </summary>
    public static ProcessContainerPermissions Default { get; } = new();

    /// <summary>
    /// Gets or sets whether read access to files is allowed.
    /// This must remain enabled for managed executables and dependencies to load.
    /// </summary>
    public bool AllowDiskRead { get; set; } = true;

    /// <summary>
    /// Gets or sets whether write access to files is allowed.
    /// </summary>
    public bool AllowDiskWrite { get; set; }

    /// <summary>
    /// Gets or sets whether outbound/inbound network access is allowed.
    /// </summary>
    public bool AllowNetwork { get; set; }

    /// <summary>
    /// Gets or sets whether access to host devices is allowed.
    /// </summary>
    public bool AllowDeviceAccess { get; set; }

    /// <summary>
    /// Gets or sets whether process debugging / inspection capabilities are allowed.
    /// </summary>
    public bool AllowProcessDebugging { get; set; }
}
