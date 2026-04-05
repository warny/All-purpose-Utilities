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
        string windowsDescription)
    {
        if (OperatingSystem.IsWindows())
        {
            return AppContainerSandbox.TryCreate(windowsContainerName, windowsDisplayName, windowsDescription);
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxBubblewrapContainer.TryCreate();
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacOsSandboxExecContainer.TryCreate();
        }

        return null;
    }
}
