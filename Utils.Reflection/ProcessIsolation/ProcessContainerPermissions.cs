namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// Represents coarse-grained permissions for child processes started through a process container.
/// Instances are immutable after construction (<c>init</c>-only properties): use an object
/// initializer to customize a permission set, or <c>Default</c> for the restrictive baseline.
/// </summary>
public sealed class ProcessContainerPermissions
{
    /// <summary>
    /// Gets a fresh instance of the default (most restrictive) permission set used for plugin
    /// worker isolation. Each access returns a new, independent instance, so callers cannot
    /// mutate a shared baseline and silently weaken isolation for other consumers.
    /// </summary>
    public static ProcessContainerPermissions Default => new();

    /// <summary>
    /// Gets whether read access to files is allowed.
    /// This must remain enabled for managed executables and dependencies to load.
    /// </summary>
    public bool AllowDiskRead { get; init; } = true;

    /// <summary>
    /// Gets whether write access to files is allowed.
    /// </summary>
    public bool AllowDiskWrite { get; init; }

    /// <summary>
    /// Gets whether outbound/inbound network access is allowed.
    /// </summary>
    public bool AllowNetwork { get; init; }

    /// <summary>
    /// Gets whether access to host devices is allowed.
    /// </summary>
    public bool AllowDeviceAccess { get; init; }

    /// <summary>
    /// Gets whether process debugging / inspection capabilities are allowed.
    /// </summary>
    public bool AllowProcessDebugging { get; init; }
}
