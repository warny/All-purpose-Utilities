using System;

namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// Builds process-container implementations based on the current operating system.
/// </summary>
public static class ProcessContainerFactory
{
    /// <summary>
    /// Creates the most restrictive container available on the current machine.
    /// </summary>
    /// <param name="windowsContainerName">Stable AppContainer name used on Windows.</param>
    /// <param name="windowsDisplayName">Display name for the Windows AppContainer profile.</param>
    /// <param name="windowsDescription">Description stored for the Windows AppContainer profile.</param>
    /// <returns>
    /// A process container when one can be provisioned; otherwise <see langword="null"/>
    /// so callers can gracefully fall back to a regular child process.
    /// </returns>
    public static IProcessContainer? TryCreate(
        string windowsContainerName,
        string windowsDisplayName,
        string windowsDescription,
        ProcessContainerPermissions? permissions = null)
    {
        permissions ??= ProcessContainerPermissions.Default;
        if (!permissions.AllowDiskRead)
        {
            return null;
        }

        if (OperatingSystem.IsWindows())
        {
            if (permissions.AllowNetwork ||
                permissions.AllowDiskWrite ||
                permissions.AllowDeviceAccess ||
                permissions.AllowProcessDebugging)
            {
                // Windows AppContainer support in this library currently targets restrictive mode.
                // When broader permissions are requested we skip containerization so the caller can
                // run a plain process with host permissions.
                return null;
            }

            return AppContainerSandbox.TryCreate(
                windowsContainerName,
                windowsDisplayName,
                windowsDescription,
                permissions);
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxBubblewrapContainer.TryCreate(permissions);
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacOsSandboxExecContainer.TryCreate(permissions);
        }

        return null;
    }
}
